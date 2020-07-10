using System.Collections.Generic;
using System.Threading.Tasks;
using Aporta.Shared.Models;
using Dapper;

namespace Aporta.Core.DataAccess.Repositories
{
    public class EndpointRepository
    {
        private const string SqlSelect = @"select id, name, endpoint_type as type, configuration, extension_id as extensionId
                                            from endpoint";

        private const string SqlInsert = @"insert into endpoint
                                            (name, endpoint_type, configuration, extension_id) values 
                                            (@name, @endpointType, @configuration, @extensionId); select last_insert_rowid()";

        private readonly IDataAccess _dataAccess;

        public EndpointRepository(IDataAccess dataAccess)
        {
            _dataAccess = dataAccess;

            SqlMapper.AddTypeHandler(new GuidHandler());
        }

        public async Task<Endpoint> Get(int id)
        {
            using var connection = _dataAccess.CreateDbConnection();
            connection.Open();

            return await connection.QueryFirstOrDefaultAsync<Endpoint>(SqlSelect +
                                                                       @" where id = @id", new {id});
        }

        public async Task<IEnumerable<Endpoint>> GetAll()
        {
            using var connection = _dataAccess.CreateDbConnection();
            connection.Open();

            return await connection.QueryAsync<Endpoint>(SqlSelect);
        }

        public async Task<int> Insert(Endpoint endpoint)
        {
            using var connection = _dataAccess.CreateDbConnection();
            connection.Open();

            return await connection.QueryFirstAsync<int>(SqlInsert,
                new
                {
                    name = endpoint.Name, endpointType = endpoint.Type,
                    configuration = endpoint.Configuration, extensionId = endpoint.ExtensionId
                });
        }
    }
}