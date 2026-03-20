using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Aporta.Shared.Models.Flex;
using Google.Protobuf;
using Z9.Protobuf;
using Z9.Spcore.Proto;

namespace Aporta.Core.Services;

public static class FlexMapper
{
    // --- ObjRef helpers ---

    public static FlexObjRef MakeObjRef(string type, int unid, string name = null, string uuid = null, string tag = null, string externalId = null)
    {
        return new FlexObjRef { Type = type, Unid = unid, Name = name, Uuid = uuid, Tag = tag, ExternalId = externalId };
    }

    // --- DateTime helpers ---

    public static string DateTimeDataToIso(DateTimeData dt)
    {
        if (dt == null) return null;
        return DateTimeOffset.FromUnixTimeMilliseconds(dt.Millis).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
    }

    public static DateTimeData IsoToDateTimeData(string iso)
    {
        if (string.IsNullOrEmpty(iso)) return null;
        if (DateTimeOffset.TryParse(iso, out var dto))
            return new DateTimeData { Millis = dto.ToUnixTimeMilliseconds() };
        return null;
    }

    public static string SqlDateToIso(SqlDateData d)
    {
        if (d == null) return null;
        return $"{d.Year:D4}-{d.Month:D2}-{d.Day:D2}";
    }

    public static SqlDateData IsoToSqlDate(string iso)
    {
        if (string.IsNullOrEmpty(iso)) return null;
        if (DateTime.TryParse(iso, out var dt))
            return new SqlDateData { Year = dt.Year, Month = dt.Month, Day = dt.Day };
        return null;
    }

    public static string SqlTimeToString(SqlTimeData t)
    {
        if (t == null) return null;
        return $"{t.Hour:D2}:{t.Minute:D2}:{t.Second:D2}";
    }

    public static SqlTimeData StringToSqlTime(string s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        if (TimeSpan.TryParse(s, out var ts))
            return new SqlTimeData { Hour = ts.Hours, Minute = ts.Minutes, Second = ts.Seconds };
        return null;
    }

    // --- BigInteger helpers ---

    public static string BigIntegerDataToString(BigIntegerData data)
    {
        if (data == null || data.BytesCase == BigIntegerData.BytesOneofCase.None) return null;
        return SpCoreProtoUtil.ToBigInteger(data).ToString();
    }

    public static BigIntegerData StringToBigIntegerData(string s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        if (BigInteger.TryParse(s, out var val))
            return SpCoreProtoUtil.ToBigIntegerData(val);
        return null;
    }

    // --- Cred ---

    public static FlexCred ToFlex(Cred proto, Func<int, string, FlexObjRef> lookupRef = null)
    {
        var flex = new FlexCred
        {
            Version = proto.Version,
            Tag = proto.Tag,
            Uuid = proto.Uuid,
            Unid = proto.Unid,
            Name = proto.Name,
            Enabled = proto.Enabled,
            Effective = proto.Effective != null ? DateTimeDataToIso(proto.Effective) : null,
            Expires = proto.Expires != null ? DateTimeDataToIso(proto.Expires) : null,
        };

        if (proto.CredTemplateUnid != 0)
            flex.CredTemplate = lookupRef?.Invoke(proto.CredTemplateUnid, "credTemplate") ?? MakeObjRef("credTemplate", proto.CredTemplateUnid);

        if (proto.CardPin != null)
            flex.CardPin = ToFlexCardPin(proto.CardPin);

        if (proto.DoorAccessModifiers != null)
            flex.DoorAccessModifiers = new FlexDoorAccessModifiers
            {
                ExtDoorTime = proto.DoorAccessModifiers.ExtDoorTimeCase == DoorAccessModifiers.ExtDoorTimeOneofCase.ExtDoorTime
                    ? proto.DoorAccessModifiers.ExtDoorTime : null
            };

        if (proto.PrivBindings.Count > 0)
        {
            flex.PrivBindings = proto.PrivBindings.Select(pb => ToFlexCredPrivBinding(pb, lookupRef)).ToList();
        }

        return flex;
    }

    public static Cred ToProto(FlexCred flex)
    {
        var proto = new Cred
        {
            Version = flex.Version ?? 0,
            Tag = flex.Tag ?? "",
            Uuid = flex.Uuid ?? "",
            Unid = flex.Unid ?? 0,
            Name = flex.Name ?? "",
            Enabled = flex.Enabled ?? true,
        };

        if (flex.Effective != null)
            proto.Effective = IsoToDateTimeData(flex.Effective);
        if (flex.Expires != null)
            proto.Expires = IsoToDateTimeData(flex.Expires);
        if (flex.CredTemplate?.Unid != null)
            proto.CredTemplateUnid = flex.CredTemplate.Unid.Value;

        if (flex.CardPin != null)
            proto.CardPin = ToProtoCardPin(flex.CardPin);

        if (flex.DoorAccessModifiers != null)
        {
            proto.DoorAccessModifiers = new DoorAccessModifiers();
            if (flex.DoorAccessModifiers.ExtDoorTime.HasValue)
                proto.DoorAccessModifiers.ExtDoorTime = flex.DoorAccessModifiers.ExtDoorTime.Value;
        }

        if (flex.PrivBindings != null)
        {
            foreach (var pb in flex.PrivBindings)
                proto.PrivBindings.Add(ToProtoCredPrivBinding(pb));
        }

        return proto;
    }

    private static FlexCardPin ToFlexCardPin(CardPin cp)
    {
        return new FlexCardPin
        {
            CredNum = BigIntegerDataToString(cp.CredNum),
            FacilityCode = cp.FacilityCodeCase == CardPin.FacilityCodeOneofCase.FacilityCode ? cp.FacilityCode : null,
            Pin = cp.Pin,
            PinUnique = cp.PinUniqueCase == CardPin.PinUniqueOneofCase.PinUnique ? cp.PinUnique : null,
        };
    }

