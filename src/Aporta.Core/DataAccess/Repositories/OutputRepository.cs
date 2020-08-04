using System.Threading.Tasks;
using Aporta.Shared.Models;
using Dapper;

namespace Aporta.Core.DataAccess.Repositories
{
    public class OutputRepository : BaseRepository<Output>
    {
        public OutputRepository(IDataAccess dataAccess)
        {
            DataAccess = dataAccess;
        }
        
        protected override IDataAccess DataAccess { get; }

        protected override string SqlSelect => @"select output.id, output.endpoint_id as endpointId, output.name, output.pulse_timer as pulseTimer
                                                from output";

        protected override string SqlInsert => @"insert into output
                                                (endpoint_id, name, pulse_timer) values 
                                                (@endpointId, @name, @pulseTimer)";

        protected override string SqlDelete => @"delete from output where id = @id";
        
        public async Task<Output> GetForDriverId(string driverId)
        {
            using var connection = DataAccess.CreateDbConnection();
            connection.Open();

            return await connection.QueryFirstAsync<Output>(SqlSelect + 
                                                         @" inner join endpoint on output.endpoint_id = endpoint.id 
                                                            where endpoint.driver_id = @driverId",
                new {driverId});
        }
        
        protected override object InsertParameters(Output output)
        {
            return new
            {
                endpointId = output.EndpointId,
                name = output.Name,
                pulseTimer = output.PulseTimer
            };
        }

        protected override void InsertId(Output output, int id)
        {
            output.Id = id;
        }
    }
}