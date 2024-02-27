using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace Aporta.WebClient.Messaging;

public class HubProxy : IHubProxy
{
    private readonly HubConnection _hubConnection;

    public HubProxy(Uri uri)
    {
        _hubConnection =  new HubConnectionBuilder()
            .WithUrl(uri)
            .WithAutomaticReconnect(new SignalRRetryPolicy())
            .Build();
    }

    public void On<T1>(string methodName, Func<T1, Task> handler)
    {
        _hubConnection.On(methodName, handler);
    }

    public async Task StartAsync()
    {
        await _hubConnection.StartAsync();
    }
    
    public async Task StopAsync()
    {
        await _hubConnection.StopAsync();
    }

    public ValueTask DisposeAsync()
    {
        return _hubConnection.DisposeAsync();
    }
}

public interface IHubProxy
{
    Task StartAsync();

    Task StopAsync();
    
    ValueTask DisposeAsync();
    
    void On<T1>(string methodName, Func<T1, Task> handler);
}