    private static CardPin ToProtoCardPin(FlexCardPin flex)
    {
        var cp = new CardPin
        {
            Pin = flex.Pin ?? "",
        };
        if (flex.CredNum != null)
            cp.CredNum = StringToBigIntegerData(flex.CredNum);
        if (flex.FacilityCode.HasValue)
            cp.FacilityCode = flex.FacilityCode.Value;
        if (flex.PinUnique.HasValue)
            cp.PinUnique = flex.PinUnique.Value;
        return cp;
    }

    private static FlexCredPrivBinding ToFlexCredPrivBinding(CredPrivBinding pb, Func<int, string, FlexObjRef> lookupRef)
    {
        var flex = new FlexCredPrivBinding
        {
            Unid = pb.Unid,
        };

        if (pb.PrivUnid != 0)
            flex.Priv = lookupRef?.Invoke(pb.PrivUnid, "priv") ?? MakeObjRef("priv", pb.PrivUnid);

        if (pb.DevAsDoorAccessPrivUnidCase == CredPrivBinding.DevAsDoorAccessPrivUnidOneofCase.DevAsDoorAccessPrivUnid)
            flex.DevAsDoorAccessPriv = lookupRef?.Invoke(pb.DevAsDoorAccessPrivUnid, "dev") ?? MakeObjRef("dev", pb.DevAsDoorAccessPrivUnid);

        if (pb.SchedRestriction != null)
            flex.SchedRestriction = ToFlexSchedRestriction(pb.SchedRestriction, lookupRef);

        return flex;
    }

    private static CredPrivBinding ToProtoCredPrivBinding(FlexCredPrivBinding flex)
    {
        var pb = new CredPrivBinding { Unid = flex.Unid ?? 0 };
        if (flex.Priv?.Unid != null) pb.PrivUnid = flex.Priv.Unid.Value;
        if (flex.DevAsDoorAccessPriv?.Unid != null) pb.DevAsDoorAccessPrivUnid = flex.DevAsDoorAccessPriv.Unid.Value;
        if (flex.SchedRestriction != null)
            pb.SchedRestriction = ToProtoSchedRestriction(flex.SchedRestriction);
        return pb;
    }

    // --- SchedRestriction ---

    private static FlexSchedRestriction ToFlexSchedRestriction(SchedRestriction sr, Func<int, string, FlexObjRef> lookupRef)
    {
        var flex = new FlexSchedRestriction
        {
            Invert = sr.Invert,
        };
        if (sr.SchedUnidCase == SchedRestriction.SchedUnidOneofCase.SchedUnid)
            flex.Sched = lookupRef?.Invoke(sr.SchedUnid, "sched") ?? MakeObjRef("sched", sr.SchedUnid);
        return flex;
    }

    private static SchedRestriction ToProtoSchedRestriction(FlexSchedRestriction flex)
    {
        var sr = new SchedRestriction { Invert = flex.Invert ?? false };
        if (flex.Sched?.Unid != null) sr.SchedUnid = flex.Sched.Unid.Value;
        return sr;
    }

    // --- CredTemplate ---

    public static FlexCredTemplate ToFlex(CredTemplate proto, Func<int, string, FlexObjRef> lookupRef = null)
    {
        var flex = new FlexCredTemplate
        {
            Version = proto.Version,
            Tag = proto.Tag,
            Uuid = proto.Uuid,
            Name = proto.Name,
            Unid = proto.Unid,
            Priority = proto.Priority,
        };

        if (proto.CardPinTemplate != null)
            flex.CardPinTemplate = ToFlexCardPinTemplate(proto.CardPinTemplate, lookupRef);

        return flex;
    }

    public static CredTemplate ToProto(FlexCredTemplate flex)
    {
        var proto = new CredTemplate
        {
            Version = flex.Version ?? 0,
            Tag = flex.Tag ?? "",
            Uuid = flex.Uuid ?? "",
            Name = flex.Name ?? "",
            Unid = flex.Unid ?? 0,
            Priority = flex.Priority ?? 0,
        };

        if (flex.CardPinTemplate != null)
            proto.CardPinTemplate = ToProtoCardPinTemplate(flex.CardPinTemplate);

        return proto;
    }

    private static FlexCardPinTemplate ToFlexCardPinTemplate(CardPinTemplate cpt, Func<int, string, FlexObjRef> lookupRef)
    {
        var flex = new FlexCardPinTemplate
        {
            CredComponentPresence = (int)cpt.CredComponentPresence,
            CredNumPresence = (int)cpt.CredNumPresence,
            PinPresence = (int)cpt.PinPresence,
            PinUnique = cpt.PinUniqueCase == CardPinTemplate.PinUniqueOneofCase.PinUnique ? cpt.PinUnique : null,
            MinPinLength = cpt.MinPinLength,
            MaxPinLength = cpt.MaxPinLength,
            FacilityCode = cpt.FacilityCode,
            AnyDataLayout = cpt.AnyDataLayout,
        };

        if (cpt.DataLayoutUnid != 0)
            flex.DataLayout = lookupRef?.Invoke(cpt.DataLayoutUnid, "dataLayout") ?? MakeObjRef("dataLayout", cpt.DataLayoutUnid);

        return flex;
    }

    private static CardPinTemplate ToProtoCardPinTemplate(FlexCardPinTemplate flex)
    {
        var cpt = new CardPinTemplate
        {
            CredComponentPresence = (CredComponentPresence)(flex.CredComponentPresence ?? 0),
            CredNumPresence = (CredComponentPresence)(flex.CredNumPresence ?? 0),
            PinPresence = (CredComponentPresence)(flex.PinPresence ?? 0),
            MinPinLength = flex.MinPinLength ?? 0,
            MaxPinLength = flex.MaxPinLength ?? 0,
            FacilityCode = flex.FacilityCode ?? 0,
            AnyDataLayout = flex.AnyDataLayout ?? false,
        };
        if (flex.PinUnique.HasValue) cpt.PinUnique = flex.PinUnique.Value;
        if (flex.DataLayout?.Unid != null) cpt.DataLayoutUnid = flex.DataLayout.Unid.Value;
        return cpt;
    }

