using System.Text.Json;
using System.Threading.Tasks;
using Aporta.Shared.Models;
using Dapper;

namespace Aporta.Core.DataAccess.Repositories;

public class OutputRepository : JsonDocumentRepository<Output>
{
    public OutputRepository(IDataAccess dataAccess)
    {
        DataAccess = dataAccess;
    }

    protected override IDataAccess DataAccess { get; }

    protected override string TableName => "output";

    protected override string SqlRowCount => "SELECT COUNT(*) FROM output";

    public async Task<Output> GetForDriverId(string driverId)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var result = await connection.QueryFirstOrDefaultAsync<(int Id, string Data)>(
            @"SELECT o.id, o.data FROM output o
              INNER JOIN endpoint e ON json_extract(o.data, '$.endpointId') = e.id
              WHERE json_extract(e.data, '$.driverEndpointId') = @driverId",
            new { driverId });

        if (result.Data == null) return null;

        var output = JsonSerializer.Deserialize<Output>(result.Data, GetJsonOptions());
        output.Id = result.Id;
        return output;
    }

    protected override void SetId(Output entity, int id)
    {
        entity.Id = id;
    }

    protected override int GetId(Output entity)
    {
        return entity.Id;
    }
}
