using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aporta.Shared.Models;
using Dapper;

namespace Aporta.Core.DataAccess.Repositories;

public class EndpointRepository : BaseRepository<Endpoint>
{
    public EndpointRepository(IDataAccess dataAccess)
    {
        DataAccess = dataAccess;

        SqlMapper.AddTypeHandler(new GuidHandler());
    }
        
    protected override IDataAccess DataAccess { get; }
        
    protected override string SqlSelect => @"select id, name, endpoint_type as type, driver_id as driverEndpointId, extension_id as extensionId
                                            from endpoint";
        
    protected override string SqlInsert => @"insert into endpoint
                                            (name, endpoint_type, driver_id, extension_id) values 
                                            (@name, @endpointType, @driverEndpointId, @extensionId)";

        
    protected override string SqlDelete => @"delete from endpoint
                                            where id = @id";
        
    public async Task<IEnumerable<Endpoint>> GetForExtension(Guid extensionId)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        return await connection.QueryAsync<Endpoint>(SqlSelect + @" where extension_id = @extensionId",
            new {extensionId});
    }

    protected override object InsertParameters(Endpoint endpoint)
    {
        return new
        {
            name = endpoint.Name, 
            endpointType = endpoint.Type,
            driverEndpointId = endpoint.DriverEndpointId, 
            extensionId = endpoint.ExtensionId
        };
    }

    protected override void InsertId(Endpoint endpoint, int id)
    {
        endpoint.Id = id;
    }
}