    // --- Evt ---

    public static FlexEvt ToFlex(Evt proto)
    {
        var flex = new FlexEvt
        {
            Unid = proto.Unid,
            HwTime = DateTimeDataToIso(proto.HwTime),
            DbTime = DateTimeDataToIso(proto.DbTime),
            HwTimeZone = proto.HwTimeZone,
            EvtCode = (int)proto.EvtCode,
            EvtCodeText = Messages.GetEvtCodeText(proto.EvtCode),
            ExternalEvtCodeText = string.IsNullOrEmpty(proto.ExternalEvtCodeText) ? null : proto.ExternalEvtCodeText,
            ExternalEvtCodeId = string.IsNullOrEmpty(proto.ExternalEvtCodeId) ? null : proto.ExternalEvtCodeId,
            EvtSubCode = proto.EvtSubCodeCase == Evt.EvtSubCodeOneofCase.EvtSubCode ? (int)proto.EvtSubCode : null,
            EvtSubCodeText = proto.EvtSubCodeCase == Evt.EvtSubCodeOneofCase.EvtSubCode ? Messages.GetEvtSubCodeText(proto.EvtSubCode) : null,
            ExternalSubCodeText = string.IsNullOrEmpty(proto.ExternalSubCodeText) ? null : proto.ExternalSubCodeText,
            ExternalSubCodeId = string.IsNullOrEmpty(proto.ExternalSubCodeId) ? null : proto.ExternalSubCodeId,
            Priority = proto.Priority,
            Data = string.IsNullOrEmpty(proto.Data) ? null : proto.Data,
            Consumed = proto.Consumed,
            Uuid = string.IsNullOrEmpty(proto.Uuid) ? null : proto.Uuid,
        };

        if (proto.EvtModifiers != null)
            flex.EvtModifiers = new FlexEvtModifiers
            {
                UsedCard = proto.EvtModifiers.UsedCardCase == EvtModifiers.UsedCardOneofCase.UsedCard ? proto.EvtModifiers.UsedCard : null,
                UsedPin = proto.EvtModifiers.UsedPinCase == EvtModifiers.UsedPinOneofCase.UsedPin ? proto.EvtModifiers.UsedPin : null,
            };

        if (proto.EvtDevRef != null)
            flex.EvtDevRef = ToFlexEvtDevRef(proto.EvtDevRef);

        if (proto.EvtControllerRef != null)
            flex.EvtControllerRef = ToFlexEvtDevRef(proto.EvtControllerRef);

        if (proto.EvtCredRef != null)
            flex.EvtCredRef = ToFlexEvtCredRef(proto.EvtCredRef);

        if (proto.EvtSchedRef != null)
            flex.EvtSchedRef = ToFlexEvtSchedRef(proto.EvtSchedRef);

        return flex;
    }

    private static FlexEvtDevRef ToFlexEvtDevRef(EvtDevRef r)
    {
        return new FlexEvtDevRef
        {
            Unid = r.Unid,
            LogicalAddress = r.LogicalAddress,
            Name = r.Name,
            Address = r.Address,
            Tag = r.Tag,
            Uuid = r.Uuid,
            ExternalId = r.ExternalId,
            DevPlatform = (int)r.DevPlatform,
            DevType = (int)r.DevType,
            DevSubType = (int)r.DevSubType,
            DevMod = (int)r.DevMod,
            DevUse = (int)r.DevUse,
            ExternalDevTypeText = string.IsNullOrEmpty(r.ExternalDevTypeText) ? null : r.ExternalDevTypeText,
            ExternalDevTypeId = string.IsNullOrEmpty(r.ExternalDevTypeId) ? null : r.ExternalDevTypeId,
            ExternalDevModText = string.IsNullOrEmpty(r.ExternalDevModText) ? null : r.ExternalDevModText,
            ExternalDevModId = string.IsNullOrEmpty(r.ExternalDevModId) ? null : r.ExternalDevModId,
        };
    }

    private static FlexEvtCredRef ToFlexEvtCredRef(EvtCredRef r)
    {
        return new FlexEvtCredRef
        {
            Unid = r.Unid,
            CredTemplateRef = r.CredTemplateRef != null ? new FlexEvtCredTemplateRef
            {
                Unid = r.CredTemplateRef.Unid,
                Name = r.CredTemplateRef.Name,
                Tag = r.CredTemplateRef.Tag,
                Uuid = r.CredTemplateRef.Uuid,
            } : null,
            Name = r.Name,
            CredNum = BigIntegerDataToString(r.CredNum),
            FacilityCode = r.FacilityCode,
            Tag = r.Tag,
            Uuid = r.Uuid,
        };
    }

    private static FlexEvtSchedRef ToFlexEvtSchedRef(EvtSchedRef r)
    {
        return new FlexEvtSchedRef
        {
            Unid = r.Unid,
            Name = r.Name,
            Tag = r.Tag,
            Uuid = r.Uuid,
            Invert = r.Invert,
        };
    }

    // --- Dev ---

