using Aporta.Shared.Models;

namespace Aporta.Core.DataAccess.Repositories
{
    public class OutputRepository : BaseRepository<Output>
    {
        public OutputRepository(IDataAccess dataAccess)
        {
            DataAccess = dataAccess;
        }
        
        protected override IDataAccess DataAccess { get; }

        protected override string SqlSelect => @"select id, endpoint_id as endpointId, name, pulse_timer as pulseTimer
                                            from output";

        protected override string SqlInsert => @"insert into output
                                            (endpoint_id, name, pulse_timer) values 
                                            (@endpointId, @name, @pulseTimer)";

        protected override string SqlDelete => @"delete from output where id = @id";
        
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