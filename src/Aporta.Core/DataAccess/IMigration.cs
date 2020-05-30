using System.Threading.Tasks;

namespace Aporta.Core.DataAccess
{
    public interface IMigration
    {
        /// <summary>
        /// Version of the migration
        /// </summary>
        int Version { get; }
        
        /// <summary>
        /// Name of the migration
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Perform an updated to the schema to match the version
        /// </summary>
        /// <param name="dataAccess"></param>
        Task PerformUpdate(IDataAccess dataAccess);
    }
}