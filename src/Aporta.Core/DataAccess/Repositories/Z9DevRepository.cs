using System;
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

    /// <summary>
    /// Gets the IO_CONTROLLER_EXTERNAL z9_dev for the given extension (driver) GUID.
    /// Uses the denormalized external_dev_mod_id column.
    /// </summary>
    public async Task<Dev> GetController(Guid extensionId)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var json = await connection.QueryFirstOrDefaultAsync<string>(
            @"SELECT data FROM z9_dev
              WHERE external_dev_mod_id = @extensionId",
            new { extensionId = extensionId.ToString() });

        return json == null ? null : Parser.Parse<Dev>(json);
    }

    /// <summary>
    /// Gets all z9_devs whose physicalParentUnid matches the given parent.
    /// </summary>
    public async Task<IEnumerable<Dev>> GetPhysicalChildren(int parentUnid)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var results = await connection.QueryAsync<string>(
            @"SELECT data FROM z9_dev
              WHERE json_extract(data, '$.physicalParentUnid') = @parentUnid",
            new { parentUnid });

        return results.Select(json => Parser.Parse<Dev>(json));
    }

    /// <summary>
    /// Gets available (pool) z9_devs matching the given devType.
    /// Available = DevPlatform is External (auto-synced from driver, not yet assigned by user).
    /// </summary>
    public async Task<IEnumerable<Dev>> GetAvailableByDevType(DevType devType)
    {
        var all = await GetAll();
        return all.Where(d =>
            d.DevType == devType &&
            d.DevPlatformCase == Dev.DevPlatformOneofCase.DevPlatform &&
            d.DevPlatform == DevPlatform.External);
    }

    /// <summary>
    /// Gets the extension (driver) GUID for any z9_dev by walking physicalParentUnid
    /// to its IO_CONTROLLER_EXTERNAL parent.
    /// </summary>
    public async Task<Guid?> GetExtensionId(Dev dev)
    {
        // If this dev IS the controller, return its ExternalDevModId directly
        if (dev.DevMod == DevMod.IoControllerExternal &&
            !string.IsNullOrEmpty(dev.ExternalDevModId))
        {
            return Guid.TryParse(dev.ExternalDevModId, out var id) ? id : null;
        }

        // Walk physicalParentUnid to find the controller
        if (dev.PhysicalParentUnidCase != Dev.PhysicalParentUnidOneofCase.PhysicalParentUnid)
            return null;

        var controller = await Get(dev.PhysicalParentUnid);
        if (controller == null)
            return null;

        return Guid.TryParse(controller.ExternalDevModId, out var controllerId) ? controllerId : null;
    }

    public async Task<int> Insert(Dev dev)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var nextId = await connection.ExecuteScalarAsync<int>(
            "SELECT COALESCE(MAX(id), 0) + 1 FROM z9_dev");

        dev.Unid = nextId;
        var json = Formatter.Format(dev);
        var externalDevModId = string.IsNullOrEmpty(dev.ExternalDevModId) ? null : dev.ExternalDevModId;

        await connection.ExecuteAsync(
            "INSERT INTO z9_dev (id, data, external_dev_mod_id) VALUES (@id, @data, @externalDevModId)",
            new { id = nextId, data = json, externalDevModId });

        return nextId;
    }

    public new async Task Upsert(Dev message)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var id = GetUnid(message);
        var json = Formatter.Format(message);
        var externalDevModId = string.IsNullOrEmpty(message.ExternalDevModId) ? null : message.ExternalDevModId;

        await connection.ExecuteAsync(
            @"INSERT INTO z9_dev (id, data, external_dev_mod_id) VALUES (@id, @data, @externalDevModId)
              ON CONFLICT(id) DO UPDATE SET data = @data, external_dev_mod_id = @externalDevModId",
            new { id, data = json, externalDevModId });
    }
}
