using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aporta.Shared.Models;
using Dapper;

namespace Aporta.Core.DataAccess.Repositories;

public class EndpointRepository : JsonDocumentRepository<Endpoint>
{
    public EndpointRepository(IDataAccess dataAccess)
    {
        DataAccess = dataAccess;
    }

    protected override IDataAccess DataAccess { get; }

    protected override string TableName => "endpoint";

    protected override string SqlRowCount => "SELECT COUNT(*) FROM endpoint";

    public async Task<IEnumerable<Endpoint>> GetForExtension(Guid extensionId)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var results = await connection.QueryAsync<(int Id, string Data)>(
            @"SELECT id, data FROM endpoint
              WHERE json_extract(data, '$.extensionId') = @extensionId",
            new { extensionId = extensionId.ToString() });

        return results.Select(r =>
        {
            var endpoint = JsonSerializer.Deserialize<Endpoint>(r.Data, GetJsonOptions());
            endpoint.Id = r.Id;
            return endpoint;
        });
    }

    public new async Task Update(Endpoint endpoint)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var json = JsonSerializer.Serialize(endpoint, GetJsonOptions());
        await connection.ExecuteAsync(
            @"UPDATE endpoint SET data = @data
              WHERE json_extract(data, '$.driverEndpointId') = @driverEndpointId
              AND json_extract(data, '$.extensionId') = @extensionId",
            new
            {
                data = json,
                driverEndpointId = endpoint.DriverEndpointId,
                extensionId = endpoint.ExtensionId.ToString()
            });
    }

    protected override void SetId(Endpoint entity, int id)
    {
        entity.Id = id;
    }

    protected override int GetId(Endpoint entity)
    {
        return entity.Id;
    }
}