    public static FlexDev ToFlex(Dev proto, Func<int, string, FlexObjRef> lookupRef = null)
    {
        var flex = new FlexDev
        {
            Version = proto.Version,
            Tag = proto.Tag,
            Uuid = proto.Uuid,
            Name = proto.Name,
            ExternalId = proto.ExternalId,
            Unid = proto.Unid,
            Address = proto.Address,
            LogicalAddress = proto.LogicalAddressCase == Dev.LogicalAddressOneofCase.LogicalAddress ? proto.LogicalAddress : null,
            MacAddress = string.IsNullOrEmpty(proto.MacAddress) ? null : proto.MacAddress,
            Enabled = proto.Enabled,
            Port = proto.Port,
            Speed = proto.Speed,
            DevType = (int)proto.DevType,
            DevSubType = (int)proto.DevSubType,
            DevMod = (int)proto.DevMod,
            DevPlatform = (int)proto.DevPlatform,
            DevUse = (int)proto.DevUse,
            TimeZone = string.IsNullOrEmpty(proto.TimeZone) ? null : proto.TimeZone,
            IgnoreDaylightSavings = proto.IgnoreDaylightSavings,
        };

        if (proto.PhysicalParentUnid != 0)
            flex.PhysicalParent = lookupRef?.Invoke(proto.PhysicalParentUnid, "dev") ?? MakeObjRef("dev", proto.PhysicalParentUnid);
        if (proto.LogicalParentUnid != 0)
            flex.LogicalParent = lookupRef?.Invoke(proto.LogicalParentUnid, "dev") ?? MakeObjRef("dev", proto.LogicalParentUnid);

        if (proto.LogicalChildrenUnid.Count > 0)
            flex.LogicalChildren = proto.LogicalChildrenUnid.Select(u => lookupRef?.Invoke(u, "dev") ?? MakeObjRef("dev", u)).ToList();
        if (proto.PhysicalChildrenUnid.Count > 0)
            flex.PhysicalChildren = proto.PhysicalChildrenUnid.Select(u => lookupRef?.Invoke(u, "dev") ?? MakeObjRef("dev", u)).ToList();

        if (proto.DevModConfig != null)
            flex.DevModConfig = new FlexDevModConfig
            {
                Unid = proto.DevModConfig.Unid,
                Version = proto.DevModConfig.Version,
                Type = (int)proto.DevModConfig.Type,
            };

        // Type-specific extension
        switch (proto.ExtensionCase)
        {
            case Dev.ExtensionOneofCase.ExtDoor:
                flex.DoorConfig = ToFlexDoorConfig(proto.ExtDoor, lookupRef);
                break;
            case Dev.ExtensionOneofCase.ExtCredReader:
                flex.CredReaderConfig = ToFlexCredReaderConfig(proto.ExtCredReader, lookupRef);
                break;
            case Dev.ExtensionOneofCase.ExtController:
                flex.ControllerConfig = ToFlexControllerConfig(proto.ExtController, lookupRef);
                break;
            case Dev.ExtensionOneofCase.ExtActuator:
                flex.ActuatorConfig = ToFlexActuatorConfig(proto.ExtActuator, lookupRef);
                break;
            case Dev.ExtensionOneofCase.ExtSensor:
                flex.SensorConfig = ToFlexSensorConfig(proto.ExtSensor, lookupRef);
                break;
        }

        return flex;
    }

    public static Dev ToProto(FlexDev flex)
    {
        var proto = new Dev
        {
            Version = flex.Version ?? 0,
            Tag = flex.Tag ?? "",
            Uuid = flex.Uuid ?? "",
            Name = flex.Name ?? "",
            ExternalId = flex.ExternalId ?? "",
            Unid = flex.Unid ?? 0,
            Address = flex.Address ?? "",
            Enabled = flex.Enabled ?? true,
            Port = flex.Port ?? 0,
            Speed = flex.Speed ?? 0,
            DevType = (DevType)(flex.DevType ?? 0),
            DevSubType = (DevSubType)(flex.DevSubType ?? 0),
            DevMod = (DevMod)(flex.DevMod ?? 0),
            DevPlatform = (DevPlatform)(flex.DevPlatform ?? 0),
            DevUse = (DevUse)(flex.DevUse ?? 0),
            TimeZone = flex.TimeZone ?? "",
            IgnoreDaylightSavings = flex.IgnoreDaylightSavings ?? false,
        };

        if (flex.LogicalAddress.HasValue) proto.LogicalAddress = flex.LogicalAddress.Value;
        if (flex.MacAddress != null) proto.MacAddress = flex.MacAddress;
        if (flex.PhysicalParent?.Unid != null) proto.PhysicalParentUnid = flex.PhysicalParent.Unid.Value;
        if (flex.LogicalParent?.Unid != null) proto.LogicalParentUnid = flex.LogicalParent.Unid.Value;

        if (flex.LogicalChildren != null)
            foreach (var c in flex.LogicalChildren.Where(c => c.Unid.HasValue))
                proto.LogicalChildrenUnid.Add(c.Unid.Value);
        if (flex.PhysicalChildren != null)
            foreach (var c in flex.PhysicalChildren.Where(c => c.Unid.HasValue))
                proto.PhysicalChildrenUnid.Add(c.Unid.Value);

        return proto;
    }

    private static FlexDoorConfig ToFlexDoorConfig(Z9.Spcore.Proto.Door door, Func<int, string, FlexObjRef> lookupRef)
    {
        var config = door.DoorConfig;
        if (config == null) return null;
        var flex = new FlexDoorConfig
        {
            Version = config.Version,
            Username = config.Username,
            Password = config.Password,
            DevInitiatesConnection = config.DevInitiatesConnectionCase == DoorConfig.DevInitiatesConnectionOneofCase.DevInitiatesConnection ? config.DevInitiatesConnection : null,
            DisableEncryption = config.DisableEncryptionCase == DoorConfig.DisableEncryptionOneofCase.DisableEncryption ? config.DisableEncryption : null,
            ActivateStrikeOnRex = config.ActivateStrikeOnRexCase == DoorConfig.ActivateStrikeOnRexOneofCase.ActivateStrikeOnRex ? config.ActivateStrikeOnRex : null,
            StrikeTime = config.StrikeTimeCase == DoorConfig.StrikeTimeOneofCase.StrikeTime ? config.StrikeTime : null,
            ExtendedStrikeTime = config.ExtendedStrikeTimeCase == DoorConfig.ExtendedStrikeTimeOneofCase.ExtendedStrikeTime ? config.ExtendedStrikeTime : null,
            HeldTime = config.HeldTimeCase == DoorConfig.HeldTimeOneofCase.HeldTime ? config.HeldTime : null,
            ExtendedHeldTime = config.ExtendedHeldTimeCase == DoorConfig.ExtendedHeldTimeOneofCase.ExtendedHeldTime ? config.ExtendedHeldTime : null,
        };

        if (config.DefaultDoorMode != null)
            flex.DefaultDoorMode = ToFlexDoorMode(config.DefaultDoorMode);

        if (config.EncryptionKeyRefUnid != 0)
            flex.EncryptionKeyRef = lookupRef?.Invoke(config.EncryptionKeyRefUnid, "encryptionKey") ?? MakeObjRef("encryptionKey", config.EncryptionKeyRefUnid);
        if (config.EncryptionKeyRefNextUnid != 0)
            flex.EncryptionKeyRefNext = lookupRef?.Invoke(config.EncryptionKeyRefNextUnid, "encryptionKey") ?? MakeObjRef("encryptionKey", config.EncryptionKeyRefNextUnid);

        return flex;
    }

