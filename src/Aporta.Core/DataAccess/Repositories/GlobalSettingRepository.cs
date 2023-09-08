using System.Threading.Tasks;
using Aporta.Shared.Models;
using Dapper;

namespace Aporta.Core.DataAccess.Repositories;

public class GlobalSettingRepository
{
    private const string SqlSelect = @"select name, value
                                            from global_setting";

    private const string SqlInsert = @"insert into global_setting
                                            (name, value) values 
                                            (@name, @value);";

    private const string SqlUpdate = @"update global_setting
                                            set value = @value
                                            where name = @name;";
        
    private readonly IDataAccess _dataAccess;

    public GlobalSettingRepository(IDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }
        
    public async Task<string> Get(string name)
    {
        using var connection = _dataAccess.CreateDbConnection();
        connection.Open();

        var globalSetting = await connection.QueryFirstOrDefaultAsync<GlobalSetting>(SqlSelect +
            @" where name = @name", new {name});

        return globalSetting?.Value;
    }

    public async Task Insert(GlobalSetting globalSetting)
    {
        using var connection = _dataAccess.CreateDbConnection();
        connection.Open();

        await connection.ExecuteAsync(SqlInsert,
            new
            {
                name = globalSetting.Name, value = globalSetting.Value
            });
    }
        
    public async Task Update(GlobalSetting globalSetting)
    {
        using var connection = _dataAccess.CreateDbConnection();
        connection.Open();

        await connection.ExecuteAsync(SqlUpdate,
            new
            {
                name = globalSetting.Name, value = globalSetting.Value
            });
    }
}