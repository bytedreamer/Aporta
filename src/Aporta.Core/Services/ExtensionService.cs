using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Extension;
using Aporta.Core.Hubs;
using Aporta.Core.Models;
using Aporta.Extensions;
using Aporta.Extensions.Endpoint;
using Aporta.Extensions.Hardware;
using Aporta.Shared.Messaging;
using Aporta.Shared.Models;
using System.Net;

namespace Aporta.Core.Services;

/// <summary>
/// 
/// </summary>
public class ExtensionService
{
    private static readonly SemaphoreSlim EndpointUpdateSemaphore = new(1, 1);

    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ExtensionService> _logger;
    private readonly ExtensionRepository _extensionRepository;
    private readonly EndpointRepository _endpointRepository;
    private readonly DoorRepository _doorRepository;
    private readonly IHubContext<DataChangeNotificationHub> _hubContext;
    private readonly IDataEncryption _dataEncryption;
    private readonly List<ExtensionHost> _extensions = new();
    private readonly object _extensionLock = new ();

    public ExtensionService(IDataAccess dataAccess, IHubContext<DataChangeNotificationHub> hubContext, 
        IDataEncryption dataEncryption, ILogger<ExtensionService> logger, ILoggerFactory loggerFactory)
    {
        _hubContext = hubContext;
        _dataEncryption = dataEncryption;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _endpointRepository = new EndpointRepository(dataAccess);
        _extensionRepository = new ExtensionRepository(dataAccess);
        _doorRepository = new DoorRepository(dataAccess);
    }
        
    public string CurrentDirectory { get; init; }

    public async Task Startup()
    {
        _logger.LogInformation("Starting extension service");

        await DiscoverExtensions();

        LoadExtensions();
    }

    public void Shutdown()
    {
        _logger.LogInformation("Shutting down extension service");

        UnloadExtensions();
    }

    public async Task EnableExtension(Guid extensionId, bool enabled)
    {
        try
        {
            var matchingExtension = _extensions.First(extension => extension.Id == extensionId);

            _logger.LogInformation("{Enabled} extension {Name}", enabled ? "Enabling" : "Disabling", matchingExtension.Name);

            matchingExtension.Enabled = enabled;
            await _extensionRepository.Update(matchingExtension);

            if (enabled)
            {
                LoadExtension(matchingExtension);
            }
            else
            {
                UnloadExtension(matchingExtension);
            }
        }
        finally
        {
            await _hubContext.Clients.All.SendAsync(Methods.ExtensionDataChanged, extensionId);
        }
    }

    public async Task<string> PerformAction(Guid extensionId, string action, string parameters)
    {
        var matchingExtension = MatchingExtensionHost(extensionId);

        _logger.LogInformation("Performing {Action} for extension {Name}", action, matchingExtension.Name);

        string result = await matchingExtension.Driver.PerformAction(action, parameters);

        _logger.LogInformation("Saving configuration for extension {Name}", matchingExtension.Name);

        //await SaveCurrentConfiguration(matchingExtension);

        return result;
    }

    private ExtensionHost MatchingExtensionHost(Guid extensionId)
    {
        return _extensions.First(extension => extension.Id == extensionId);
    }

    public IOutput GetControlPoint(Guid extensionId, string endpointId)
    {
        return _extensions.First(extension => extension.Id == extensionId).Driver.Endpoints
            .First(endpoint => endpoint.Id == endpointId) as IOutput;
    }
        
    public IInput GetMonitorPoint(Guid extensionId, string endpointId)
    {
        return _extensions.First(extension => extension.Id == extensionId).Driver.Endpoints
            .First(endpoint => endpoint.Id == endpointId) as IInput;
    }

    public IAccess GetAccessPoint(Guid extensionId, string endpointId)
    {
        return _extensions.First(extension => extension.Id == extensionId).Driver.Endpoints
            .First(endpoint => endpoint.Id == endpointId) as IAccess;
    }

    public IEnumerable<Shared.Models.Extension> GetExtensions()
    {
        return _extensions.Select(extension =>
        {
            var clone =  extension.ShallowCopy();
            clone.Configuration = extension.Driver?.ScrubSensitiveConfigurationData(clone.Configuration);
            return clone;
        });
    }
    
