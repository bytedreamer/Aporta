using System;
using System.Threading.Tasks;
using Aporta.Shared.Models;
using Dapper;

namespace Aporta.Core.DataAccess.Repositories;

public class OutputRepository : BaseRepository<Output>
{
    public OutputRepository(IDataAccess dataAccess)
    {
        DataAccess = dataAccess;
    }
        
    protected override IDataAccess DataAccess { get; }

    protected override string SqlSelect => @"select output.id, output.endpoint_id as endpointId, output.name 
                                                from output";

    protected override string SqlInsert => @"insert into output
                                                (endpoint_id, name) values 
                                                (@endpointId, @name)";

    protected override string SqlUpdate => throw new NotImplementedException();

    protected override string SqlDelete => @"delete from output where id = @id";
    
    protected override string SqlRowCount => @"select count(*) from output";
        
    public async Task<Output> GetForDriverId(string driverId)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        return await connection.QueryFirstAsync<Output>(SqlSelect + 
                                                        @" inner join endpoint on output.endpoint_id = endpoint.id 
                                                            where endpoint.driver_id = @driverId",
            new {driverId});
    }
        
    protected override object InsertParameters(Output output)
    {
        return new
        {
            endpointId = output.EndpointId,
            name = output.Name
        };
    }

    protected override object UpdateParameters(Output record)
    {
        throw new System.NotImplementedException();
    }

    protected override void InsertId(Output output, int id)
    {
        output.Id = id;
    }
}