    private static FlexCredReaderConfig ToFlexCredReaderConfig(CredReader cr, Func<int, string, FlexObjRef> lookupRef)
    {
        var config = cr.CredReaderConfig;
        if (config == null) return null;
        return new FlexCredReaderConfig
        {
            Version = config.Version,
            Username = config.Username,
            Password = config.Password,
            DevInitiatesConnection = config.DevInitiatesConnectionCase == CredReaderConfig.DevInitiatesConnectionOneofCase.DevInitiatesConnection ? config.DevInitiatesConnection : null,
            DisableEncryption = config.DisableEncryptionCase == CredReaderConfig.DisableEncryptionOneofCase.DisableEncryption ? config.DisableEncryption : null,
            CommType = (int)config.CommType,
            TamperType = (int)config.TamperType,
            LedType = (int)config.LedType,
            SerialPortAddress = string.IsNullOrEmpty(config.SerialPortAddress) ? null : config.SerialPortAddress,
        };
    }

    private static FlexControllerConfig ToFlexControllerConfig(Controller ctrl, Func<int, string, FlexObjRef> lookupRef)
    {
        var config = ctrl.ControllerConfig;
        if (config == null) return null;
        return new FlexControllerConfig
        {
            Version = config.Version,
            Username = config.Username,
            Password = config.Password,
            DevInitiatesConnection = config.DevInitiatesConnectionCase == ControllerConfig.DevInitiatesConnectionOneofCase.DevInitiatesConnection ? config.DevInitiatesConnection : null,
            DisableEncryption = config.DisableEncryptionCase == ControllerConfig.DisableEncryptionOneofCase.DisableEncryption ? config.DisableEncryption : null,
        };
    }

    private static FlexActuatorConfig ToFlexActuatorConfig(Actuator act, Func<int, string, FlexObjRef> lookupRef)
    {
        var config = act.ActuatorConfig;
        if (config == null) return null;
        return new FlexActuatorConfig
        {
            Version = config.Version,
            Username = config.Username,
            Password = config.Password,
            DevInitiatesConnection = config.DevInitiatesConnectionCase == ActuatorConfig.DevInitiatesConnectionOneofCase.DevInitiatesConnection ? config.DevInitiatesConnection : null,
            DisableEncryption = config.DisableEncryptionCase == ActuatorConfig.DisableEncryptionOneofCase.DisableEncryption ? config.DisableEncryption : null,
            Invert = config.InvertCase == ActuatorConfig.InvertOneofCase.Invert ? config.Invert : null,
        };
    }

    private static FlexSensorConfig ToFlexSensorConfig(Sensor sensor, Func<int, string, FlexObjRef> lookupRef)
    {
        var config = sensor.SensorConfig;
        if (config == null) return null;
        return new FlexSensorConfig
        {
            Version = config.Version,
            Username = config.Username,
            Password = config.Password,
            DevInitiatesConnection = config.DevInitiatesConnectionCase == SensorConfig.DevInitiatesConnectionOneofCase.DevInitiatesConnection ? config.DevInitiatesConnection : null,
            DisableEncryption = config.DisableEncryptionCase == SensorConfig.DisableEncryptionOneofCase.DisableEncryption ? config.DisableEncryption : null,
            Invert = config.InvertCase == SensorConfig.InvertOneofCase.Invert ? config.Invert : null,
        };
    }

    public static FlexDoorMode ToFlexDoorMode(DoorMode dm)
    {
        return new FlexDoorMode
        {
            StaticState = (int)dm.StaticState,
            AllowUniquePin = dm.AllowUniquePinCase == DoorMode.AllowUniquePinOneofCase.AllowUniquePin ? dm.AllowUniquePin : null,
            AllowCard = dm.AllowCardCase == DoorMode.AllowCardOneofCase.AllowCard ? dm.AllowCard : null,
            RequireConfirmingPinWithCard = dm.RequireConfirmingPinWithCardCase == DoorMode.RequireConfirmingPinWithCardOneofCase.RequireConfirmingPinWithCard ? dm.RequireConfirmingPinWithCard : null,
        };
    }

    // --- Priv (DoorAccessPriv) ---

    public static FlexPriv ToFlex(Priv proto, Func<int, string, FlexObjRef> lookupRef = null)
    {
        var flex = new FlexPriv
        {
            Version = proto.Version,
            Tag = proto.Tag,
            Uuid = proto.Uuid,
            Name = proto.Name,
            ExternalId = proto.ExternalId,
            Unid = proto.Unid,
            Enabled = proto.Enabled,
            PrivType = (int)proto.PrivType,
        };

        if (proto.ExtensionCase == Priv.ExtensionOneofCase.ExtDoorAccessPriv && proto.ExtDoorAccessPriv.Elements.Count > 0)
        {
            flex.Elements = proto.ExtDoorAccessPriv.Elements.Select(e => new FlexDoorAccessPrivElement
            {
                Unid = e.Unid,
                Door = e.DoorUnidCase == DoorAccessPrivElement.DoorUnidOneofCase.DoorUnid
                    ? (lookupRef?.Invoke(e.DoorUnid, "door") ?? MakeObjRef("door", e.DoorUnid))
                    : null,
                SchedRestriction = e.SchedRestriction != null ? ToFlexSchedRestriction(e.SchedRestriction, lookupRef) : null,
            }).ToList();
        }

        return flex;
    }