    public Shared.Models.Extension GetExtension(Guid extensionId)
    {
        var extension = _extensions.FirstOrDefault(extension => extension.Id == extensionId);
        var clone = extension?.ShallowCopy();
        
        if (clone == null) return null;
        
        clone.Configuration = extension.Driver?.ScrubSensitiveConfigurationData(clone.Configuration);

        return clone;
    }

    public event EventHandler<AccessCredentialReceivedEventArgs> AccessCredentialReceived;
        
    public event EventHandler<StateChangedEventArgs> StateChanged;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task DiscoverExtensions()
    {
        var extensionFinder = new Finder<IHardwareDriver>();
        var assemblyPaths =
            extensionFinder.FindAssembliesWithPlugins(
                Path.Combine(CurrentDirectory ??
                             Path.GetDirectoryName(CurrentDirectory ?? Assembly.GetEntryAssembly()?.Location) ?? Environment.CurrentDirectory,
                    "Drivers"), _loggerFactory.CreateLogger<Finder<IHardwareDriver>>());

        foreach (string assemblyPath in assemblyPaths)
        {
            try
            {
                await GetExtensionsFromAssembly(assemblyPath);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unable to discover assembly {AssemblyPath}", assemblyPath);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task GetExtensionsFromAssembly(string assemblyPath)
    {
        var host = new Host<IHardwareDriver>(assemblyPath);
        host.Load();

        foreach (var driver in host.GetExtensions())
        {
            var extension = await _extensionRepository.Get(driver.Id);
            if (extension == null)
            {
                extension = new ExtensionHost
                    {Enabled = false, Id = driver.Id, Name = driver.Name};
                await _extensionRepository.Insert(extension);
            }

            extension.AssemblyPath = assemblyPath;

            _extensions.Add(extension);
        }

        host.Unload();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void LoadExtensions()
    {
        foreach (var extension in _extensions.Where(extension => extension.Enabled))
        {
            try
            {
                LoadExtension(extension);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unable to load extension {Name}", extension.Name);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void LoadExtension(ExtensionHost extension)
    {
        lock (_extensionLock)
        {
            if (extension.Loaded)
            {
                return;
            }

            extension.Host = new Host<IHardwareDriver>(extension.AssemblyPath);
            extension.Host.Load();
            extension.Driver = extension.Host.GetExtensions().First(ext => ext.Id == extension.Id);
            extension.Driver.UpdatedEndpoints += DriverOnUpdatedEndpoints;
            extension.Driver.AccessCredentialReceived += DriverOnAccessCredentialReceived;
            extension.Driver.StateChanged += DriverOnStateChanged;

            extension.Driver.Load(extension.Configuration, _dataEncryption, _loggerFactory);
            extension.Configuration = extension.Driver.CurrentConfiguration();

            extension.Loaded = true;
        }
    }

    private void DriverOnUpdatedEndpoints(object sender, EventArgs eventArgs)
    {
        if (sender is not IHardwareDriver driver) return;

        Task.Run(async () =>
        {
            await EndpointUpdateSemaphore.WaitAsync();

            try
            {
                var existingEndpoints = (await _endpointRepository.GetForExtension(driver.Id)).ToArray();
                foreach (var endpoint in EndpointsToBeInserted(driver, existingEndpoints))
                {
                    var insertEndpoint = new Endpoint
                    {
                        DriverEndpointId = endpoint.Id, ExtensionId = driver.Id, Name = endpoint.Name,
                        Type = endpoint switch
                        {
                            IOutput _ => EndpointType.Output,
                            IInput _ => EndpointType.Input,
                            IAccess _ => EndpointType.Reader,
                            _ => throw new Exception("Invalid endpoint type")
                        }
                    };

                    await _endpointRepository.Insert(insertEndpoint);
                }

                foreach (var endpoint in EndpointsToBeUpdated(driver, existingEndpoints))
                {
                    var updateEndpoint = new Endpoint
                    {
                        DriverEndpointId = endpoint.Id, ExtensionId = driver.Id, Name = endpoint.Name,
                        Type = endpoint switch
                        {
                            IOutput _ => EndpointType.Output,
                            IInput _ => EndpointType.Input,
                            IAccess _ => EndpointType.Reader,
                            _ => throw new Exception("Invalid endpoint type")
                        }
                    };

                    await _endpointRepository.Update(updateEndpoint);
                }

                var okToSave = true;
                var allEndPoints = await _endpointRepository.GetAll();
                var doors = (await _doorRepository.GetAll()).ToArray();
                foreach (var endpoint in EndpointsToBeDeleted(driver, existingEndpoints))
                {

                    var isAvailable = IsEndPointAvailableForDelete(endpoint, doors, allEndPoints);

                    if (isAvailable)
                    {
                        await _endpointRepository.Delete(endpoint.Id);
                    } else
                    {
                        okToSave = false;
                        //throw new Exception($"Endpoint {endpoint.Name} not available for delete.");
                        //Add the endpoint back if it is not
                    }
                    
                }

                await SaveCurrentConfiguration(MatchingExtensionHost(driver.Id));
            }
            finally
            {
                EndpointUpdateSemaphore.Release();
            }
        });
    }


    private bool IsEndPointAvailableForDelete(Endpoint endpointToDelete, Door[] doors, IEnumerable<Endpoint> endpoints)
    {

        var availableEndPoints = endpoints.Where(endpoint =>
            endpoint.Type == EndpointType.Reader &&
            !doors.Select(door => door.InAccessEndpointId).Contains(endpoint.Id) &&
            !doors.Select(door => door.OutAccessEndpointId).Contains(endpoint.Id));

        return availableEndPoints.Count(endpoint => endpoint.DriverEndpointId == endpointToDelete.DriverEndpointId) > 0;
    }

    private bool IsEndPointAssignedToDoorPrev(Endpoint endpointToDelete, Door[] doors)
    {

        var count = (doors.Select(door => door.InAccessEndpointId == endpointToDelete.Id || door.OutAccessEndpointId == endpointToDelete.Id)).Count();

        return (doors.Select(door => door.InAccessEndpointId == endpointToDelete.Id || door.OutAccessEndpointId == endpointToDelete.Id)).Count() > 0;

    }

    private void DriverOnAccessCredentialReceived(object sender, AccessCredentialReceivedEventArgs eventArgs)
    {
        AccessCredentialReceived?.Invoke(this, eventArgs);
    }
        
    private void DriverOnStateChanged(object sender, StateChangedEventArgs eventArgs)
    {
        StateChanged?.Invoke(this, eventArgs);
    }

    private static IEnumerable<IEndpoint> EndpointsToBeInserted(IHardwareDriver driver,
        Endpoint[] existingEndpoints)
    {
        return driver.Endpoints.Where(endpoint =>
            endpoint.ExtensionId == driver.Id && !existingEndpoints
                .Select(existingEndpoint => existingEndpoint.DriverEndpointId).Contains(endpoint.Id));
    }
    
    private static IEnumerable<IEndpoint> EndpointsToBeUpdated(IHardwareDriver driver, Endpoint[] existingEndpoints)
    {
        return driver.Endpoints.Where(endpoint =>
            endpoint.ExtensionId == driver.Id && existingEndpoints
                .Select(existingEndpoint => existingEndpoint.DriverEndpointId).Contains(endpoint.Id));
    }

    private static IEnumerable<Endpoint> EndpointsToBeDeleted(IHardwareDriver driver,
        IEnumerable<Endpoint> existingEndpoints)
    {
        return existingEndpoints.Where(existingEndpoint =>
            existingEndpoint.ExtensionId == driver.Id && !driver.Endpoints
                .Select(endpoint => endpoint.Id).Contains(existingEndpoint.DriverEndpointId));
    }

    private async Task SaveCurrentConfiguration(ExtensionHost extension)
    {
        try
        {
            extension.Configuration = extension.Driver.CurrentConfiguration();
            await _extensionRepository.Update(extension);
        }
        finally
        {
            await _hubContext.Clients.All.SendAsync(Methods.ExtensionDataChanged, extension.Id);
        }
    }

    private void UnloadExtensions()
    {
        foreach (var extension in _extensions)
        {
            try
            {
                UnloadExtension(extension);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unable to unload extension {Name}", extension.Name);
            }
        }

        _extensions.Clear();
    }

    private void UnloadExtension(ExtensionHost extension)
    {
        lock (_extensionLock)
        {
            if (!extension.Loaded)
            {
                return;
            }
                
            extension.Driver.Unload();
            extension.Driver.UpdatedEndpoints -= DriverOnUpdatedEndpoints;
            extension.Driver.AccessCredentialReceived -= DriverOnAccessCredentialReceived;
            extension.Driver.StateChanged -= DriverOnStateChanged;

            extension.Host.Unload();
            extension.Loaded = false;
        }
    }
}