using System.Collections.Generic;
using System.Threading.Tasks;
using Aporta.Shared.Models;
using Dapper;

namespace Aporta.Core.DataAccess.Repositories;

public class CredentialRepository : BaseRepository<Credential>
{
    public CredentialRepository(IDataAccess dataAccess)
    {
        DataAccess = dataAccess;
    }
        
    protected override IDataAccess DataAccess { get; }
        
    protected override string SqlSelect => @"select credential.id, 
                                                    credential.number, 
                                                    credential.last_event lastEvent,
                                                    credential_assignment.person_id assignedPersonId,
                                                    credential_assignment.enabled
                                                    from credential
                                                    left join credential_assignment on credential.id = credential_assignment.credential_id";
        
    protected override string SqlInsert => @"insert into credential
                                                (number, last_event) values 
                                                (@number, @lastEvent)";
        
    private string SqlAssignmentInsert => @"insert into credential_assignment
                                                (person_id, credential_id, enabled) values 
                                                (@personId, @credentialId, @enabled)";

    private string SqlUpdateLastEvent => @"update credential
                                               set last_event = @lastEventId
                                               where id = @credentialId";

    private string SqlAssignedCredentials => SqlSelect + @" where credential.id IN (SELECT credential_id FROM credential_assignment)";
    private string SqlUnassignedCredentials => SqlSelect + @" where credential.id NOT IN (SELECT credential_id FROM credential_assignment)";

    protected override string SqlDelete => @"delete from credential where id = @id";
        
    protected override object InsertParameters(Credential credential)
    {
        return new
        {
            number = credential.Number,
            lastEvent = credential.LastEvent
        };
    }

    protected override void InsertId(Credential credential, int id)
    {
        credential.Id = id;
    }

    public async Task<AssignedCredential> AssignedCredential(string cardNumber)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var credential = await connection.QuerySingleOrDefaultAsync<AssignedCredential>(
            $@"{SqlSelect} where number = @number",
            new {number = cardNumber});

        if (credential == null)
        {
            return null;
        }

        var personAssignment = await connection.QuerySingleOrDefaultAsync(
            "select person_id as personId, enabled from credential_assignment where credential_id = @credentialId",
            new {credentialId = credential.Id});

        if (personAssignment != null)
        {
            var personRepository = new PersonRepository(DataAccess);
            credential.Person = await personRepository.Get((int)personAssignment.personId);
            credential.Enabled = personAssignment.enabled > 0 && credential.Person.Enabled;
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

        return await connection.QueryAsync<Credential>(
            $@"{SqlSelect} where credential_assignment.person_id = @personId",
            new { personId });
    }

    public async Task<IEnumerable<Credential>> Assigned()
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        return await connection.QueryAsync<Credential>(SqlAssignedCredentials);
    }
        
    public async Task<IEnumerable<Credential>> Unassigned()
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        return await connection.QueryAsync<Credential>(SqlUnassignedCredentials);
    }
        
    public async Task AssignPerson(int credentialId, int personId, bool enabled = true)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();
            
        await connection.ExecuteAsync(SqlAssignmentInsert,
            new
            {
                credentialId,
                personId, 
                enabled
            });
    }
        
    public async Task UpdateLastEvent(int credentialId, int lastEventId)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        await connection.ExecuteAsync(SqlUpdateLastEvent,
            new
            {
                credentialId, lastEventId
            });
    }
}