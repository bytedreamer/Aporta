using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Aporta.Shared.Models;
using Microsoft.AspNetCore.WebUtilities;

namespace Aporta.Shared.Calls;

public class ExtensionCalls : IExtensionCalls
{
    private readonly HttpClient _httpClient;

    public ExtensionCalls(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Extension>> GetAll()
    {
        var response  = await _httpClient.GetAsync(Paths.Extensions);
        if (!response.IsSuccessStatusCode)
        {
            dynamic content = await response.Content.ReadFromJsonAsync<ExpandoObject>();
            throw new Exception(content.details.ToString());
        }

        return await response.Content.ReadFromJsonAsync<Extension[]>();
    }

    public async Task ChangeEnableSettings(Guid extensionId, bool enabled)
    {
        string url = $"{Paths.Extensions}/{extensionId}";
        url = QueryHelpers.AddQueryString(url, "enabled", enabled.ToString());
        var response = await _httpClient.PostAsync(url, new StringContent(string.Empty));
        if (!response.IsSuccessStatusCode)
        {
            dynamic content = await response.Content.ReadFromJsonAsync<ExpandoObject>();
            throw new Exception(content.details.ToString());
        }
    }
}

public interface IExtensionCalls
{
    /// <summary>
    /// Get all the driver extensions
    /// </summary>
    /// <returns>The driver extensions</returns>
    Task<IEnumerable<Extension>> GetAll();
    
    Task ChangeEnableSettings(Guid exentionId, bool enabled);
}