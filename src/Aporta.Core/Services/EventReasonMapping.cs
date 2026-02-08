using System;
using Aporta.Shared.Models;
using Z9.Spcore.Proto;

namespace Aporta.Core.Services;

/// <summary>
/// Bidirectional mapping between classic Aporta EventType/EventReason
/// and Z9/Open EvtCode/EvtSubCode.
/// </summary>
public static class EventReasonMapping
{
    /// <summary>
    /// Maps classic EventType + EventReason to Z9/Open EvtCode + EvtSubCode.
    /// evtSubCode is null for granted events.
    /// </summary>
    public static (EvtCode evtCode, EvtSubCode? evtSubCode) ToEvtCodes(
        EventType eventType, EventReason reason)
    {
        if (eventType == EventType.AccessGranted)
            return (EvtCode.DoorAccessGranted, null);

        var subCode = reason switch
        {
            EventReason.CredentialNotEnrolled => EvtSubCode.AccessDeniedUnknownCredNum,
            EventReason.NoCredentialTemplate => EvtSubCode.AccessDeniedUnknownCredNumFormat,
            EventReason.CredentialDisabled => EvtSubCode.AccessDeniedInactive,
            EventReason.CredentialNotYetEffective => EvtSubCode.AccessDeniedNotEffective,
            EventReason.CredentialExpired => EvtSubCode.AccessDeniedExpired,
            EventReason.NoPrivilege => EvtSubCode.AccessDeniedNoPriv,
            EventReason.AccessNotAssigned => EvtSubCode.AccessDeniedNoPriv,
            EventReason.OutsideSchedule => EvtSubCode.AccessDeniedOutsideSched,
            EventReason.DoorLocked => EvtSubCode.AccessDeniedDoorModeStaticLocked,
            EventReason.UnknownUniquePin => EvtSubCode.AccessDeniedUnknownCredUniquePin,
            EventReason.NoConfirmingPin => EvtSubCode.AccessDeniedNoConfirmingPinForCred,
            EventReason.IncorrectConfirmingPin => EvtSubCode.AccessDeniedIncorrectConfirmingPin,
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason,
                $"No EvtSubCode mapping for EventReason.{reason}")
        };

        return (EvtCode.DoorAccessDenied, subCode);
    }

    /// <summary>
    /// Reverse mapping: Z9/Open EvtCode + EvtSubCode back to classic EventType + EventReason.
    /// Used by EventService to reconstruct Event DTOs from Evt protos.
    /// </summary>
    public static (EventType eventType, EventReason reason) ToEventTypeAndReason(
        EvtCode evtCode, EvtSubCode evtSubCode)
    {
        if (evtCode == EvtCode.DoorAccessGranted)
            return (EventType.AccessGranted, EventReason.None);

        var reason = evtSubCode switch
        {
            EvtSubCode.AccessDeniedUnknownCredNum => EventReason.CredentialNotEnrolled,
            EvtSubCode.AccessDeniedUnknownCredNumFormat => EventReason.NoCredentialTemplate,
            EvtSubCode.AccessDeniedInactive => EventReason.CredentialDisabled,
            EvtSubCode.AccessDeniedNotEffective => EventReason.CredentialNotYetEffective,
            EvtSubCode.AccessDeniedExpired => EventReason.CredentialExpired,
            EvtSubCode.AccessDeniedNoPriv => EventReason.NoPrivilege,
            EvtSubCode.AccessDeniedOutsideSched => EventReason.OutsideSchedule,
            EvtSubCode.AccessDeniedDoorModeStaticLocked => EventReason.DoorLocked,
            EvtSubCode.AccessDeniedUnknownCredUniquePin => EventReason.UnknownUniquePin,
            EvtSubCode.AccessDeniedNoConfirmingPinForCred => EventReason.NoConfirmingPin,
            EvtSubCode.AccessDeniedIncorrectConfirmingPin => EventReason.IncorrectConfirmingPin,
            _ => EventReason.None
        };

        return (EventType.AccessDenied, reason);
    }
}
