using System.Threading.Tasks;
using Aporta.Shared.Models;
using Dapper;

namespace Aporta.Core.DataAccess.Repositories
{
    public class InputRepository : BaseRepository<Input>
    {
        public InputRepository(IDataAccess dataAccess)
        {
            DataAccess = dataAccess;
        }
        
        protected override IDataAccess DataAccess { get; }

        protected override string SqlSelect => @"select input.id, input.endpoint_id as endpointId, input.name
                                                from input";

        protected override string SqlInsert => @"insert into input
                                                (endpoint_id, name) values 
                                                (@endpointId, @name)";

        protected override string SqlDelete => @"delete from input where id = @id";
        
        public async Task<Input> GetForDriverId(string driverId)
        {
            using var connection = DataAccess.CreateDbConnection();
            connection.Open();

            return await connection.QueryFirstAsync<Input>(SqlSelect + 
                                                         @" inner join endpoint on input.endpoint_id = endpoint.id 
                                                            where endpoint.driver_id = @driverId",
                new {driverId});
        }
        
        protected override object InsertParameters(Input input)
        {
            return new
            {
                endpointId = input.EndpointId,
                name = input.Name
            };
        }

        protected override void InsertId(Input input, int id)
        {
            input.Id = id;
        }
    }
}