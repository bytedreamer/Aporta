using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aporta.Core.Models;
using Dapper;

namespace Aporta.Core.DataAccess.Repositories
{
    public class ExtensionRepository
    {
        private const string SqlSelect = @"select id, name, enabled, configuration
                                            from extension";

        private const string SqlInsert = @"insert into extension
                                            (id, name, enabled, configuration) values 
                                            (@id, @name, @enabled, @configuration);";

        private const string SqlUpdate = @"update extension
                                            set enabled = @enabled,
                                                configuration = @configuration
                                            where id = @id;";
        
        private readonly IDataAccess _dataAccess;

        public ExtensionRepository(IDataAccess dataAccess)
        {
            _dataAccess = dataAccess;
            
            SqlMapper.AddTypeHandler(new GuidHandler());
        }
        
        public async Task<ExtensionHost> Get(Guid id)
        {
            using var connection = _dataAccess.CreateDbConnection();
            connection.Open();

            return await connection.QueryFirstOrDefaultAsync<ExtensionHost>(SqlSelect +
                                                                            @" where id = @id", new {id});
        }

        public async Task<IEnumerable<ExtensionHost>> GetAll()
        {
            using var connection = _dataAccess.CreateDbConnection();
            connection.Open();

            return await connection.QueryAsync<ExtensionHost>(SqlSelect);
        }

        public async Task Insert(ExtensionHost extension)
        {
            using var connection = _dataAccess.CreateDbConnection();
            connection.Open();

            await connection.ExecuteAsync(SqlInsert,
                new
                {
                    id = extension.Id, name = extension.Name, enabled = extension.Enabled,
                    configuration = extension.Configuration
                });
        }

        public async Task Update(ExtensionHost extension)
        {
            using var connection = _dataAccess.CreateDbConnection();
            connection.Open();

            await connection.ExecuteAsync(SqlUpdate,
                new
                {
                    id = extension.Id, 
                    name = extension.Name, 
                    enabled = extension.Enabled,
                    configuration = extension.Configuration
                });
        }
    }
}