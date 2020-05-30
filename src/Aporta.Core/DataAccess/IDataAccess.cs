using System.Data;
using System.Threading.Tasks;

namespace Aporta.Core.DataAccess
{
    /// <summary>
    /// Routines to access data in persistent storage
    /// </summary>
    public interface IDataAccess
    {
        /// <summary>
        /// Create a database connection for accessing persistent storage
        /// </summary>
        /// <returns>The database connection</returns>
        IDbConnection CreateDbConnection();

        /// <summary>
        /// Query for the current version of the schema
        /// </summary>
        /// <returns>The current version of the schema</returns>
        Task<int> CurrentVersion();
        
        /// <summary>
        /// Update database schema to the correct version
        /// </summary>
        /// <returns></returns>
        Task UpdateSchema();
    }
}