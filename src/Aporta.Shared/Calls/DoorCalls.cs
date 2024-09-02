using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
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
            return await httpClient.GetFromJsonAsync<Endpoint[]>($"{Paths.Doors}/available");
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
    }

}
