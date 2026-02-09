using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
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

    public async Task<FlexInstanceResponse<FlexCred>> SaveCredAsync(FlexCred cred)
    {
        return await FlexPostAsync<FlexInstanceResponse<FlexCred>>("flex/cred/save", cred);
    }

    public async Task<FlexVoid> DeleteCredAsync(int unid)
    {
        return await FlexPostAsync<FlexVoid>($"flex/cred/delete/{unid}");
    }

    public async Task<FlexVoid> EnrollCredAsync(int credentialId, int personId)
    {
        return await FlexPostAsync<FlexVoid>($"flex/cred/{credentialId}/enroll/{personId}");
    }

    public async Task<FlexListResponse<FlexDev>> GetAvailableSensorsAsync()
    {
        return await FlexGetAsync<FlexListResponse<FlexDev>>("flex/sensor/available");
    }

    public async Task<FlexListResponse<FlexDev>> GetAvailableActuatorsAsync()
    {
        return await FlexGetAsync<FlexListResponse<FlexDev>>("flex/actuator/available");
    }

    public async Task<FlexInstanceResponse<FlexDev>> SaveSensorAsync(FlexDev sensor)
    {
        return await FlexPostAsync<FlexInstanceResponse<FlexDev>>("flex/sensor/save", sensor);
    }

    public async Task<FlexInstanceResponse<FlexDev>> SaveActuatorAsync(FlexDev actuator)
    {
        return await FlexPostAsync<FlexInstanceResponse<FlexDev>>("flex/actuator/save", actuator);
    }

    public async Task<FlexVoid> DeleteSensorAsync(int unid)
    {
        return await FlexPostAsync<FlexVoid>($"flex/sensor/delete/{unid}");
    }

    public async Task<FlexVoid> DeleteActuatorAsync(int unid)
    {
        return await FlexPostAsync<FlexVoid>($"flex/actuator/delete/{unid}");
    }

    public async Task<bool?> GetSensorStateAsync(int id)
    {
        var json = await FlexGetAsync<JsonElement>($"flex/sensor/state/{id}");
        return ParseStateResponse(json);
    }

    public async Task<bool?> GetActuatorStateAsync(int id)
    {
        var json = await FlexGetAsync<JsonElement>($"flex/actuator/state/{id}");
        return ParseStateResponse(json);
    }

    public async Task SetActuatorStateAsync(int id, bool state)
    {
        await FlexPostAsync<FlexVoid>($"flex/actuator/state/{id}?state={state}");
    }

    private static bool? ParseStateResponse(JsonElement json)
    {
        if (json.TryGetProperty("state", out var stateProp))
        {
            if (stateProp.ValueKind == JsonValueKind.Null)
                return null;
            return stateProp.GetBoolean();
        }
        return null;
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

    private async Task<T> FlexPostAsync<T>(string url, object body = null)
    {
        await EnsureAuthenticated();

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("sessionToken", _sessionToken);
        if (body != null)
            request.Content = JsonContent.Create(body);

        var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _sessionToken = null;
            await EnsureAuthenticated();

            request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("sessionToken", _sessionToken);
            if (body != null)
                request.Content = JsonContent.Create(body);
            response = await _httpClient.SendAsync(request);
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }
}
