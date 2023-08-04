using Aporta.Extensions.Endpoint;
using Aporta.Extensions.Hardware;
using Microsoft.Extensions.Logging;

namespace Aporta.Drivers.Virtual;

public class VirtualDriver : IHardwareDriver
{
    private readonly List<IEndpoint> _endpoints = new();
    
    public string Name => "Virtual";
    public Guid Id  => Guid.Parse("6667E442-53B2-4240-A10D-25F5E4400D83");
    public IEnumerable<IEndpoint> Endpoints => _endpoints;
    public void Load(string configuration, ILoggerFactory loggerFactory)
    {

    }

    public void Unload()
    {

    }

    public string CurrentConfiguration()
    {
        return string.Empty;
    }

    public Task<string> PerformAction(string action, string parameters)
    {
        return Task.FromResult(string.Empty);
    }

    public event EventHandler<EventArgs>? UpdatedEndpoints;
    public event EventHandler<AccessCredentialReceivedEventArgs>? AccessCredentialReceived;
    public event EventHandler<StateChangedEventArgs>? StateChanged;
    public event EventHandler<OnlineStatusChangedEventArgs>? OnlineStatusChanged;
}