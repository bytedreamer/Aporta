using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Google.Protobuf;

namespace Aporta.Core.DataAccess.Repositories;

/// <summary>
/// Base repository for storing Google.Protobuf IMessage types as JSON documents.
/// Uses Google.Protobuf.JsonFormatter/JsonParser for serialization.
/// </summary>
public abstract class ProtoJsonRepository<T> where T : IMessage<T>, new()
{
    protected static readonly JsonFormatter Formatter = new(JsonFormatter.Settings.Default);
    protected static readonly JsonParser Parser = new(JsonParser.Settings.Default);

    protected abstract IDataAccess DataAccess { get; }
    protected abstract string TableName { get; }

    /// <summary>
    /// Gets the unid field value from the protobuf message (used as primary key).
    /// </summary>
    protected abstract int GetUnid(T message);

    public async Task<T> Get(int id)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var json = await connection.QuerySingleOrDefaultAsync<string>(
            $"SELECT data FROM {TableName} WHERE id = @id", new { id });

        return json == null ? default : Parser.Parse<T>(json);
    }

    public async Task<IEnumerable<T>> GetAll()
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var results = await connection.QueryAsync<string>(
            $"SELECT data FROM {TableName}");

        return results.Select(json => Parser.Parse<T>(json));
    }

    public async Task Upsert(T message)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var id = GetUnid(message);
        var json = Formatter.Format(message);

        await connection.ExecuteAsync(
            $"INSERT INTO {TableName} (id, data) VALUES (@id, @data) ON CONFLICT(id) DO UPDATE SET data = @data",
            new { id, data = json });
    }

    public async Task Delete(int id)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        await connection.ExecuteAsync(
            $"DELETE FROM {TableName} WHERE id = @id", new { id });
    }

    public async Task DeleteAll()
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        await connection.ExecuteAsync($"DELETE FROM {TableName}");
    }

    public async Task<int> Count()
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        return await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {TableName}");
    }

    /// <summary>
    /// Returns the next available ID (MAX(id) + 1, or 1 if empty).
    /// </summary>
    public async Task<int> NextId()
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        return await connection.ExecuteScalarAsync<int>(
            $"SELECT COALESCE(MAX(id), 0) + 1 FROM {TableName}");
    }
}
