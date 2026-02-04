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

        var result = await connection.QuerySingleOrDefaultAsync<(int Id, string Data, int? AssignedPersonId, int? Enabled)>(
            @"SELECT c.id, c.data,
                (SELECT json_extract(ca.data, '$.personId') FROM credential_assignment ca
                 WHERE json_extract(ca.data, '$.credentialId') = c.id) as AssignedPersonId,
                (SELECT json_extract(ca.data, '$.enabled') FROM credential_assignment ca
                 WHERE json_extract(ca.data, '$.credentialId') = c.id) as Enabled
              FROM credential c WHERE c.id = @id",
            new { id });

        if (result.Data == null) return null;

        var credential = JsonSerializer.Deserialize<Credential>(result.Data, GetJsonOptions());
        credential.Id = result.Id;
        credential.AssignedPersonId = result.AssignedPersonId;
        credential.Enabled = result.Enabled.HasValue ? result.Enabled > 0 : (bool?)null;
        return credential;
    }

    public new async Task<IEnumerable<Credential>> GetAll()
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var results = await connection.QueryAsync<(int Id, string Data, int? AssignedPersonId, int? Enabled)>(
            @"SELECT c.id, c.data,
                (SELECT json_extract(ca.data, '$.personId') FROM credential_assignment ca
                 WHERE json_extract(ca.data, '$.credentialId') = c.id) as AssignedPersonId,
                (SELECT json_extract(ca.data, '$.enabled') FROM credential_assignment ca
                 WHERE json_extract(ca.data, '$.credentialId') = c.id) as Enabled
              FROM credential c");

        return results.Select(r =>
        {
            var credential = JsonSerializer.Deserialize<Credential>(r.Data, GetJsonOptions());
            credential.Id = r.Id;
            credential.AssignedPersonId = r.AssignedPersonId;
            credential.Enabled = r.Enabled.HasValue ? r.Enabled > 0 : (bool?)null;
            return credential;
        });
    }

    public async Task<AssignedCredential> AssignedCredential(string cardNumber)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var result = await connection.QuerySingleOrDefaultAsync<(int Id, string Data)>(
            @"SELECT id, data FROM credential
              WHERE json_extract(data, '$.number') = @number",
            new { number = cardNumber });

        if (result.Data == null)
        {
            return null;
        }

        var credential = JsonSerializer.Deserialize<AssignedCredential>(result.Data, GetJsonOptions());
        credential.Id = result.Id;

        var personAssignment = await connection.QuerySingleOrDefaultAsync<(int PersonId, int Enabled)>(
            @"SELECT json_extract(data, '$.personId') as PersonId,
                     json_extract(data, '$.enabled') as Enabled
              FROM credential_assignment
              WHERE json_extract(data, '$.credentialId') = @credentialId",
            new { credentialId = credential.Id });

        if (personAssignment.PersonId > 0)
        {
            var personRepository = new PersonRepository(DataAccess);
            credential.Person = await personRepository.Get(personAssignment.PersonId);
            credential.Enabled = personAssignment.Enabled > 0 && credential.Person.Enabled;
        }
        else
        {
            credential.Enabled = false;
        }

        return credential;
    }

    public async Task<IEnumerable<Credential>> CredentialsAssignedToPerson(int personId)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var results = await connection.QueryAsync<(int Id, string Data, int? Enabled)>(
            @"SELECT c.id, c.data, json_extract(ca.data, '$.enabled') as Enabled
              FROM credential c
              INNER JOIN credential_assignment ca ON json_extract(ca.data, '$.credentialId') = c.id
              WHERE json_extract(ca.data, '$.personId') = @personId",
            new { personId });

        return results.Select(r =>
        {
            var credential = JsonSerializer.Deserialize<Credential>(r.Data, GetJsonOptions());
            credential.Id = r.Id;
            credential.AssignedPersonId = personId;
            credential.Enabled = r.Enabled.HasValue ? r.Enabled > 0 : (bool?)null;
            return credential;
        });
    }

    public async Task<IEnumerable<Credential>> Assigned()
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var results = await connection.QueryAsync<(int Id, string Data, int PersonId, int Enabled)>(
            @"SELECT c.id, c.data,
                     json_extract(ca.data, '$.personId') as PersonId,
                     json_extract(ca.data, '$.enabled') as Enabled
              FROM credential c
              INNER JOIN credential_assignment ca ON json_extract(ca.data, '$.credentialId') = c.id");

        return results.Select(r =>
        {
            var credential = JsonSerializer.Deserialize<Credential>(r.Data, GetJsonOptions());
            credential.Id = r.Id;
            credential.AssignedPersonId = r.PersonId;
            credential.Enabled = r.Enabled > 0;
            return credential;
        });
    }

    public async Task<IEnumerable<Credential>> Unassigned()
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var results = await connection.QueryAsync<(int Id, string Data)>(
            @"SELECT c.id, c.data FROM credential c
              WHERE c.id NOT IN (
                SELECT json_extract(ca.data, '$.credentialId')
                FROM credential_assignment ca)");

        return results.Select(r =>
        {
            var credential = JsonSerializer.Deserialize<Credential>(r.Data, GetJsonOptions());
            credential.Id = r.Id;
            return credential;
        });
    }

    public async Task AssignPerson(int credentialId, int personId, bool enabled = true)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var json = JsonSerializer.Serialize(new
        {
            credentialId,
            personId,
            enabled = enabled ? 1 : 0
        }, GetJsonOptions());

        await connection.ExecuteAsync(
            "INSERT INTO credential_assignment (data) VALUES (@data)",
            new { data = json });
    }

    public async Task RevokePerson(int credentialId, int personId)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        await connection.ExecuteAsync(
            @"DELETE FROM credential_assignment
              WHERE json_extract(data, '$.credentialId') = @credentialId
              AND json_extract(data, '$.personId') = @personId",
            new { credentialId, personId });
    }

    public async Task UpdateLastEvent(int credentialId, int lastEventId)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        // Get the current credential data
        var currentData = await connection.QuerySingleOrDefaultAsync<string>(
            "SELECT data FROM credential WHERE id = @credentialId",
            new { credentialId });

        if (currentData != null)
        {
            var credential = JsonSerializer.Deserialize<Credential>(currentData, GetJsonOptions());
            credential.LastEvent = lastEventId;
            var newJson = JsonSerializer.Serialize(credential, GetJsonOptions());

            await connection.ExecuteAsync(
                "UPDATE credential SET data = @data WHERE id = @credentialId",
                new { data = newJson, credentialId });
        }
    }
}
