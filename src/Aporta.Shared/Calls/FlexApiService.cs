using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Aporta.Shared.Models.Flex;

namespace Aporta.Shared.Calls;

public class FlexApiService
{
    private readonly HttpClient _httpClient;
    private string _sessionToken;

    public FlexApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FlexListResponse<FlexEvt>> GetEvtListAsync(int offset, int max)
    {
        return await FlexGetAsync<FlexListResponse<FlexEvt>>($"flex/evt/list?offset={offset}&max={max}");
    }

    public async Task<FlexListResponse<FlexCred>> GetCredListAsync(int offset, int max)
    {
        return await FlexGetAsync<FlexListResponse<FlexCred>>($"flex/cred/list?offset={offset}&max={max}");
    }

    public async Task<FlexListResponse<FlexDev>> GetSensorListAsync(int offset, int max)
    {
        return await FlexGetAsync<FlexListResponse<FlexDev>>($"flex/sensor/list?offset={offset}&max={max}");
    }

    public async Task<FlexListResponse<FlexDev>> GetActuatorListAsync(int offset, int max)
    {
        return await FlexGetAsync<FlexListResponse<FlexDev>>($"flex/actuator/list?offset={offset}&max={max}");
    }

    private async Task EnsureAuthenticated()
    {
        if (!string.IsNullOrEmpty(_sessionToken))
            return;

        var request = new FlexAuthenticateRequest { Username = "admin", Password = "pass" };
        var response = await _httpClient.PostAsJsonAsync("flex/authenticate", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<FlexAuthenticateResult>();
        _sessionToken = result?.SessionToken;
    }

    private async Task<T> FlexGetAsync<T>(string url)
    {
        await EnsureAuthenticated();

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("sessionToken", _sessionToken);
        var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Token expired — re-authenticate and retry once
            _sessionToken = null;
            await EnsureAuthenticated();

            request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("sessionToken", _sessionToken);
            response = await _httpClient.SendAsync(request);
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }
}
