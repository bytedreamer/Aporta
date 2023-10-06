using System.Collections.Generic;
using System.Threading.Tasks;

using Dapper;

using Aporta.Shared.Models;

namespace Aporta.Core.DataAccess.Repositories;

public abstract class BaseRepository<T>
{
    protected abstract IDataAccess DataAccess { get; }
        
    protected abstract string SqlSelect { get; }
        
    protected abstract string SqlInsert { get; }
        
    protected abstract string SqlDelete { get; }
    
    protected abstract string SqlRowCount { get; }

    public async Task<T> Get(int id)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        return await connection.QuerySingleOrDefaultAsync<T>($@"{SqlSelect} where id = @id", new {id});
    }

    public async Task<IEnumerable<T>> GetAll()
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        return await connection.QueryAsync<T>(SqlSelect);
    }

    public async Task<PaginatedItemsDto<T>> GetAll(int pageNumber, int pageSize, string orderBy)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        int offset = (pageNumber - 1) * pageSize;
        var results = await connection.QueryAsync<T>($@"{SqlSelect} order by {orderBy} desc limit @offset, @pageSize",
            new { offset, pageSize });

        return new PaginatedItemsDto<T>
        {
            Items = results,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = await connection.ExecuteScalarAsync<int>(SqlRowCount)
        };
    }

    public async Task<int> Insert(T record)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        int id = await connection.QueryFirstAsync<int>($"{SqlInsert}; select last_insert_rowid()",
            InsertParameters(record));

        InsertId(record, id);

        return id;
    }

    protected abstract object InsertParameters(T record);

    protected abstract void InsertId(T record, int id);
        
    public async Task Delete(int id)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        await connection.ExecuteAsync(SqlDelete,
            new {id});
    }
}