    public static Priv ToProto(FlexPriv flex)
    {
        var proto = new Priv
        {
            Version = flex.Version ?? 0,
            Tag = flex.Tag ?? "",
            Uuid = flex.Uuid ?? "",
            Name = flex.Name ?? "",
            ExternalId = flex.ExternalId ?? "",
            Unid = flex.Unid ?? 0,
            Enabled = flex.Enabled ?? true,
            PrivType = (PrivType)(flex.PrivType ?? 0),
        };

        if (flex.Elements != null)
        {
            var dap = new DoorAccessPriv();
            foreach (var e in flex.Elements)
            {
                var elem = new DoorAccessPrivElement { Unid = e.Unid ?? 0 };
                if (e.Door?.Unid != null) elem.DoorUnid = e.Door.Unid.Value;
                if (e.SchedRestriction != null)
                    elem.SchedRestriction = ToProtoSchedRestriction(e.SchedRestriction);
                dap.Elements.Add(elem);
            }
            proto.ExtDoorAccessPriv = dap;
        }

        return proto;
    }

    // --- Sched ---

    public static FlexSched ToFlex(Sched proto, Func<int, string, FlexObjRef> lookupRef = null)
    {
        var flex = new FlexSched
        {
            Version = proto.Version,
            Tag = proto.Tag,
            Uuid = proto.Uuid,
            Name = proto.Name,
            Unid = proto.Unid,
            ExternalId = proto.ExternalId,
        };

        if (proto.Elements.Count > 0)
        {
            flex.Elements = proto.Elements.Select(e =>
            {
                var se = new FlexSchedElement
                {
                    Unid = e.Unid,
                    SchedDays = e.SchedDays.Select(d => (int)d).ToList(),
                    Holidays = e.Holidays,
                    Start = SqlTimeToString(e.Start),
                    Stop = SqlTimeToString(e.Stop),
                    PlusDays = e.PlusDays,
                };
                if (e.HolTypesUnid.Count > 0)
                    se.HolTypes = e.HolTypesUnid.Select(u => lookupRef?.Invoke(u, "holType") ?? MakeObjRef("holType", u)).ToList();
                return se;
            }).ToList();
        }

        return flex;
    }

    public static Sched ToProto(FlexSched flex)
    {
        var proto = new Sched
        {
            Version = flex.Version ?? 0,
            Tag = flex.Tag ?? "",
            Uuid = flex.Uuid ?? "",
            Name = flex.Name ?? "",
            Unid = flex.Unid ?? 0,
            ExternalId = flex.ExternalId ?? "",
        };

        if (flex.Elements != null)
        {
            foreach (var e in flex.Elements)
            {
                var se = new SchedElement
                {
                    Unid = e.Unid ?? 0,
                    Holidays = e.Holidays ?? false,
                    Start = StringToSqlTime(e.Start) ?? new SqlTimeData(),
                    Stop = StringToSqlTime(e.Stop) ?? new SqlTimeData(),
                    PlusDays = e.PlusDays ?? 0,
                };
                if (e.SchedDays != null)
                    foreach (var d in e.SchedDays)
                        se.SchedDays.Add((SchedDay)d);
                if (e.HolTypes != null)
                    foreach (var ht in e.HolTypes.Where(h => h.Unid.HasValue))
                        se.HolTypesUnid.Add(ht.Unid.Value);
                proto.Elements.Add(se);
            }
        }

        return proto;
    }

    // --- Hol ---

    public static FlexHol ToFlex(Hol proto, Func<int, string, FlexObjRef> lookupRef = null)
    {
        var flex = new FlexHol
        {
            Version = proto.Version,
            Tag = proto.Tag,
            Uuid = proto.Uuid,
            Name = proto.Name,
            Unid = proto.Unid,
            AllHolTypes = proto.AllHolTypes,
            Date = SqlDateToIso(proto.Date),
            NumDays = proto.NumDays,
            Repeat = proto.Repeat,
            NumYearsRepeat = proto.NumYearsRepeat,
            PreserveSchedDay = proto.PreserveSchedDay,
        };

        if (proto.HolCalUnid != 0)
            flex.HolCal = lookupRef?.Invoke(proto.HolCalUnid, "holCal") ?? MakeObjRef("holCal", proto.HolCalUnid);

        if (proto.HolTypesUnid.Count > 0)
            flex.HolTypes = proto.HolTypesUnid.Select(u => lookupRef?.Invoke(u, "holType") ?? MakeObjRef("holType", u)).ToList();

        return flex;
    }

    public static Hol ToProto(FlexHol flex)
    {
        var proto = new Hol
        {
            Version = flex.Version ?? 0,
            Tag = flex.Tag ?? "",
            Uuid = flex.Uuid ?? "",
            Name = flex.Name ?? "",
            Unid = flex.Unid ?? 0,
            AllHolTypes = flex.AllHolTypes ?? false,
            NumDays = flex.NumDays ?? 1,
            Repeat = flex.Repeat ?? false,
            NumYearsRepeat = flex.NumYearsRepeat ?? 0,
            PreserveSchedDay = flex.PreserveSchedDay ?? false,
        };

        if (flex.Date != null) proto.Date = IsoToSqlDate(flex.Date) ?? new SqlDateData();
        if (flex.HolCal?.Unid != null) proto.HolCalUnid = flex.HolCal.Unid.Value;
        if (flex.HolTypes != null)
            foreach (var ht in flex.HolTypes.Where(h => h.Unid.HasValue))
                proto.HolTypesUnid.Add(ht.Unid.Value);

        return proto;
    }

