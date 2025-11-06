using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Aporta.Shared.Models;

namespace Aporta.Shared.Calls
{
    /// <inheritdoc />
    public class DoorCalls(HttpClient httpClient) : IDoorCalls
    {

        /// <inheritdoc />
        public async Task<Endpoint[]> GetAvailableEndpoints()
        {
            return await httpClient.GetFromJsonAsync<Endpoint[]>($"{Paths.Doors}/endpointsavailable");
        }

        /// <inheritdoc />
        public async Task<Endpoint[]> GetAvailableAccessPoints()
        {
            return await httpClient.GetFromJsonAsync<Endpoint[]>($"{Paths.Doors}/available");
        }

        /// <inheritdoc />
        public async Task<List<Door>> GetAllDoors()
        {
            return await httpClient.GetFromJsonAsync<List<Door>>(Paths.Doors);
        }

    }

    /// <summary>
    /// Represents a class that handles Door API calls.
    /// </summary>
    public interface IDoorCalls
    {
        /// <summary>
        /// Return all endpoints available to be assigned to a door
        /// </summary>
        public Task<Endpoint[]> GetAvailableEndpoints();

        /// <summary>
        /// Return all access points available to be assigned to a door
        /// </summary>
        public Task<Endpoint[]> GetAvailableAccessPoints();

        /// <summary>
        /// Return all doors
        /// </summary>
        public Task<List<Door>> GetAllDoors();
    }

}
