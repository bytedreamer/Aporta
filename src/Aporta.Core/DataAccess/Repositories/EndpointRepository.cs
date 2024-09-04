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

    protected override string SqlUpdate => @"update endpoint
                                            set name = @name
                                            where driver_id = @driverEndpointId and extension_id = @extensionId";


    protected override string SqlDelete => @"delete from endpoint
                                            where id = @id";
    
    protected override string SqlRowCount => @"select count(*) from endpoint";

    public async Task<IEnumerable<Endpoint>> GetForExtension(Guid extensionId)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        return await connection.QueryAsync<Endpoint>(SqlSelect + @" where extension_id = @extensionId",
            new {extensionId});
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    protected override object UpdateParameters(Endpoint endpoint)
    {
        return new
        {
            name = endpoint.Name,
            driverEndpointId = endpoint.DriverEndpointId,
            extensionId = endpoint.ExtensionId
        };
    }

    /// <inheritdoc/>
    protected override void InsertId(Endpoint endpoint, int id)
    {
        endpoint.Id = id;
    }
}