    // --- HolCal ---

    public static FlexHolCal ToFlex(HolCal proto)
    {
        return new FlexHolCal
        {
            Version = proto.Version,
            Tag = proto.Tag,
            Uuid = proto.Uuid,
            Name = proto.Name,
            Unid = proto.Unid,
        };
    }

    public static HolCal ToProto(FlexHolCal flex)
    {
        return new HolCal
        {
            Version = flex.Version ?? 0,
            Tag = flex.Tag ?? "",
            Uuid = flex.Uuid ?? "",
            Name = flex.Name ?? "",
            Unid = flex.Unid ?? 0,
        };
    }

    // --- HolType ---

    public static FlexHolType ToFlex(HolType proto)
    {
        return new FlexHolType
        {
            Version = proto.Version,
            Tag = proto.Tag,
            Uuid = proto.Uuid,
            Name = proto.Name,
            Unid = proto.Unid,
            ExternalId = proto.ExternalId,
        };
    }

    public static HolType ToProto(FlexHolType flex)
    {
        return new HolType
        {
            Version = flex.Version ?? 0,
            Tag = flex.Tag ?? "",
            Uuid = flex.Uuid ?? "",
            Name = flex.Name ?? "",
            Unid = flex.Unid ?? 0,
            ExternalId = flex.ExternalId ?? "",
        };
    }

    // --- DataFormat ---

    public static FlexDataFormat ToFlex(DataFormat proto)
    {
        var flex = new FlexDataFormat
        {
            Version = proto.Version,
            Tag = proto.Tag,
            Uuid = proto.Uuid,
            Name = proto.Name,
            Unid = proto.Unid,
            DataFormatType = (int)proto.DataFormatType,
        };

        if (proto.ExtensionCase == DataFormat.ExtensionOneofCase.ExtBinaryFormat)
        {
            var bf = proto.ExtBinaryFormat;
            flex.MinBits = bf.MinBits;
            flex.MaxBits = bf.MaxBits;
            flex.SupportReverseRead = bf.SupportReverseRead;
            if (bf.Elements.Count > 0)
                flex.Elements = bf.Elements.Select(ToFlexBinaryElement).ToList();
        }

        return flex;
    }

    public static DataFormat ToProto(FlexDataFormat flex)
    {
        var proto = new DataFormat
        {
            Version = flex.Version ?? 0,
            Tag = flex.Tag ?? "",
            Uuid = flex.Uuid ?? "",
            Name = flex.Name ?? "",
            Unid = flex.Unid ?? 0,
            DataFormatType = (DataFormatType)(flex.DataFormatType ?? 0),
        };

        if (flex.DataFormatType == 1) // BINARY
        {
            var bf = new BinaryFormat
            {
                MinBits = flex.MinBits ?? 0,
                MaxBits = flex.MaxBits ?? 0,
                SupportReverseRead = flex.SupportReverseRead ?? false,
            };
            if (flex.Elements != null)
                foreach (var e in flex.Elements)
                    bf.Elements.Add(ToProtoBinaryElement(e));
            proto.ExtBinaryFormat = bf;
        }

        return proto;
    }

    private static FlexBinaryElement ToFlexBinaryElement(BinaryElement e)
    {
        var flex = new FlexBinaryElement
        {
            Unid = e.Unid,
            Num = e.Num,
            Type = (int)e.Type,
            Start = e.Start,
            Len = e.Len,
        };

        switch (e.ExtensionCase)
        {
            case BinaryElement.ExtensionOneofCase.ExtFieldBinaryElement:
                flex.Field = (int)e.ExtFieldBinaryElement.Field;
                flex.StaticValue = BigIntegerDataToString(e.ExtFieldBinaryElement.StaticValue);
                break;
            case BinaryElement.ExtensionOneofCase.ExtParityBinaryElement:
                flex.Odd = e.ExtParityBinaryElement.Odd;
                flex.SrcStart = e.ExtParityBinaryElement.SrcStart;
                flex.SrcLen = e.ExtParityBinaryElement.SrcLen;
                flex.Mask = string.IsNullOrEmpty(e.ExtParityBinaryElement.Mask) ? null : e.ExtParityBinaryElement.Mask;
                break;
            case BinaryElement.ExtensionOneofCase.ExtStaticBinaryElement:
                flex.Value = BigIntegerDataToString(e.ExtStaticBinaryElement.Value);
                break;
        }

        return flex;
    }

    private static BinaryElement ToProtoBinaryElement(FlexBinaryElement flex)
    {
        var e = new BinaryElement
        {
            Unid = flex.Unid ?? 0,
            Num = flex.Num ?? 0,
            Type = (BinaryElementType)(flex.Type ?? 0),
            Start = flex.Start ?? 0,
            Len = flex.Len ?? 0,
        };

        switch (e.Type)
        {
            case BinaryElementType.Field:
                e.ExtFieldBinaryElement = new FieldBinaryElement
                {
                    Field = (DataFormatField)(flex.Field ?? 0),
                };
                if (flex.StaticValue != null)
                    e.ExtFieldBinaryElement.StaticValue = StringToBigIntegerData(flex.StaticValue);
                break;
            case BinaryElementType.Parity:
                e.ExtParityBinaryElement = new ParityBinaryElement
                {
                    Odd = flex.Odd ?? false,
                    SrcStart = flex.SrcStart ?? 0,
                    SrcLen = flex.SrcLen ?? 0,
                    Mask = flex.Mask ?? "",
                };
                break;
            case BinaryElementType.Static:
                e.ExtStaticBinaryElement = new StaticBinaryElement();
                if (flex.Value != null)
                    e.ExtStaticBinaryElement.Value = StringToBigIntegerData(flex.Value);
                break;
        }

        return e;
    }

    // --- DataLayout ---

