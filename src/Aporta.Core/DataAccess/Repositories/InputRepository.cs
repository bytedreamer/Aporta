using System.Text.Json;
using System.Threading.Tasks;
using Aporta.Shared.Models;
using Dapper;

namespace Aporta.Core.DataAccess.Repositories;

public class InputRepository : JsonDocumentRepository<Input>
{
    public InputRepository(IDataAccess dataAccess)
    {
        DataAccess = dataAccess;
    }

    protected override IDataAccess DataAccess { get; }

    protected override string TableName => "input";

    protected override string SqlRowCount => "SELECT COUNT(*) FROM input";

    public async Task<Input> GetForDriverId(string driverId)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var result = await connection.QueryFirstOrDefaultAsync<(int Id, string Data)>(
            @"SELECT i.id, i.data FROM input i
              INNER JOIN endpoint e ON json_extract(i.data, '$.endpointId') = e.id
              WHERE json_extract(e.data, '$.driverEndpointId') = @driverId",
            new { driverId });

        if (result.Data == null) return null;

        var input = JsonSerializer.Deserialize<Input>(result.Data, GetJsonOptions());
        input.Id = result.Id;
        return input;
    }

    protected override void SetId(Input entity, int id)
    {
        entity.Id = id;
    }

    protected override int GetId(Input entity)
    {
        return entity.Id;
    }
}
