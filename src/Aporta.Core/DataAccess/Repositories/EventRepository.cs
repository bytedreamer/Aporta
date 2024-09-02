using System;
using Aporta.Shared.Models;

namespace Aporta.Core.DataAccess.Repositories;

public class EventRepository : BaseRepository<Event>
{
    public EventRepository(IDataAccess dataAccess)
    {
        DataAccess = dataAccess;
    }
    
    protected override IDataAccess DataAccess { get; }
    
    protected override string SqlSelect => @"select event.id,
                                                    event.endpoint_id as endpointId,
                                                    event.timestamp,
                                                    event.event_type as type,
                                                    event.data
                                                    from event";
        
    protected override string SqlInsert => @"insert into event
                                                (endpoint_id, timestamp, event_type, data) values 
                                                (@endpointId, @timestamp, @type, @data)";

    protected override string SqlUpdate => throw new NotImplementedException();

    protected override string SqlDelete => @"delete from event where id = @id";

    protected override string SqlRowCount => @"select count(*) from event";
        
    protected override object InsertParameters(Event @event)
    {
        return new
        {
            endpointId = @event.EndpointId,
            timestamp = @event.Timestamp,
            type = @event.Type,
            data = @event.Data
        };
    }

    protected override object UpdateParameters(Event record)
    {
        throw new System.NotImplementedException();
    }

    protected override void InsertId(Event @event, int id)
    {
        @event.Id = id;
    }
}