using System.Collections.Generic;
using System.Threading.Tasks;

using Dapper;

using Aporta.Shared.Models;

namespace Aporta.Core.DataAccess.Repositories;

public abstract class BaseRepository<T>
{
    /// <summary>
    /// Represents a base repository for accessing data from persistent storage.
    /// </summary>
    /// <typeparam name="T">The type of the data entity.</typeparam>
    protected abstract IDataAccess DataAccess { get; }

    /// <summary>
    /// Represents the SQL SELECT query that is used to select data from the persistent storage.
    /// </summary>
    protected abstract string SqlSelect { get; }

    /// <summary>
    /// Represents the SQL INSERT statement for inserting a record into the database.
    /// </summary>
    protected abstract string SqlInsert { get; }

    /// <summary>
    /// Represents a SQL UPDATE statement for updating a record in a database table.
    /// </summary>
    protected abstract string SqlUpdate { get; }

    /// <summary>
    /// Represents the SQL DELETE statement for a specific entity in the database.
    /// </summary>
    protected abstract string SqlDelete { get; }

    /// <summary>
    /// Represents the SQL query for retrieving the total number of rows in the associated table.
    /// </summary>
    protected abstract string SqlRowCount { get; }

    /// <summary>
    /// Retrieves an entity by its ID.
    /// </summary>
    /// <param name="id">The ID of the entity to retrieve.</param>
    /// <returns>The entity with the specified ID.</returns>
    public async Task<T> Get(int id)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        return await connection.QuerySingleOrDefaultAsync<T>($@"{SqlSelect} where id = @id", new {id});
    }

    /// <summary>
    /// Retrieves all entities of type T.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains an enumerable collection of entities of type T.</returns>
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

    /// <summary>
    /// Inserts a record into the database and returns the inserted record's ID.
    /// </summary>
    /// <param name="record">The record to insert.</param>
    /// <returns>The ID of the inserted record.</returns>
    public async Task<int> Insert(T record)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        int id = await connection.QueryFirstAsync<int>($"{SqlInsert}; select last_insert_rowid()",
            InsertParameters(record));

        InsertId(record, id);

        return id;
    }

    /// <summary>
    /// Updates an entity in the database.
    /// </summary>
    /// <param name="record">The entity to be updated.</param>
    public async Task Update(T record)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();
        
        await connection.ExecuteAsync(SqlUpdate, UpdateParameters(record));
    }

    /// <summary>
    /// Parameters for SQL INSERT command.
    /// </summary>
    /// <param name="record">The record to be inserted.</param>
    /// <returns>An anonymous type object with properties to be inserted.</returns>
    protected abstract object InsertParameters(T record);
    
    /// <summary>
    /// Parameters for SQL UPDATE command.
    /// </summary>
    /// <param name="record">The record to be updated.</param>
    /// <returns>An anonymous type object with properties to be updated.</returns>
    protected abstract object UpdateParameters(T record);

    /// <summary>
    /// Sets the new created ID value on an exsiting record.
    /// </summary>
    /// <param name="record">The record to insert into the database.</param>
    /// <param name="id">The new created ID of the inserted record.</param>
    protected abstract void InsertId(T record, int id);

    /// <summary>
    /// Deletes a record with the specified ID.
    /// </summary>
    /// <param name="id">The ID of the record to delete.</param>
    /// <returns>A task representing the asynchronous delete operation.</returns>
    public async Task Delete(int id)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        await connection.ExecuteAsync(SqlDelete,
            new {id});
    }
}