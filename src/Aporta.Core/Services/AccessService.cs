using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Hubs;
using Aporta.Extensions.Endpoint;
using Aporta.Extensions.Hardware;
using Aporta.Shared.Messaging;
using Aporta.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Z9.Protobuf;
using Z9.Spcore.Proto;

namespace Aporta.Core.Services;

/// <summary>
/// Event args for access decision events, used to notify external systems.
/// </summary>
public class AccessDecisionEventArgs : EventArgs
{
    public string EndpointId { get; set; }
    public bool IsGranted { get; set; }
    public string CardNumber { get; set; }
    public string PersonName { get; set; }
    public EventReason Reason { get; set; }
    public bool UseExtendedTime { get; set; }
}

public class AccessService
{
    private readonly ExtensionService _extensionService;
    private readonly ILogger<AccessService> _logger;
    private readonly DoorRepository _doorRepository;
    private readonly EndpointRepository _endpointRepository;
    private readonly CredentialRepository _credentialRepository;
    private readonly EventRepository _eventRepository;
    private readonly Z9CredRepository _z9CredRepository;
    private readonly CredTemplateRepository _credTemplateRepository;
    private readonly PrivRepository _privRepository;
    private readonly SchedRepository _schedRepository;
    private readonly ConcurrentDictionary<string, Task> _processAccessCredential = new();
    private readonly IHubContext<DataChangeNotificationHub> _hubContext;
    private Func<string, int?> _getDoorUnidForEndpoint;
    private Func<string, (int?, int?)> _getStrikeTimesForEndpoint;
    private Func<string, DoorModeType?> _getDoorModeForEndpoint;
    private Func<string, string> _decodeCardData;
    private Func<string, int, int, Task<string>> _enrollCredential;

    /// <summary>
    /// Event raised when an access decision is made (granted or denied).
    /// External systems can subscribe to forward the decision.
    /// </summary>
    public event EventHandler<AccessDecisionEventArgs> AccessDecisionMade;

