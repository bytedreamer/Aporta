using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aporta.Shared.Models;
using Dapper;

namespace Aporta.Core.DataAccess.Repositories;

public class CredentialRepository : JsonDocumentRepository<Credential>
{
    public CredentialRepository(IDataAccess dataAccess)
    {
        DataAccess = dataAccess;
    }

    protected override IDataAccess DataAccess { get; }

    protected override string TableName => "credential";

    protected override string SqlRowCount => "SELECT COUNT(*) FROM credential";

    protected override void SetId(Credential entity, int id)
    {
        entity.Id = id;
    }

    protected override int GetId(Credential entity)
    {
        return entity.Id;
    }

    public new async Task<Credential> Get(int id)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var result = await connection.QuerySingleOrDefaultAsync<(int Id, string Data, string CredEnabled)>(
            @"SELECT c.id, c.data,
                json_extract(z.data, '$.enabled') as CredEnabled
              FROM credential c
              LEFT JOIN z9_cred z ON z.id = c.id
              WHERE c.id = @id",
            new { id });

        if (result.Data == null) return null;

        var credential = JsonSerializer.Deserialize<Credential>(result.Data, GetJsonOptions());
        credential.Id = result.Id;
        credential.Enabled = ParseBool(result.CredEnabled);
        return credential;
    }

    public new async Task<IEnumerable<Credential>> GetAll()
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var results = await connection.QueryAsync<(int Id, string Data, string CredEnabled)>(
            @"SELECT c.id, c.data,
                json_extract(z.data, '$.enabled') as CredEnabled
              FROM credential c
              LEFT JOIN z9_cred z ON z.id = c.id");

        return results.Select(r =>
        {
            var credential = JsonSerializer.Deserialize<Credential>(r.Data, GetJsonOptions());
            credential.Id = r.Id;
            credential.Enabled = ParseBool(r.CredEnabled);
            return credential;
        });
    }

    public async Task<AssignedCredential> AssignedCredential(string cardNumber)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var result = await connection.QuerySingleOrDefaultAsync<(int Id, string Data, string CredName, string CredEnabled)>(
            @"SELECT c.id, c.data,
                json_extract(z.data, '$.name') as CredName,
                json_extract(z.data, '$.enabled') as CredEnabled
              FROM credential c
              LEFT JOIN z9_cred z ON z.id = c.id
              WHERE json_extract(c.data, '$.number') = @number",
            new { number = cardNumber });

        if (result.Data == null)
        {
            return null;
        }

        var credential = JsonSerializer.Deserialize<AssignedCredential>(result.Data, GetJsonOptions());
        credential.Id = result.Id;

        if (!string.IsNullOrWhiteSpace(result.CredName))
        {
            var enabled = ParseBool(result.CredEnabled) ?? true;
            credential.Person = ParsePersonFromName(result.CredName, credential.Id, enabled);
            credential.Enabled = enabled;
        }
        else
        {
            credential.Enabled = false;
        }

        return credential;
    }

    public async Task<IEnumerable<Credential>> Assigned()
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var results = await connection.QueryAsync<(int Id, string Data, string CredEnabled)>(
            @"SELECT c.id, c.data,
                json_extract(z.data, '$.enabled') as CredEnabled
              FROM credential c
              INNER JOIN z9_cred z ON z.id = c.id
              WHERE json_extract(z.data, '$.name') IS NOT NULL
                AND json_extract(c.data, '$.number') IS NOT NULL
                AND json_extract(c.data, '$.number') != ''");

        return results.Select(r =>
        {
            var credential = JsonSerializer.Deserialize<Credential>(r.Data, GetJsonOptions());
            credential.Id = r.Id;
            credential.Enabled = ParseBool(r.CredEnabled);
            return credential;
        });
    }

    public async Task<IEnumerable<Credential>> Unassigned()
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var results = await connection.QueryAsync<(int Id, string Data)>(
            @"SELECT c.id, c.data FROM credential c
              LEFT JOIN z9_cred z ON z.id = c.id
              WHERE json_extract(z.data, '$.name') IS NULL");

        return results.Select(r =>
        {
            var credential = JsonSerializer.Deserialize<Credential>(r.Data, GetJsonOptions());
            credential.Id = r.Id;
            return credential;
        });
    }

    public async Task<IEnumerable<Person>> Named()
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var results = await connection.QueryAsync<(int Id, string CredName, string CredEnabled)>(
            @"SELECT c.id,
                json_extract(z.data, '$.name') as CredName,
                json_extract(z.data, '$.enabled') as CredEnabled
              FROM credential c
              INNER JOIN z9_cred z ON z.id = c.id
              WHERE json_extract(z.data, '$.name') IS NOT NULL");

        return results.Select(r =>
        {
            var enabled = ParseBool(r.CredEnabled) ?? true;
            return ParsePersonFromName(r.CredName, r.Id, enabled);
        });
    }

    public static Person ParsePersonFromName(string credName, int id, bool enabled)
    {
        var person = new Person { Id = id, Enabled = enabled };
        if (credName == null)
            return person;

        var commaIndex = credName.IndexOf(", ", StringComparison.Ordinal);
        if (commaIndex >= 0)
        {
            person.LastName = credName.Substring(0, commaIndex);
            person.FirstName = credName.Substring(commaIndex + 2);
        }
        else
        {
            person.FirstName = credName;
        }
        return person;
    }

    private static bool? ParseBool(string value)
    {
        if (value == null) return null;
        // SQLite json_extract returns "true"/"false" or "1"/"0"
        if (bool.TryParse(value, out var b)) return b;
        if (int.TryParse(value, out var i)) return i > 0;
        return null;
    }
}
