using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Aporta.Shared.Models;
using Dapper;

namespace Aporta.Core.DataAccess.Repositories;

public class GlobalSettingRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IDataAccess _dataAccess;

    public GlobalSettingRepository(IDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    public async Task<string> Get(string name)
    {
        using var connection = _dataAccess.CreateDbConnection();
        connection.Open();

        var data = await connection.QueryFirstOrDefaultAsync<string>(
            "SELECT data FROM global_setting WHERE name = @name",
            new { name });

        if (data == null) return null;

        var setting = JsonSerializer.Deserialize<GlobalSetting>(data, JsonOptions);
        return setting?.Value;
    }

    public async Task Insert(GlobalSetting globalSetting)
    {
        using var connection = _dataAccess.CreateDbConnection();
        connection.Open();

        var json = JsonSerializer.Serialize(new { value = globalSetting.Value }, JsonOptions);

        await connection.ExecuteAsync(
            "INSERT INTO global_setting (name, data) VALUES (@name, @data)",
            new { name = globalSetting.Name, data = json });
    }

    public async Task Update(GlobalSetting globalSetting)
    {
        using var connection = _dataAccess.CreateDbConnection();
        connection.Open();

        var json = JsonSerializer.Serialize(new { value = globalSetting.Value }, JsonOptions);

        await connection.ExecuteAsync(
            "UPDATE global_setting SET data = @data WHERE name = @name",
            new { name = globalSetting.Name, data = json });
    }
}
