using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Aporta.Core.Models;
using Dapper;

namespace Aporta.Core.DataAccess.Repositories;

public class ExtensionRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IDataAccess _dataAccess;

    public ExtensionRepository(IDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    public async Task<ExtensionHost> Get(Guid id)
    {
        using var connection = _dataAccess.CreateDbConnection();
        connection.Open();

        var data = await connection.QueryFirstOrDefaultAsync<string>(
            "SELECT data FROM extension WHERE id = @id",
            new { id = id.ToString() });

        if (data == null) return null;

        var extension = JsonSerializer.Deserialize<ExtensionHost>(data, JsonOptions);
        extension.Id = id;
        return extension;
    }

    public async Task<IEnumerable<ExtensionHost>> GetAll()
    {
        using var connection = _dataAccess.CreateDbConnection();
        connection.Open();

        var results = await connection.QueryAsync<(string Id, string Data)>(
            "SELECT id, data FROM extension");

        return results.Select(r =>
        {
            var extension = JsonSerializer.Deserialize<ExtensionHost>(r.Data, JsonOptions);
            extension.Id = Guid.Parse(r.Id);
            return extension;
        });
    }

    public async Task Insert(ExtensionHost extension)
    {
        using var connection = _dataAccess.CreateDbConnection();
        connection.Open();

        var json = JsonSerializer.Serialize(new
        {
            name = extension.Name,
            enabled = extension.Enabled,
            configuration = extension.Configuration
        }, JsonOptions);

        await connection.ExecuteAsync(
            "INSERT INTO extension (id, data) VALUES (@id, @data)",
            new { id = extension.Id.ToString(), data = json });
    }

    public async Task Update(ExtensionHost extension)
    {
        using var connection = _dataAccess.CreateDbConnection();
        connection.Open();

        var json = JsonSerializer.Serialize(new
        {
            name = extension.Name,
            enabled = extension.Enabled,
            configuration = extension.Configuration
        }, JsonOptions);

        await connection.ExecuteAsync(
            "UPDATE extension SET data = @data WHERE id = @id",
            new { id = extension.Id.ToString(), data = json });
    }
}
