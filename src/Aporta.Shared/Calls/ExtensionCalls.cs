using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Aporta.Shared.Models;
using Microsoft.AspNetCore.WebUtilities;
using static System.Net.WebRequestMethods;

namespace Aporta.Shared.Calls;

/// <inheritdoc />
public class ExtensionCalls(HttpClient httpClient) : IExtensionCalls
{
    /// <inheritdoc />
    public async Task<IEnumerable<Extension>> GetAll()
    {
        var response  = await httpClient.GetAsync(Paths.Extensions);
        if (!response.IsSuccessStatusCode)
        {
            dynamic content = await response.Content.ReadFromJsonAsync<ExpandoObject>();
            throw new Exception(content.detail.ToString());
        }

        return await response.Content.ReadFromJsonAsync<Extension[]>();
    }

    /// <inheritdoc />
    public async Task<Extension> GetExtension(Guid extensionId)
    {
        return await httpClient.GetFromJsonAsync<Extension>($"{Paths.Extensions}/{extensionId}");
    }

    /// <inheritdoc />
    public async Task ChangeEnableSettings(Guid extensionId, bool enabled)
    {
        string url = $"{Paths.Extensions}/{extensionId}";
        url = QueryHelpers.AddQueryString(url, "enabled", enabled.ToString());
        var response = await httpClient.PostAsync(url, new StringContent(string.Empty));
        if (!response.IsSuccessStatusCode)
        {
            dynamic content = await response.Content.ReadFromJsonAsync<ExpandoObject>();
            throw new Exception(content.detail.ToString());
        }
    }
}

/// <summary>
/// Represents a class that handles extension API calls.
/// </summary>
public interface IExtensionCalls
{
    /// <summary>
    /// Get all the driver extensions.
    /// </summary>
    /// <returns>A collection of driver extensions</returns>
    Task<IEnumerable<Extension>> GetAll();

    /// <summary>
    /// Changes the enable settings of an extension.
    /// </summary>
    /// <param name="extensionId">The ID of the extension.</param>
    /// <param name="enabled">True to enable the extension, false to disable it.</param>
    Task ChangeEnableSettings(Guid extensionId, bool enabled);

    /// <summary>
    /// Return the requested driver extension.
    /// </summary>
    /// <param name="extensionId">The ID of the extension.</param>
    public Task<Extension> GetExtension(Guid extensionId);

}