using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aporta.Shared.Models;
using Dapper;

namespace Aporta.Core.DataAccess.Repositories;

/// <summary>
/// Read-only query helper that projects z9_cred rows into Credential/Person DTOs.
/// No longer owns a table — all data lives in z9_cred.
/// </summary>
public class CredentialRepository
{
    private readonly IDataAccess _dataAccess;

    public CredentialRepository(IDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    public async Task<Credential> Get(int id)
    {
        using var connection = _dataAccess.CreateDbConnection();
        connection.Open();

        var result = await connection.QuerySingleOrDefaultAsync<(int Id, string CredNum, string CredEnabled)>(
            @"SELECT id, cred_num,
                json_extract(data, '$.enabled') as CredEnabled
              FROM z9_cred WHERE id = @id",
            new { id });

        if (result.Id == 0 && result.CredNum == null && result.CredEnabled == null) return null;

        return new Credential
        {
            Id = result.Id,
            Number = result.CredNum,
            Enabled = ParseBool(result.CredEnabled)
        };
    }

    public async Task<IEnumerable<Credential>> GetAll()
    {
        using var connection = _dataAccess.CreateDbConnection();
        connection.Open();

        var results = await connection.QueryAsync<(int Id, string CredNum, string CredEnabled)>(
            @"SELECT id, cred_num,
                json_extract(data, '$.enabled') as CredEnabled
              FROM z9_cred");

        return results.Select(r => new Credential
        {
            Id = r.Id,
            Number = r.CredNum,
            Enabled = ParseBool(r.CredEnabled)
        });
    }

    public async Task<AssignedCredential> AssignedCredential(string cardNumber)
    {
        using var connection = _dataAccess.CreateDbConnection();
        connection.Open();

        var result = await connection.QuerySingleOrDefaultAsync<(int Id, string CredNum, string CredName, string CredEnabled)>(
            @"SELECT id, cred_num,
                json_extract(data, '$.name') as CredName,
                json_extract(data, '$.enabled') as CredEnabled
              FROM z9_cred
              WHERE cred_num = @number",
            new { number = cardNumber });

        if (result.Id == 0 && result.CredNum == null)
        {
            return null;
        }

        var credential = new AssignedCredential
        {
            Id = result.Id,
            Number = result.CredNum
        };

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
        using var connection = _dataAccess.CreateDbConnection();
        connection.Open();

        var results = await connection.QueryAsync<(int Id, string CredNum, string CredEnabled)>(
            @"SELECT id, cred_num,
                json_extract(data, '$.enabled') as CredEnabled
              FROM z9_cred
              WHERE json_extract(data, '$.name') IS NOT NULL
                AND cred_num IS NOT NULL
                AND cred_num != ''");

        return results.Select(r => new Credential
        {
            Id = r.Id,
            Number = r.CredNum,
            Enabled = ParseBool(r.CredEnabled)
        });
    }

    public async Task<IEnumerable<Credential>> Unassigned()
    {
        using var connection = _dataAccess.CreateDbConnection();
        connection.Open();

        var results = await connection.QueryAsync<(int Id, string CredNum)>(
            @"SELECT id, cred_num FROM z9_cred
              WHERE json_extract(data, '$.name') IS NULL
                AND cred_num IS NOT NULL");

        return results.Select(r => new Credential
        {
            Id = r.Id,
            Number = r.CredNum
        });
    }

    public async Task<IEnumerable<Person>> Named()
    {
        using var connection = _dataAccess.CreateDbConnection();
        connection.Open();

        var results = await connection.QueryAsync<(int Id, string CredName, string CredEnabled)>(
            @"SELECT id,
                json_extract(data, '$.name') as CredName,
                json_extract(data, '$.enabled') as CredEnabled
              FROM z9_cred
              WHERE json_extract(data, '$.name') IS NOT NULL");

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
