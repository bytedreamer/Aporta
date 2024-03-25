using System;
using System.Dynamic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace Aporta.Shared.Calls;

/// <inheritdoc />
public class DriverConfigurationCalls(HttpClient httpClient) : IDriverConfigurationCalls
{
    /// <inheritdoc />
    public async Task<string> PerformAction(Guid extensionId, string actionType, string parameters)
    {
        string url = string.Format(Paths.ExtensionPerformAction, extensionId, actionType);
        var response =
            await httpClient.PostAsync(url, new StringContent(parameters, Encoding.UTF8, "application/json"));

        if (!response.IsSuccessStatusCode)
        {
            dynamic content = await response.Content.ReadFromJsonAsync<ExpandoObject>();
            throw new Exception(content.detail.ToString());
        }
        
        return await response.Content.ReadAsStringAsync();
    }
}

/// <summary>
/// Represents a class that handles driver configuration API calls.
/// </summary>
public interface IDriverConfigurationCalls
{
    /// <summary>
    /// Performs an action with the given parameters on a driver extension.
    /// </summary>
    /// <param name="extensionId">The ID of the driver extension.</param>
    /// <param name="actionType">The type of action to perform.</param>
    /// <param name="parameters">The parameters for the action.</param>
    /// <returns>
    /// The result of the action as a string.
    /// </returns>
    Task<string> PerformAction(Guid extensionId, string actionType, string parameters);
}