using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aporta.Shared.Models;
using Dapper;

namespace Aporta.Core.DataAccess.Repositories
{
    public class EndpointRepository
    {
        private const string SqlSelect = @"select id, name, endpoint_type as type, driver_id as endpointId, extension_id as extensionId
                                            from endpoint";

        private const string SqlInsert = @"insert into endpoint
                                            (name, endpoint_type, driver_id, extension_id) values 
                                            (@name, @endpointType, @configuration, @extensionId); select last_insert_rowid()";

        private const string SqlDelete = @"delete from endpoint
                                            where id = @id";

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

        public async Task<IEnumerable<Endpoint>> GetForExtension(Guid extensionId)
        {
            using var connection = _dataAccess.CreateDbConnection();
            connection.Open();

            return await connection.QueryAsync<Endpoint>(SqlSelect + @" where extension_id = @extensionId",
                new {extensionId});
        }

        public async Task Insert(Endpoint endpoint)
        {
            using var connection = _dataAccess.CreateDbConnection();
            connection.Open();

            endpoint.Id = await connection.QueryFirstAsync<int>(SqlInsert,
                new
                {
                    name = endpoint.Name, endpointType = endpoint.Type,
                    configuration = endpoint.EndpointId, extensionId = endpoint.ExtensionId
                });
        }

        public async Task Delete(int id)
        {
            using var connection = _dataAccess.CreateDbConnection();
            connection.Open();

            await connection.ExecuteAsync(SqlDelete,
                new {id = id});
        }
    }
}