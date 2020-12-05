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

        public async Task<bool> IsMatchingNumber(string hashedCardNumber)
        {
            using var connection = DataAccess.CreateDbConnection();
            connection.Open();

            return await connection.QuerySingleOrDefaultAsync<Credential>($@"{SqlSelect} where number = @number",
                new {number = hashedCardNumber}) != null;
        }
    }
}