    /// <summary>
    /// Represents a service for accessing and managing system access.
    /// </summary>
    public AccessService(IDataAccess dataAccess, ExtensionService extensionService,
        IHubContext<DataChangeNotificationHub> hubContext, ILogger<AccessService> logger)
    {
        _doorRepository = new DoorRepository(dataAccess);
        _credentialRepository = new CredentialRepository(dataAccess);
        _endpointRepository = new EndpointRepository(dataAccess);
        _eventRepository = new EventRepository(dataAccess);
        _z9CredRepository = new Z9CredRepository(dataAccess);
        _credTemplateRepository = new CredTemplateRepository(dataAccess);
        _privRepository = new PrivRepository(dataAccess);
        _schedRepository = new SchedRepository(dataAccess);
        _extensionService = extensionService;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Sets a function to look up the Z9 Door unid for an endpoint.
    /// Called by StartupWorker after Z9OpenCommunityProtocolService is available.
    /// </summary>
    public void SetDoorUnidLookup(Func<string, int?> getDoorUnidForEndpoint)
    {
        _getDoorUnidForEndpoint = getDoorUnidForEndpoint;
    }

    /// <summary>
    /// Sets a function to look up the strike times (strikeTimeMs, extendedStrikeTimeMs) for an endpoint.
    /// </summary>
    public void SetStrikeTimeLookup(Func<string, (int?, int?)> getStrikeTimesForEndpoint)
    {
        _getStrikeTimesForEndpoint = getStrikeTimesForEndpoint;
    }

    /// <summary>
    /// Sets a function to look up the current door mode for an endpoint.
    /// </summary>
    public void SetDoorModeLookup(Func<string, DoorModeType?> getDoorModeForEndpoint)
    {
        _getDoorModeForEndpoint = getDoorModeForEndpoint;
    }

    /// <summary>
    /// Sets a function to decode raw card bit data into a credential number string.
    /// In Z9 mode, credentials are stored by decoded card number, so card reads
    /// must be decoded before matching. In standalone mode, this is not set and
    /// raw bit strings are matched directly.
    /// </summary>
    public void SetCardDataDecoder(Func<string, string> decodeCardData)
    {
        _decodeCardData = decodeCardData;
    }

    /// <summary>
    /// Sets a handler called during auto-enrollment to create Z9 data structures
    /// (DataFormat, CredTemplate, Z9 Cred) for a newly enrolled credential.
    /// Signature: (rawBits, credentialId, doorId) → credNum string.
    /// </summary>
    public void SetEnrollmentHandler(Func<string, int, int, Task<string>> enrollCredential)
    {
        _enrollCredential = enrollCredential;
    }

    public void Startup()
    {
        _extensionService.AccessCredentialReceived += ExtensionServiceOnAccessCredentialReceived;
    }

    public void Shutdown()
    {
        _processAccessCredential.Clear();
            
        _extensionService.AccessCredentialReceived -= ExtensionServiceOnAccessCredentialReceived;
    }

    private void ExtensionServiceOnAccessCredentialReceived(object sender,
        AccessCredentialReceivedEventArgs eventArgs)
    {
        bool foundTask = _processAccessCredential.TryGetValue(eventArgs.Access.Id, out var existingTask);

        if (foundTask && existingTask is { Status: TaskStatus.WaitingForActivation })
        {
            return;
        }

        _processAccessCredential[eventArgs.Access.Id] = Task.Run(() => ProcessAccessRequest(eventArgs));
    }

    private async Task ProcessAccessRequest(AccessCredentialReceivedEventArgs eventArgs)
    {
        try
        {
            var endpoints = (await _endpointRepository.GetAll()).ToArray();

            var matchingDoor = await MatchingDoor(eventArgs.Access.Id, endpoints);

            if (matchingDoor == null)
            {
                _logger.LogInformation(
                    "Credential received from '{Name}' was not assigned to a door", eventArgs.Access.Name);
                return;
            }

            var accessPoint = endpoints.First(endpoint => endpoint.DriverEndpointId == eventArgs.Access.Id);

            var matchingDoorStrike = MatchingDoorStrike(matchingDoor.DoorStrikeEndpointId, endpoints);

            if (!eventArgs.Handler.IsValid())
            {
                return;
            }

            // Check door mode before normal access control
            var doorMode = _getDoorModeForEndpoint?.Invoke(accessPoint.DriverEndpointId);
            if (doorMode == DoorModeType.StaticStateUnlocked)
            {
                _logger.LogInformation("Door '{Name}' is unlocked - granting access without credential check", matchingDoor.Name);
                await eventArgs.Access.AccessGrantedNotification();
                AccessDecisionMade?.Invoke(this, new AccessDecisionEventArgs
                {
                    EndpointId = accessPoint.DriverEndpointId,
                    IsGranted = true,
                    CardNumber = eventArgs.Handler.MatchingCardData,
                    Reason = EventReason.None
                });
                return;
            }
            if (doorMode == DoorModeType.StaticStateLocked)
            {
                _logger.LogInformation("Door '{Name}' is locked - denying access", matchingDoor.Name);
                await eventArgs.Access.AccessDeniedNotification();
                AccessDecisionMade?.Invoke(this, new AccessDecisionEventArgs
                {
                    EndpointId = accessPoint.DriverEndpointId,
                    IsGranted = false,
                    CardNumber = eventArgs.Handler.MatchingCardData,
                    Reason = EventReason.DoorLocked
                });
                return;
            }

            var (granted, useExtendedTime) = await IsAccessGranted(eventArgs.Handler.MatchingCardData, matchingDoor, accessPoint);
            if (granted)
            {
                // Only open door if strike is assigned - community controllers may not have one
                if (matchingDoorStrike != null)
                {
                    var strikeTimeMs = GetStrikeTimeMs(accessPoint.DriverEndpointId, useExtendedTime);
                    await OpenDoor(eventArgs.Access, matchingDoorStrike, strikeTimeMs);
                }
                else
                {
                    _logger.LogInformation("Door '{Name}' access granted but no strike assigned to operate", matchingDoor.Name);
                    await eventArgs.Access.AccessGrantedNotification();
                }
            }
            else
            {
                await eventArgs.Access.AccessDeniedNotification();
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to process access event");
        }
    }

    private async Task<(bool granted, bool useExtendedTime)> IsAccessGranted(string matchingCardData, Aporta.Shared.Models.Door matchingDoor, Endpoint accessPoint)
    {
        AssignedCredential assignedCredential;
        if (_decodeCardData != null)
        {
            // Z9 mode: decode raw bits to credential number, then look up by credNum
            var decodedCredNum = _decodeCardData(matchingCardData);
            assignedCredential = decodedCredNum != null
                ? await _credentialRepository.AssignedCredential(decodedCredNum)
                : null;
            if (assignedCredential != null)
            {
                _logger.LogDebug("Matched credential by decoded credNum: {CredNum}", decodedCredNum);
            }
        }
        else
        {
            // Standalone mode: match by raw bit string directly
            assignedCredential = await _credentialRepository.AssignedCredential(matchingCardData);
        }
        int eventId;
        if (assignedCredential?.Person == null)
        {
            _logger.LogInformation("Door '{Name}' badge requires enrollment", matchingDoor.Name);

            eventId = await _eventRepository.Insert(new Event
            {
                EndpointId = accessPoint.Id, Type = EventType.AccessDenied,
                Data = JsonSerializer.Serialize(new EventData
                {
                    Door = matchingDoor,
                    Endpoint = accessPoint,
                    EventReason = EventReason.CredentialNotEnrolled,
                    CardNumber = matchingCardData
                })
            });

            if (assignedCredential == null)
            {
                if (_enrollCredential != null)
                {
                    // Create credential and Z9 data chain (DataFormat, CredTemplate, Z9 Cred)
                    // but leave unassigned — admin must enroll via API
                    var credentialId = await _credentialRepository.Insert(new Credential
                        { Number = matchingCardData, LastEvent = eventId });
                    var credNum = await _enrollCredential(matchingCardData, credentialId, matchingDoor.Id);
                    // Update credential number to decoded credNum
                    var credential = await _credentialRepository.Get(credentialId);
                    if (credential != null)
                    {
                        credential.Number = credNum;
                        credential.LastEvent = eventId;
                        await _credentialRepository.Update(credential);
                    }
                }
                else
                {
                    await _credentialRepository.Insert(new Credential
                        { Number = matchingCardData, LastEvent = eventId });
                }
            }
            else
            {
                await _credentialRepository.UpdateLastEvent(assignedCredential.Id, eventId);
            }

            await _hubContext.Clients.All.SendAsync(Methods.NewEventReceived, eventId);

            // Notify external systems of access decision
            AccessDecisionMade?.Invoke(this, new AccessDecisionEventArgs
            {
                EndpointId = accessPoint.DriverEndpointId,
                IsGranted = false,
                CardNumber = matchingCardData,
                PersonName = null,
                Reason = EventReason.CredentialNotEnrolled
            });

            return (false, false);
        }

        // Check Z9 Cred status (enabled, effective date, credTemplate)
        var z9Cred = await _z9CredRepository.Get(assignedCredential.Id);
        if (z9Cred != null)
        {
            // Check credential template requirement
            if (z9Cred.CredTemplateUnidCase != Cred.CredTemplateUnidOneofCase.CredTemplateUnid
                || await _credTemplateRepository.Get(z9Cred.CredTemplateUnid) == null)
            {
                _logger.LogInformation("Door '{Name}' denied access - no credential template", matchingDoor.Name);

                eventId = await _eventRepository.Insert(new Event
                {
                    EndpointId = accessPoint.Id, Type = EventType.AccessDenied,
                    Data = JsonSerializer.Serialize(new EventData
                    {
                        Door = matchingDoor,
                        Endpoint = accessPoint,
                        Person = assignedCredential.Person,
                        EventReason = EventReason.NoCredentialTemplate,
                        CardNumber = matchingCardData
                    })
                });
                await _credentialRepository.UpdateLastEvent(assignedCredential.Id, eventId);
                await _hubContext.Clients.All.SendAsync(Methods.NewEventReceived, eventId);

                AccessDecisionMade?.Invoke(this, new AccessDecisionEventArgs
                {
                    EndpointId = accessPoint.DriverEndpointId,
                    IsGranted = false,
                    CardNumber = matchingCardData,
                    PersonName = assignedCredential.Person.FirstName,
                    Reason = EventReason.NoCredentialTemplate
                });

                return (false, false);
            }

            // Check if credential is disabled
            if (z9Cred.EnabledCase == Cred.EnabledOneofCase.Enabled && !z9Cred.Enabled)
            {
                _logger.LogInformation("Door '{Name}' denied access - credential disabled", matchingDoor.Name);

                eventId = await _eventRepository.Insert(new Event
                {
                    EndpointId = accessPoint.Id, Type = EventType.AccessDenied,
                    Data = JsonSerializer.Serialize(new EventData
                    {
                        Door = matchingDoor,
                        Endpoint = accessPoint,
                        Person = assignedCredential.Person,
                        EventReason = EventReason.CredentialDisabled,
                        CardNumber = matchingCardData
                    })
                });
                await _credentialRepository.UpdateLastEvent(assignedCredential.Id, eventId);
                await _hubContext.Clients.All.SendAsync(Methods.NewEventReceived, eventId);

                AccessDecisionMade?.Invoke(this, new AccessDecisionEventArgs
                {
                    EndpointId = accessPoint.DriverEndpointId,
                    IsGranted = false,
                    CardNumber = matchingCardData,
                    PersonName = assignedCredential.Person.FirstName,
                    Reason = EventReason.CredentialDisabled
                });

                return (false, false);
            }

            // Check if credential is not yet effective
            if (z9Cred.Effective != null && z9Cred.Effective.MillisCase == DateTimeData.MillisOneofCase.Millis)
            {
                var effectiveDate = DateTimeOffset.FromUnixTimeMilliseconds(z9Cred.Effective.Millis).DateTime;
                if (effectiveDate > DateTime.UtcNow)
                {
                    _logger.LogInformation("Door '{Name}' denied access - credential not yet effective (effective {Date})",
                        matchingDoor.Name, effectiveDate.ToShortDateString());

                    eventId = await _eventRepository.Insert(new Event
                    {
                        EndpointId = accessPoint.Id, Type = EventType.AccessDenied,
                        Data = JsonSerializer.Serialize(new EventData
                        {
                            Door = matchingDoor,
                            Endpoint = accessPoint,
                            Person = assignedCredential.Person,
                            EventReason = EventReason.CredentialNotYetEffective,
                            CardNumber = matchingCardData
                        })
                    });
                    await _credentialRepository.UpdateLastEvent(assignedCredential.Id, eventId);
                    await _hubContext.Clients.All.SendAsync(Methods.NewEventReceived, eventId);

                    AccessDecisionMade?.Invoke(this, new AccessDecisionEventArgs
                    {
                        EndpointId = accessPoint.DriverEndpointId,
                        IsGranted = false,
                        CardNumber = matchingCardData,
                        PersonName = assignedCredential.Person.FirstName,
                        Reason = EventReason.CredentialNotYetEffective
                    });

                    return (false, false);
                }
            }

            // Check if credential has expired
            if (z9Cred.Expires != null && z9Cred.Expires.MillisCase == DateTimeData.MillisOneofCase.Millis)
            {
                var expiresDate = DateTimeOffset.FromUnixTimeMilliseconds(z9Cred.Expires.Millis).DateTime;
                if (expiresDate < DateTime.UtcNow)
                {
                    _logger.LogInformation("Door '{Name}' denied access - credential expired (expired {Date})",
                        matchingDoor.Name, expiresDate.ToShortDateString());

                    eventId = await _eventRepository.Insert(new Event
                    {
                        EndpointId = accessPoint.Id, Type = EventType.AccessDenied,
                        Data = JsonSerializer.Serialize(new EventData
                        {
                            Door = matchingDoor,
                            Endpoint = accessPoint,
                            Person = assignedCredential.Person,
                            EventReason = EventReason.CredentialExpired,
                            CardNumber = matchingCardData
                        })
                    });
                    await _credentialRepository.UpdateLastEvent(assignedCredential.Id, eventId);
                    await _hubContext.Clients.All.SendAsync(Methods.NewEventReceived, eventId);

                    AccessDecisionMade?.Invoke(this, new AccessDecisionEventArgs
                    {
                        EndpointId = accessPoint.DriverEndpointId,
                        IsGranted = false,
                        CardNumber = matchingCardData,
                        PersonName = assignedCredential.Person.FirstName,
                        Reason = EventReason.CredentialExpired
                    });

                    return (false, false);
                }
            }
        }

        var privilegeDenialReason = await CheckAccessPrivilege(assignedCredential.Id, matchingDoor, accessPoint.DriverEndpointId);
        if (privilegeDenialReason != null)
        {
            var reason = privilegeDenialReason.Value;
            var reasonText = reason == EventReason.OutsideSchedule ? "outside schedule" : "no privilege";
            _logger.LogInformation("Door '{Name}' denied access - {Reason}", matchingDoor.Name, reasonText);

            eventId = await _eventRepository.Insert(new Event
            {
                EndpointId = accessPoint.Id, Type = EventType.AccessDenied,
                Data = JsonSerializer.Serialize(new EventData
                {
                    Door = matchingDoor,
                    Endpoint = accessPoint,
                    Person = assignedCredential.Person,
                    EventReason = reason,
                    CardNumber = matchingCardData
                })
            });
            await _credentialRepository.UpdateLastEvent(assignedCredential.Id, eventId);

            await _hubContext.Clients.All.SendAsync(Methods.NewEventReceived, eventId);

            // Notify external systems of access decision
            AccessDecisionMade?.Invoke(this, new AccessDecisionEventArgs
            {
                EndpointId = accessPoint.DriverEndpointId,
                IsGranted = false,
                CardNumber = matchingCardData,
                PersonName = assignedCredential.Person.FirstName,
                Reason = reason
            });

            return (false, false);
        }

        // Check if credential has extended door time modifier
        var useExtendedTime = z9Cred?.DoorAccessModifiers?.ExtDoorTimeCase ==
            DoorAccessModifiers.ExtDoorTimeOneofCase.ExtDoorTime && z9Cred.DoorAccessModifiers.ExtDoorTime;

        _logger.LogInformation("Door '{Name}' granted access{ExtTime}", matchingDoor.Name,
            useExtendedTime ? " (extended time)" : "");

        eventId = await _eventRepository.Insert(new Event
        {
            EndpointId = accessPoint.Id, Type = EventType.AccessGranted,
            Data = JsonSerializer.Serialize(new EventData
            {
                Door = matchingDoor,
                Endpoint = accessPoint,
                Person = assignedCredential.Person,
                EventReason = EventReason.None,
                CardNumber = matchingCardData
            })
        });
        await _credentialRepository.UpdateLastEvent(assignedCredential.Id, eventId);

        await _hubContext.Clients.All.SendAsync(Methods.NewEventReceived, eventId);

        // Notify external systems of access decision
        AccessDecisionMade?.Invoke(this, new AccessDecisionEventArgs
        {
            EndpointId = accessPoint.DriverEndpointId,
            IsGranted = true,
            CardNumber = matchingCardData,
            PersonName = assignedCredential.Person.FirstName,
            Reason = EventReason.None,
            UseExtendedTime = useExtendedTime
        });

        return (true, useExtendedTime);
    }

    private int GetStrikeTimeMs(string endpointId, bool useExtendedTime)
    {
        var (strikeTimeMs, extendedStrikeTimeMs) = _getStrikeTimesForEndpoint?.Invoke(endpointId) ?? (null, null);
        if (useExtendedTime && extendedStrikeTimeMs.HasValue)
            return extendedStrikeTimeMs.Value;
        return strikeTimeMs ?? 3000;
    }

    private async Task OpenDoor(IAccess access, Endpoint matchingDoorStrike, int strikeTimeMs)
    {
        var controlPoint =
            _extensionService.GetControlPoint(matchingDoorStrike.ExtensionId, matchingDoorStrike.DriverEndpointId); 
            
        async Task ControlStrike()
        {
            await controlPoint.SetState(true);
            await Task.Delay(TimeSpan.FromMilliseconds(strikeTimeMs));
            await controlPoint.SetState(false);
        } 

        // Fire strike asynchronously - don't block access processing
        // (blocking would keep the task in WaitingForActivation state,
        // causing the deduplication check to drop subsequent card reads)
        _ = Task.Run(ControlStrike);

        await access.AccessGrantedNotification();
    }

    /// <summary>
    /// Checks if the credential has privilege to access the door.
    /// Returns null if access is granted, or the EventReason for denial.
    /// </summary>
    private async Task<EventReason?> CheckAccessPrivilege(int credentialUnid, Aporta.Shared.Models.Door door, string endpointId)
    {
        // Look up the Z9 Door unid for this endpoint
        int? z9DoorUnid = _getDoorUnidForEndpoint?.Invoke(endpointId);

        // Look up the full Z9 Cred proto to check privilege bindings
        var z9Cred = await _z9CredRepository.Get(credentialUnid);
        if (z9Cred == null)
        {
            _logger.LogDebug("Z9 Cred not found for unid {Unid}, denying access", credentialUnid);
            return EventReason.NoPrivilege;
        }

        if (z9Cred.PrivBindings == null || z9Cred.PrivBindings.Count == 0)
        {
            _logger.LogDebug("Credential {Unid} has no privilege bindings, denying access", credentialUnid);
            return EventReason.NoPrivilege;
        }

        // Track if we found any valid privilege (to distinguish NO_PRIV from OUTSIDE_SCHED)
        bool foundValidPrivilege = false;

        // Check each privilege binding
        foreach (var binding in z9Cred.PrivBindings)
        {
            // Check for precision access (direct door reference via devAsDoorAccessPrivUnid)
            if (binding.DevAsDoorAccessPrivUnidCase == CredPrivBinding.DevAsDoorAccessPrivUnidOneofCase.DevAsDoorAccessPrivUnid)
            {
                // Check if this precision access applies to our door
                if (z9DoorUnid.HasValue && binding.DevAsDoorAccessPrivUnid != z9DoorUnid.Value)
                {
                    _logger.LogDebug("Credential {Unid} has precision access to door {PrivDoor} but not to {ActualDoor}",
                        credentialUnid, binding.DevAsDoorAccessPrivUnid, z9DoorUnid.Value);
                    continue; // This binding doesn't apply to our door
                }

                foundValidPrivilege = true;

                // Check schedule restriction on the binding
                if (!await IsInSchedule(binding.SchedRestriction))
                {
                    _logger.LogDebug("Credential {Unid} has precision access but outside schedule",
                        credentialUnid);
                    continue; // Try other bindings
                }

                _logger.LogDebug("Credential {Unid} has precision access binding to door unid {DoorUnid}",
                    credentialUnid, binding.DevAsDoorAccessPrivUnid);
                return null;  // Access granted
            }

            // Check for privilege reference (DoorAccessPriv)
            if (binding.PrivUnidCase == CredPrivBinding.PrivUnidOneofCase.PrivUnid)
            {
                var priv = await _privRepository.Get(binding.PrivUnid);
                if (priv == null)
                {
                    _logger.LogDebug("Privilege {Unid} not found for credential {CredUnid}",
                        binding.PrivUnid, credentialUnid);
                    continue;
                }

                // Check if this is a door access privilege
                if (priv.PrivType == PrivType.Door && priv.Enabled)
                {
                    // Check if any DoorAccessPrivElement matches our door
                    bool doorMatches = await CheckDoorAccessPrivElements(priv, z9DoorUnid);
                    if (!doorMatches)
                    {
                        _logger.LogDebug("Credential {Unid} has privilege {PrivName} but it doesn't include door {DoorUnid}",
                            credentialUnid, priv.Name, z9DoorUnid);
                        continue; // This privilege doesn't cover our door
                    }

                    foundValidPrivilege = true;

                    // Check schedule restriction on the binding
                    if (!await IsInSchedule(binding.SchedRestriction))
                    {
                        _logger.LogDebug("Credential {Unid} has privilege {PrivName} but outside schedule",
                            credentialUnid, priv.Name);
                        continue; // Try other bindings
                    }

                    // TODO: Also check element-level schedule restrictions

                    _logger.LogDebug("Credential {Unid} has door access privilege {PrivUnid} ({PrivName})",
                        credentialUnid, priv.Unid, priv.Name);
                    return null;  // Access granted
                }
            }
        }

        // If we found valid privileges but all were outside schedule, return OutsideSchedule
        if (foundValidPrivilege)
        {
            _logger.LogDebug("Credential {Unid} has valid privileges but all are outside schedule for door {DoorName}",
                credentialUnid, door.Name);
            return EventReason.OutsideSchedule;
        }

        _logger.LogDebug("Credential {Unid} has no matching privilege for door {DoorName}",
            credentialUnid, door.Name);
        return EventReason.NoPrivilege;
    }

    /// <summary>
    /// Checks if a DoorAccessPriv includes the specified door.
    /// </summary>
    private Task<bool> CheckDoorAccessPrivElements(Priv priv, int? z9DoorUnid)
    {
        var doorAccessPriv = priv.ExtDoorAccessPriv;
        if (doorAccessPriv == null || doorAccessPriv.Elements == null || doorAccessPriv.Elements.Count == 0)
        {
            // No elements means no doors covered
            _logger.LogDebug("DoorAccessPriv {PrivUnid} has no elements", priv.Unid);
            return Task.FromResult(false);
        }

        // If we don't know the Z9 door unid, we can't do door-specific checking
        // Fall back to allowing access if there are any elements
        if (!z9DoorUnid.HasValue)
        {
            _logger.LogDebug("No Z9 door unid available, allowing any DoorAccessPriv element");
            return Task.FromResult(true);
        }

        foreach (var element in doorAccessPriv.Elements)
        {
            // Check if element specifies a door
            if (element.DoorUnidCase == DoorAccessPrivElement.DoorUnidOneofCase.DoorUnid)
            {
                if (element.DoorUnid == z9DoorUnid.Value)
                {
                    _logger.LogDebug("DoorAccessPrivElement matches door {DoorUnid}", z9DoorUnid.Value);
                    return Task.FromResult(true);
                }
            }
            else
            {
                // Element has no specific door - means "all doors"
                // (community profile doesn't support devGroup or controlledArea)
                _logger.LogDebug("DoorAccessPrivElement has no specific door, treating as 'all doors'");
                return Task.FromResult(true);
            }
        }

        _logger.LogDebug("No DoorAccessPrivElement matches door {DoorUnid}", z9DoorUnid.Value);
        return Task.FromResult(false);
    }

    /// <summary>
    /// Checks if the current time is within the schedule restriction.
    /// Returns true if in schedule or no schedule restriction (null = always).
    /// </summary>
    private async Task<bool> IsInSchedule(SchedRestriction schedRestriction)
    {
        // No schedule restriction means "always" (24/7)
        if (schedRestriction == null)
            return true;

        // No schedule unid means "always"
        if (schedRestriction.SchedUnidCase != SchedRestriction.SchedUnidOneofCase.SchedUnid)
            return true;

        var sched = await _schedRepository.Get(schedRestriction.SchedUnid);
        if (sched == null)
        {
            _logger.LogWarning("Schedule {Unid} not found, treating as always active", schedRestriction.SchedUnid);
            return true;
        }

        // TODO: Pass holidays when we have HolRepository
        bool inSched = SchedEvaluator.InSched(DateTime.Now, sched, null);

        // Apply invert flag
        if (schedRestriction.Invert)
            inSched = !inSched;

        return inSched;
    }

    private async Task<Aporta.Shared.Models.Door> MatchingDoor(string accessPointId, Endpoint[] endpoints)
    {
        var doors = await _doorRepository.GetAll();

        var matchingDoor = doors.FirstOrDefault(door =>
            MatchingEndpointId(endpoints, accessPointId, door.InAccessEndpointId) ||
            MatchingEndpointId(endpoints, accessPointId, door.OutAccessEndpointId));
        return matchingDoor;
    }
        
    private static bool MatchingEndpointId(IEnumerable<Endpoint> endpoints, string accessPointId, int? endpointId)
    {
        if (endpointId == null) return false;
        return endpointId == endpoints.FirstOrDefault(endpoint => endpoint.DriverEndpointId == accessPointId)?.Id;
    }

    private static Endpoint MatchingDoorStrike(int? doorStrikeEndpointId, IEnumerable<Endpoint> endpoints)
    {
        return endpoints.FirstOrDefault(endpoint => doorStrikeEndpointId == endpoint.Id);
    }
}