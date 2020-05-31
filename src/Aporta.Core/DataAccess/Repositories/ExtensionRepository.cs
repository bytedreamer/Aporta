using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace Aporta.Core.DataAccess.Repositories
{
    public class ExtensionRepository
    {
        private readonly IDataAccess _dataAccess;

        public ExtensionRepository(IDataAccess dataAccess)
        {
            _dataAccess = dataAccess;
            
            SqlMapper.AddTypeHandler(new GuidHandler());
        }
        
        public async Task<Model.Extension> Get(Guid id)
        {
            using var connection = _dataAccess.CreateDbConnection();
            connection.Open();

            return await connection.QueryFirstOrDefaultAsync<Model.Extension>(
                @"select id, name
                        from extension
                        where id = @id", new {id});
        }

        public async Task<IEnumerable<Model.Extension>> GetAll()
        {
            using var connection = _dataAccess.CreateDbConnection();
            connection.Open();

            return await connection.QueryAsync<Model.Extension>(
                @"select id, name
                        from extension");
        }
        
        public async Task Insert(Model.Extension extension)
        {
            using var connection = _dataAccess.CreateDbConnection();
            connection.Open();

            await connection.ExecuteAsync(
                @"insert into extension
                        (id, name) values 
                        (@id, @name);", 
                new {id = extension.Id, name = extension.Name});
        }
        
        private abstract class SqLiteTypeHandler<T> : SqlMapper.TypeHandler<T>
        {
            public override void SetValue(IDbDataParameter parameter, T value)
                => parameter.Value = value;
        }
        
        private class GuidHandler : SqLiteTypeHandler<Guid>
        {
            public override Guid Parse(object value)
                => Guid.Parse((string)value);
        }
    }
}