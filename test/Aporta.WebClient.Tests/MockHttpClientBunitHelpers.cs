namespace Aporta.WebClient.Tests;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

public static class MockHttpClientBunitHelpers
{
    private static readonly string BaseUrl = "http://localhost";
    
    public static MockHttpMessageHandler AddMockHttpClient(this TestServiceProvider services)
    {
        var mockHttpHandler = new MockHttpMessageHandler
        {
            AutoFlush = false
        };
        var httpClient = mockHttpHandler.ToHttpClient();
        httpClient.BaseAddress = new Uri(BaseUrl);
        services.AddSingleton(httpClient);
        return mockHttpHandler;
    }

    public static string BuildUrl(string uri)
    {
        return $"{BaseUrl}/{uri}";
    }

    public static MockedRequest RespondJson<T>(this MockedRequest request, T content)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        request.Respond(_ =>
        {
            response.Content = new StringContent(JsonSerializer.Serialize(content));
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return response;
        });
        return request;
    }
}