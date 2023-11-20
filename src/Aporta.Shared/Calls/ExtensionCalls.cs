using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Aporta.Shared.Models;

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
            throw new Exception($"Unable to get driver extensions. Return status code {(int)response.StatusCode}:{response.ReasonPhrase}");
        }

        return await response.Content.ReadFromJsonAsync<Extension[]>();
    }
}

public interface IExtensionCalls
{
    /// <summary>
    /// Get all the driver extensions
    /// </summary>
    /// <returns>The driver extensions</returns>
    Task<IEnumerable<Extension>> GetAll();
}