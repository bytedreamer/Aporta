using Aporta.Drivers.Virtual.Shared;
using Aporta.Extensions.Endpoint;
using Aporta.Extensions.Hardware;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Aporta.Drivers.Virtual;

public class VirtualDriver : IHardwareDriver
{
    private readonly List<IEndpoint> _endpoints = new();
    private readonly Configuration _configuration = new();
    
    private ILogger<VirtualDriver>? _logger;
    
    /// <inheritdoc />
    public string Name => "Virtual";
    
    /// <inheritdoc />
    public Guid Id  => Guid.Parse("6667E442-53B2-4240-A10D-25F5E4400D83");
    
    /// <inheritdoc />
    public IEnumerable<IEndpoint> Endpoints => _endpoints;
    
    /// <inheritdoc />
    public void Load(string configuration, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<VirtualDriver>();
        
        _configuration.Readers.Add(new Reader{Name = "Virtual Reader 1", Number = 1});
        foreach (var reader in _configuration.Readers)
        {
            _endpoints.Add(new VirtualReader(reader.Name, Id, $"VR{reader.Number}"));
        }
        
        _configuration.Outputs.Add(new Output{Name = "Virtual Output 1", Number = 1});
        foreach (var output in _configuration.Outputs)
        {
            _endpoints.Add(new VirtualOutput(output.Name, Id, $"VO{output.Number}"));
        }
        
        _configuration.Inputs.Add(new Input{Name = "Virtual Input 1", Number = 1});
        _configuration.Inputs.Add(new Input{Name = "Virtual Input 1", Number = 2});
        foreach (var input in _configuration.Inputs)
        {
            _endpoints.Add(new VirtualInput(input.Name, Id, $"VI{input.Number}"));
        }
        
        OnUpdatedEndpoints();
    }
    
    /// <inheritdoc />
    public void Unload()
    {

    }

    /// <inheritdoc />
    public string CurrentConfiguration()
    {
        return JsonConvert.SerializeObject(_configuration);
    }

    /// <inheritdoc />
    public Task<string> PerformAction(string action, string parameters)
    {
        return Task.FromResult(string.Empty);
    }

    /// <inheritdoc />
    public event EventHandler<EventArgs>? UpdatedEndpoints;
    protected virtual void OnUpdatedEndpoints()
    {
        UpdatedEndpoints?.Invoke(this, EventArgs.Empty);
    }
    
    /// <inheritdoc />
    public event EventHandler<AccessCredentialReceivedEventArgs>? AccessCredentialReceived;
    protected virtual void OnAccessCredentialReceived(AccessCredentialReceivedEventArgs eventArgs)
    {
        AccessCredentialReceived?.Invoke(this, eventArgs);
    }
    
    /// <inheritdoc />
    public event EventHandler<StateChangedEventArgs>? StateChanged;
    protected virtual void OnStateChanged(IEndpoint endpoint, bool state)
    {
        _logger?.LogInformation("State changed for {EndpointName} to {State}", endpoint.Name, state);
        StateChanged?.Invoke(this, new StateChangedEventArgs(endpoint, state));
    }
    
    /// <inheritdoc />
    public event EventHandler<OnlineStatusChangedEventArgs>? OnlineStatusChanged;
    protected virtual void OnOnlineStatusChanged(OnlineStatusChangedEventArgs eventArgs)
    {
        OnlineStatusChanged?.Invoke(this, eventArgs);
    }
}