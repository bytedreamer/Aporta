using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Z9.Spcore.Proto;

namespace Aporta.Core.DataAccess.Repositories;

public class Z9DevRepository : ProtoJsonRepository<Dev>
{
    private readonly IDataAccess _dataAccess;

    public Z9DevRepository(IDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    protected override IDataAccess DataAccess => _dataAccess;
    protected override string TableName => "z9_dev";
    protected override int GetUnid(Dev message) => message.Unid;

    public async Task<IEnumerable<Dev>> GetAllByDevType(DevType devType)
    {
        var all = await GetAll();
        return all.Where(d => d.DevType == devType);
    }

    public async Task<Dev> GetByExternalId(string externalId)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var json = await connection.QueryFirstOrDefaultAsync<string>(
            @"SELECT data FROM z9_dev
              WHERE json_extract(data, '$.externalId') = @externalId",
            new { externalId });

        return json == null ? null : Parser.Parse<Dev>(json);
    }

    public async Task<IEnumerable<Dev>> GetChildren(int parentUnid)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var results = await connection.QueryAsync<string>(
            @"SELECT data FROM z9_dev
              WHERE json_extract(data, '$.logicalParentUnid') = @parentUnid",
            new { parentUnid });

        return results.Select(json => Parser.Parse<Dev>(json));
    }

    public async Task<Dev> GetForDriverId(string driverId)
    {
        return await GetByExternalId(driverId);
    }

    public async Task<int> Insert(Dev dev)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var nextId = await connection.ExecuteScalarAsync<int>(
            "SELECT COALESCE(MAX(id), 0) + 1 FROM z9_dev");

        dev.Unid = nextId;
        var json = Formatter.Format(dev);

        await connection.ExecuteAsync(
            "INSERT INTO z9_dev (id, data) VALUES (@id, @data)",
            new { id = nextId, data = json });

        return nextId;
    }
}
