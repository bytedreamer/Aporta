using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Z9.Spcore.Proto;

namespace Aporta.Core.Services;

public class DevAspectStateChangedEventArgs : EventArgs
{
    public int DevUnid { get; set; }
    public DevAspect Aspect { get; set; }
    public DevAspectState State { get; set; }
}

public class DevStateService
{
    private readonly ConcurrentDictionary<int, DevStateRecord> _stateRecords = new();

    public event EventHandler<DevAspectStateChangedEventArgs> DevAspectStateChanged;

    public void UpdateAspect(int devUnid, DevAspect aspect, Action<DevAspectState> mutator)
    {
        var record = _stateRecords.GetOrAdd(devUnid, _ =>
        {
            var r = new DevStateRecord { DevUnid = devUnid, Unid = devUnid };
            r.DevState = new DevState();
            return r;
        });

        lock (record)
        {
            if (record.DevState == null)
                record.DevState = new DevState();

            var entry = record.DevState.DevAspectStates
                .FirstOrDefault(e => e.KeyCase == DevState.Types.DevAspect_DevAspectState_Entry.KeyOneofCase.Key && e.Key == aspect);

            if (entry == null)
            {
                entry = new DevState.Types.DevAspect_DevAspectState_Entry { Key = aspect };
                entry.Value = new DevAspectState { DevAspect = aspect };
                record.DevState.DevAspectStates.Add(entry);
            }

            if (entry.Value == null)
                entry.Value = new DevAspectState { DevAspect = aspect };

            entry.Value.DbTime = new DateTimeData { Millis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };

            mutator(entry.Value);

            DevAspectStateChanged?.Invoke(this, new DevAspectStateChangedEventArgs
            {
                DevUnid = devUnid,
                Aspect = aspect,
                State = entry.Value,
            });
        }
    }

    public DevStateRecord GetDevStateRecord(int devUnid)
    {
        if (!_stateRecords.TryGetValue(devUnid, out var record))
            return null;
        lock (record)
        {
            return record.Clone();
        }
    }

    public List<DevStateRecord> GetAllDevStateRecords()
    {
        var result = new List<DevStateRecord>();
        foreach (var record in _stateRecords.Values)
        {
            lock (record)
            {
                result.Add(record.Clone());
            }
        }
        return result;
    }

    public void RemoveDevStateRecord(int devUnid)
    {
        _stateRecords.TryRemove(devUnid, out _);
    }
}
