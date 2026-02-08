using System;
using System.Linq;
using System.Threading.Tasks;

using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Shared.Models;
using Z9.Spcore.Proto;

namespace Aporta.Core.Services;

public class EventService
{
    private readonly Z9EvtRepository _z9EvtRepository;

    public EventService(IDataAccess dataAccess)
    {
        _z9EvtRepository = new Z9EvtRepository(dataAccess);
    }

    public async Task<Event> Get(int eventId)
    {
        var evt = await _z9EvtRepository.Get(eventId);
        return evt != null ? EvtToEvent(evt) : null;
    }

    public async Task<PaginatedItemsDto<Event>> GetAll(int pageNumber, int pageSize)
    {
        var page = await _z9EvtRepository.GetAll(pageNumber, pageSize);
        return new PaginatedItemsDto<Event>
        {
            Items = page.Items.Select(EvtToEvent),
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalItems = page.TotalItems
        };
    }

    private static Event EvtToEvent(Evt evt)
    {
        var (eventType, _) = EventReasonMapping.ToEventTypeAndReason(
            evt.EvtCode,
            evt.EvtSubCodeCase == Evt.EvtSubCodeOneofCase.EvtSubCode ? evt.EvtSubCode : 0);

        return new Event
        {
            Id = (int)evt.Unid,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(evt.DbTime.Millis).UtcDateTime,
            Type = eventType,
            Data = evt.Data
        };
    }
}