    public static FlexDataLayout ToFlex(DataLayout proto, Func<int, string, FlexObjRef> lookupRef = null)
    {
        var flex = new FlexDataLayout
        {
            Version = proto.Version,
            Tag = proto.Tag,
            Uuid = proto.Uuid,
            Name = proto.Name,
            Unid = proto.Unid,
            LayoutType = (int)proto.LayoutType,
            Priority = proto.Priority,
            Enabled = proto.EnabledCase == DataLayout.EnabledOneofCase.Enabled ? proto.Enabled : null,
        };

        if (proto.ExtensionCase == DataLayout.ExtensionOneofCase.ExtBasicDataLayout)
        {
            var bdl = proto.ExtBasicDataLayout;
            if (bdl.DataFormatUnid != 0)
                flex.DataFormat = lookupRef?.Invoke(bdl.DataFormatUnid, "dataFormat") ?? MakeObjRef("dataFormat", bdl.DataFormatUnid);
        }

        return flex;
    }

    public static DataLayout ToProto(FlexDataLayout flex)
    {
        var proto = new DataLayout
        {
            Version = flex.Version ?? 0,
            Tag = flex.Tag ?? "",
            Uuid = flex.Uuid ?? "",
            Name = flex.Name ?? "",
            Unid = flex.Unid ?? 0,
            LayoutType = (DataLayoutType)(flex.LayoutType ?? 0),
            Priority = flex.Priority ?? 0,
        };
        if (flex.Enabled.HasValue) proto.Enabled = flex.Enabled.Value;

        if (flex.LayoutType == 0) // BASIC
        {
            var bdl = new BasicDataLayout();
            if (flex.DataFormat?.Unid != null)
                bdl.DataFormatUnid = flex.DataFormat.Unid.Value;
            proto.ExtBasicDataLayout = bdl;
        }

        return proto;
    }

    // --- DevStateRecord ---

    public static FlexDevStateRecord ToFlex(DevStateRecord proto)
    {
        var flex = new FlexDevStateRecord
        {
            Unid = proto.UnidCase == DevStateRecord.UnidOneofCase.Unid ? proto.Unid : null,
            Dev = MakeObjRef("dev", proto.DevUnid),
        };

        if (proto.DevState != null)
        {
            flex.DevState = new FlexDevState();
            foreach (var entry in proto.DevState.DevAspectStates)
            {
                var flexEntry = new FlexDevAspectStateEntry
                {
                    Key = entry.KeyCase == DevState.Types.DevAspect_DevAspectState_Entry.KeyOneofCase.Key ? (int)entry.Key : null,
                };
                if (entry.ValueCase == DevState.Types.DevAspect_DevAspectState_Entry.ValueOneofCase.Value && entry.Value != null)
                {
                    flexEntry.Value = ToFlexDevAspectState(entry.Value);
                }
                flex.DevState.DevAspectStates.Add(flexEntry);
            }
        }

        return flex;
    }

    private static FlexDevAspectState ToFlexDevAspectState(DevAspectState s)
    {
        var flex = new FlexDevAspectState
        {
            Unid = s.UnidCase == DevAspectState.UnidOneofCase.Unid ? s.Unid : null,
            DevAspect = (int)s.DevAspect,
            HwTime = DateTimeDataToIso(s.HwTime),
            DbTime = DateTimeDataToIso(s.DbTime),
        };

        if (s.CommStateCase == DevAspectState.CommStateOneofCase.CommState)
            flex.CommState = (int)s.CommState;
        if (s.CommStateStaleCase == DevAspectState.CommStateStaleOneofCase.CommStateStale)
            flex.CommStateStale = s.CommStateStale;
        if (s.ActivityStateCase == DevAspectState.ActivityStateOneofCase.ActivityState)
            flex.ActivityState = (int)s.ActivityState;
        if (s.ActivityStateStaleCase == DevAspectState.ActivityStateStaleOneofCase.ActivityStateStale)
            flex.ActivityStateStale = s.ActivityStateStale;
        if (s.DoorMode != null)
            flex.DoorMode = ToFlexDoorMode(s.DoorMode);
        if (s.DoorModeStaleCase == DevAspectState.DoorModeStaleOneofCase.DoorModeStale)
            flex.DoorModeStale = s.DoorModeStale;
        if (s.ExternalStateCase == DevAspectState.ExternalStateOneofCase.ExternalState)
            flex.ExternalState = s.ExternalState;
        if (s.ExternalStateStaleCase == DevAspectState.ExternalStateStaleOneofCase.ExternalStateStale)
            flex.ExternalStateStale = s.ExternalStateStale;

        return flex;
    }

    // --- EncryptionKey ---

    public static FlexEncryptionKey ToFlex(EncryptionKey proto)
    {
        return new FlexEncryptionKey
        {
            Unid = proto.Unid,
            Tag = proto.Tag,
            Uuid = proto.Uuid,
            Version = proto.Version,
            Algorithm = proto.Algorithm,
            Size = proto.Size,
            KeyIdentifier = proto.KeyIdentifier,
            Bytes = proto.BytesCase == EncryptionKey.BytesOneofCase.Bytes && !proto.Bytes.IsEmpty
                ? Convert.ToBase64String(proto.Bytes.ToByteArray())
                : null,
        };
    }

    public static EncryptionKey ToProto(FlexEncryptionKey flex)
    {
        var proto = new EncryptionKey
        {
            Unid = flex.Unid ?? 0,
            Tag = flex.Tag ?? "",
            Uuid = flex.Uuid ?? "",
            Version = flex.Version ?? 0,
            Algorithm = flex.Algorithm ?? "",
            Size = flex.Size ?? 0,
            KeyIdentifier = flex.KeyIdentifier ?? "",
        };

        if (flex.Bytes != null)
            proto.Bytes = ByteString.CopyFrom(Convert.FromBase64String(flex.Bytes));

        return proto;
    }
}
