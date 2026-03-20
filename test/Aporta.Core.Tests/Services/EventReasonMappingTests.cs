using System;
using Aporta.Core.Services;
using Aporta.Shared.Models;
using NUnit.Framework;
using Z9.Spcore.Proto;

namespace Aporta.Core.Tests.Services;

public class EventReasonMappingTests
{
    [Test]
    public void AccessGranted_MapsToEvtCode()
    {
        var (evtCode, evtSubCode) =
            EventReasonMapping.ToEvtCodes(EventType.AccessGranted, EventReason.None);

        Assert.That(evtCode, Is.EqualTo(EvtCode.DoorAccessGranted));
        Assert.That(evtSubCode, Is.Null);
    }

    [TestCase(EventReason.CredentialNotEnrolled, EvtSubCode.AccessDeniedUnknownCredNum)]
    [TestCase(EventReason.NoCredentialTemplate, EvtSubCode.AccessDeniedUnknownCredNumFormat)]
    [TestCase(EventReason.CredentialDisabled, EvtSubCode.AccessDeniedInactive)]
    [TestCase(EventReason.CredentialNotYetEffective, EvtSubCode.AccessDeniedNotEffective)]
    [TestCase(EventReason.CredentialExpired, EvtSubCode.AccessDeniedExpired)]
    [TestCase(EventReason.NoPrivilege, EvtSubCode.AccessDeniedNoPriv)]
    [TestCase(EventReason.AccessNotAssigned, EvtSubCode.AccessDeniedNoPriv)]
    [TestCase(EventReason.OutsideSchedule, EvtSubCode.AccessDeniedOutsideSched)]
    public void AccessDenied_MapsToCorrectSubCode(EventReason reason, EvtSubCode expectedSubCode)
    {
        var (evtCode, evtSubCode) =
            EventReasonMapping.ToEvtCodes(EventType.AccessDenied, reason);

        Assert.That(evtCode, Is.EqualTo(EvtCode.DoorAccessDenied));
        Assert.That(evtSubCode, Is.EqualTo(expectedSubCode));
    }

    [Test]
    public void DoorLocked_MapsToStaticLocked_NotNoPriv()
    {
        var (evtCode, evtSubCode) =
            EventReasonMapping.ToEvtCodes(EventType.AccessDenied, EventReason.DoorLocked);

        Assert.That(evtCode, Is.EqualTo(EvtCode.DoorAccessDenied));
        Assert.That(evtSubCode, Is.EqualTo(EvtSubCode.AccessDeniedDoorModeStaticLocked));
    }

    [TestCase(EventReason.DoorUsed)]
    [TestCase(EventReason.DoorNotUsed)]
    public void UnmappedReason_Throws(EventReason reason)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventReasonMapping.ToEvtCodes(EventType.AccessDenied, reason));
    }

    [Test]
    public void ReverseMapping_AccessGranted()
    {
        var (eventType, reason) =
            EventReasonMapping.ToEventTypeAndReason(EvtCode.DoorAccessGranted, 0);

        Assert.That(eventType, Is.EqualTo(EventType.AccessGranted));
        Assert.That(reason, Is.EqualTo(EventReason.None));
    }

    [TestCase(EvtSubCode.AccessDeniedUnknownCredNum, EventReason.CredentialNotEnrolled)]
    [TestCase(EvtSubCode.AccessDeniedUnknownCredNumFormat, EventReason.NoCredentialTemplate)]
    [TestCase(EvtSubCode.AccessDeniedInactive, EventReason.CredentialDisabled)]
    [TestCase(EvtSubCode.AccessDeniedNotEffective, EventReason.CredentialNotYetEffective)]
    [TestCase(EvtSubCode.AccessDeniedExpired, EventReason.CredentialExpired)]
    [TestCase(EvtSubCode.AccessDeniedNoPriv, EventReason.NoPrivilege)]
    [TestCase(EvtSubCode.AccessDeniedOutsideSched, EventReason.OutsideSchedule)]
    [TestCase(EvtSubCode.AccessDeniedDoorModeStaticLocked, EventReason.DoorLocked)]
    public void ReverseMapping_AccessDenied(EvtSubCode subCode, EventReason expectedReason)
    {
        var (eventType, reason) =
            EventReasonMapping.ToEventTypeAndReason(EvtCode.DoorAccessDenied, subCode);

        Assert.That(eventType, Is.EqualTo(EventType.AccessDenied));
        Assert.That(reason, Is.EqualTo(expectedReason));
    }
}
