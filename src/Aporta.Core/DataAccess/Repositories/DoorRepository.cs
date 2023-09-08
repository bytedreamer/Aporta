using Aporta.Shared.Models;

namespace Aporta.Core.DataAccess.Repositories;

public class DoorRepository : BaseRepository<Door>
{
    public DoorRepository(IDataAccess dataAccess)
    {
        DataAccess = dataAccess;
    }
        
    protected override IDataAccess DataAccess { get; }

    protected override string SqlSelect => @"select door.id, 
                                                    door.in_access_endpoint_id as inAccessEndpointId, 
                                                    door.out_access_endpoint_id as outAccessEndpointId, 
                                                    door.door_contact_endpoint_id as doorContactEndpointId, 
                                                    door.request_to_exit_endpoint_id as requestToExitEndpointId, 
                                                    door.door_strike_endpoint_id as doorStrikeEndpointId,
                                                    door.name
                                                    from door";

    protected override string SqlInsert => @"insert into door
                                                (in_access_endpoint_id, out_access_endpoint_id, 
                                                 door_contact_endpoint_id, request_to_exit_endpoint_id, 
                                                 door_strike_endpoint_id, name) values 
                                                (@inAccessEndpointId, @outAccessEndpointId, @doorContactEndpointId, 
                                                 @requestToExitEndpointId, @doorStrikeEndpointId, @name)";

    protected override string SqlDelete => @"delete from door where id = @id";

    protected override object InsertParameters(Door door)
    {
        return new
        {
            inAccessEndpointId = door.InAccessEndpointId,
            outAccessEndpointId = door.OutAccessEndpointId,
            doorContactEndpointId = door.DoorContactEndpointId,
            requestToExitEndpointId = door.RequestToExitEndpointId,
            doorStrikeEndpointId = door.DoorStrikeEndpointId,
            name = door.Name
        };
    }

    protected override void InsertId(Door door, int id)
    {
        door.Id = id;
    }
}