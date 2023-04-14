using System.Threading.Tasks;
using Aporta.Shared.Models;
using Dapper;

namespace Aporta.Core.DataAccess.Repositories
{
    public class CredentialRepository : BaseRepository<Credential>
    {
        public CredentialRepository(IDataAccess dataAccess)
        {
            DataAccess = dataAccess;
        }
        
        protected override IDataAccess DataAccess { get; }
        
        protected override string SqlSelect => @"select credential.id, 
                                                    credential.number, 
                                                    credential.enroll_date as enrollDate
                                                    from credential";
        
        protected override string SqlInsert => @"insert into credential
                                                (number, enroll_date) values 
                                                (@number, @enrollDate)";
        
        private string SqlAssignmentInsert => @"insert into credential_assignment
                                                (person_id, credential_id, enabled) values 
                                                (@personId, @credentialId, @enabled)";

        protected override string SqlDelete => @"delete from credential where id = @id";
        
        protected override object InsertParameters(Credential credential)
        {
            return new
            {
                number = credential.Number,
                enrollDate = credential.EnrollDate
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

            if (personAssignment == null)
            {
                return credential;
            }

            var personRepository = new PersonRepository(DataAccess);
            credential.Person = await personRepository.Get((int)personAssignment.personId);
            credential.Enabled = personAssignment.enabled > 0 && credential.Person.Enabled;
            
            return credential;
        }

        public async Task AssignPerson(int personId, int credentialId)
        {
            using var connection = DataAccess.CreateDbConnection();
            connection.Open();
            
            await connection.ExecuteAsync(SqlAssignmentInsert,
                new
                {
                    personId, 
                    credentialId,
                    enabled = true
                });
        }
    }
}