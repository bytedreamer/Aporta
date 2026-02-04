using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Aporta.Shared.Models;
using Dapper;

namespace Aporta.Core.DataAccess.Repositories;

public abstract class JsonDocumentRepository<T> where T : class
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    protected abstract IDataAccess DataAccess { get; }

    protected abstract string TableName { get; }

    protected abstract string SqlRowCount { get; }

    public async Task<T> Get(int id)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var row = await connection.QuerySingleOrDefaultAsync<(int Id, string Data)>(
            $"SELECT id, data FROM {TableName} WHERE id = @id", new { id });

        if (row.Data == null) return default;

        var entity = JsonSerializer.Deserialize<T>(row.Data, JsonOptions);
        SetId(entity, row.Id);
        return entity;
    }

    public async Task<IEnumerable<T>> GetAll()
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var results = await connection.QueryAsync<(int Id, string Data)>(
            $"SELECT id, data FROM {TableName}");

        return results.Select(r =>
        {
            var entity = JsonSerializer.Deserialize<T>(r.Data, JsonOptions);
            SetId(entity, r.Id);
            return entity;
        });
    }

    public async Task<PaginatedItemsDto<T>> GetAll(int pageNumber, int pageSize, string orderBy)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        int offset = (pageNumber - 1) * pageSize;
        var results = await connection.QueryAsync<(int Id, string Data)>(
            $"SELECT id, data FROM {TableName} ORDER BY {orderBy} DESC LIMIT @offset, @pageSize",
            new { offset, pageSize });

        var items = results.Select(r =>
        {
            var entity = JsonSerializer.Deserialize<T>(r.Data, JsonOptions);
            SetId(entity, r.Id);
            return entity;
        });

        return new PaginatedItemsDto<T>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = await connection.ExecuteScalarAsync<int>(SqlRowCount)
        };
    }

    public async Task<int> Insert(T record, int? explicitId = null)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var json = SerializeForInsert(record);
        int id;

        if (explicitId.HasValue && explicitId.Value > 0)
        {
            // Use provided ID
            id = explicitId.Value;
            await connection.ExecuteAsync(
                $"INSERT INTO {TableName} (id, data) VALUES (@id, @data)",
                new { id, data = json });
        }
        else
        {
            // Auto-assign: MAX(id) + 1, or 1 if table is empty
            id = await connection.QueryFirstAsync<int>(
                $"INSERT INTO {TableName} (id, data) VALUES ((SELECT COALESCE(MAX(id), 0) + 1 FROM {TableName}), @data); SELECT last_insert_rowid()",
                new { data = json });
        }

        SetId(record, id);
        return id;
    }

    public async Task Upsert(T record, int id)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var json = SerializeForInsert(record);
        await connection.ExecuteAsync(
            $"INSERT INTO {TableName} (id, data) VALUES (@id, @data) ON CONFLICT(id) DO UPDATE SET data = @data",
            new { id, data = json });

        SetId(record, id);
    }

    public async Task Update(T record)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var json = SerializeForUpdate(record);
        var id = GetId(record);
        await connection.ExecuteAsync(
            $"UPDATE {TableName} SET data = @data WHERE id = @id",
            new { data = json, id });
    }

    public async Task Delete(int id)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        await connection.ExecuteAsync(
            $"DELETE FROM {TableName} WHERE id = @id", new { id });
    }

    protected virtual string SerializeForInsert(T record)
    {
        return JsonSerializer.Serialize(record, JsonOptions);
    }

    protected virtual string SerializeForUpdate(T record)
    {
        return JsonSerializer.Serialize(record, JsonOptions);
    }

    protected abstract void SetId(T entity, int id);

    protected abstract int GetId(T entity);

    protected static JsonSerializerOptions GetJsonOptions() => JsonOptions;
}
