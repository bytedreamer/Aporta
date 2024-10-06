using Aporta.Drivers.Virtual.Shared;
using Aporta.Extensions;
using Aporta.Extensions.Endpoint;
using Aporta.Extensions.Hardware;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Aporta.Drivers.Virtual.Shared.Actions;

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
    public void Load(string configuration, IDataEncryption dataEncryption, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<VirtualDriver>();

        var configToLoad = JsonConvert.DeserializeObject<Configuration>(configuration);
        if (configToLoad == null) return;
        LoadConfiguration(configToLoad);
    }

    private void LoadConfiguration(Configuration configToLoad)
    {
        foreach (var reader in configToLoad.Readers)
        {
            _configuration.Readers.Add(reader);
            _endpoints.Add(new VirtualReader(reader.Name, Id, $"VR{reader.Number}"));
        }
        
        foreach (var output in configToLoad.Outputs)
        {
            _configuration.Outputs.Add(output);
            _endpoints.Add(new VirtualOutput(output.Name, Id, $"VO{output.Number}"));
        }
        
        foreach (var input in configToLoad.Inputs)
        {
            _configuration.Inputs.Add(input);
            _endpoints.Add(new VirtualInput(input.Name, Id, $"VI{input.Number}"));
        }
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
    public string ScrubSensitiveConfigurationData(string jsonConfigurationString)
    {
        return jsonConfigurationString;
    }

    /// <inheritdoc />
    public Task<string> PerformAction(string action, string parameters)
    {
        if (!Enum.TryParse(action, out ActionType actionType))
        {
            throw new ArgumentException("Invalid action type");
        }
        
        switch (actionType)
        {
            case ActionType.BadgeSwipe:
                ProcessBadgeSwipe(parameters);
                break;

            case ActionType.AddUpdateReader:
                AddUpdateReader(parameters);
                break;

            case ActionType.RemoveReader:
                var requestedReaderToRemove = JsonConvert.DeserializeObject<Device>(parameters);
                if (requestedReaderToRemove != null)
                {
                    var readerToRemove = _configuration.Readers.Find(rdr => rdr.Number == requestedReaderToRemove.Number);
                    if (readerToRemove != null && RemoveReader(readerToRemove))
                    {
                        OnUpdatedEndpoints();
                    }
                }
                break;

            case ActionType.AddUpdateInput:
                AddUpdateInput(parameters);
                break;

            case ActionType.RemoveInput:
                var requestedInputToRemove = JsonConvert.DeserializeObject<Device>(parameters);
                if (requestedInputToRemove != null)
                {
                    var inputToRemove = _configuration.Inputs.Find(rdr => rdr.Number == requestedInputToRemove.Number);
                    if (inputToRemove != null && RemoveInput(inputToRemove))
                    {
                        OnUpdatedEndpoints();
                    }
                }
                break;

            case ActionType.AddUpdateOutput:
                AddUpdateOutput(parameters);
                break;

            case ActionType.RemoveOutput:

                var requestedOutputToRemove = JsonConvert.DeserializeObject<Device>(parameters);
                if (requestedOutputToRemove != null)
                {
                    var outputToRemove = _configuration.Outputs.Find(rdr => rdr.Number == requestedOutputToRemove.Number);
                    if (outputToRemove != null && RemoveOutput(outputToRemove))
                    {
                        OnUpdatedEndpoints();
                    }
                }
                break;
        }

        return Task.FromResult(string.Empty);
    }

    private void AddUpdateReader(string parameters)
    {
        var reader = JsonConvert.DeserializeObject<Device>(parameters);
        if (reader == null)
        {
            throw new NullReferenceException($"Cannot add reader {parameters}");
        }
        
        var foundReader = _configuration.Readers.Find(rdr => rdr.Number == reader.Number);
        if (foundReader != null)
        {
            _configuration.Readers[_configuration.Readers.IndexOf(foundReader)] = reader;
        }
        else
        {
            var readerToAdd = new Device { Name = reader.Name, Number = GetNextReaderNumber() };
            _configuration.Readers.Add(readerToAdd);
            _endpoints.Add(new VirtualReader(readerToAdd.Name, Id, $"VR{readerToAdd.Number}"));
        }
        
        OnUpdatedEndpoints();
    }
    
    private void AddUpdateInput(string parameters)
    {
        var input = JsonConvert.DeserializeObject<Device>(parameters);
        if (input == null)
        {
            throw new NullReferenceException($"Cannot add input {parameters}");
        }
        
        var foundInput = _configuration.Inputs.Find(rdr => rdr.Number == input.Number);
        if (foundInput != null)
        {
            _configuration.Inputs[_configuration.Inputs.IndexOf(foundInput)] = input;
        }
        else
        {
            var inputToAdd = new Device { Name = input.Name, Number = GetNextInputNumber() };
            _configuration.Inputs.Add(inputToAdd);
            _endpoints.Add(new VirtualInput(inputToAdd.Name, Id, $"VI{inputToAdd.Number}"));
        }
        
        OnUpdatedEndpoints();
    }

    private void AddUpdateOutput(string parameters)
    {
        var output = JsonConvert.DeserializeObject<Device>(parameters);
        if (output == null)
        {
            throw new NullReferenceException($"Cannot add output {parameters}");
        }
        
        var foundOutput = _configuration.Outputs.Find(rdr => rdr.Number == output.Number);
        if (foundOutput != null)
        {
            _configuration.Outputs[_configuration.Outputs.IndexOf(foundOutput)] = output;
        }
        else
        {
            var ouptutToAdd = new Device { Name = output.Name, Number = GetNextOutputNumber() };
            _configuration.Outputs.Add(ouptutToAdd);
            _endpoints.Add(new VirtualOutput(ouptutToAdd.Name, Id, $"VO{ouptutToAdd.Number}"));
        }
        
        OnUpdatedEndpoints();
    }

    private byte GetNextReaderNumber()
    {
        if (_configuration.Readers.Count == 0) {  return 1; }
        var maxReader = _configuration.Readers.MaxBy(x => x.Number);
        return (maxReader == null) ? (byte) 1 : (byte)(maxReader.Number + 1);
    }

    private byte GetNextInputNumber()
    {
        if (_configuration.Inputs.Count == 0) { return 1; }
        var maxInput = _configuration.Inputs.MaxBy(x => x.Number);
        return (maxInput == null) ? (byte)1 : (byte)(maxInput.Number + 1);
    }

    private byte GetNextOutputNumber()
    {
        if (_configuration.Outputs.Count == 0) { return 1; }
        var maxOutput = _configuration.Outputs.MaxBy(x => x.Number);
        return (maxOutput == null) ? (byte)1 : (byte)(maxOutput.Number + 1);
    }

    private bool RemoveReader(Device readerToRemove)
    {
        try
        {
            _configuration.Readers.Remove(readerToRemove);

            var endPointToRemove = _endpoints.Find(endpoint => endpoint.Id == $"VR{readerToRemove.Number}");
            if (endPointToRemove != null)
            {
                _endpoints.Remove(endPointToRemove);
            }

        } catch (Exception exception)
        {
            _logger?.LogError(exception, "Exception occurred removing a reader");
            return false;
        }

        return true;
    }

    private bool RemoveInput(Device inputToRemove)
    {
        try
        {
            _configuration.Inputs.Remove(inputToRemove);

            var endPointToRemove = _endpoints.Find(endpoint => endpoint.Id == $"VI{inputToRemove.Number}");
            if (endPointToRemove != null)
            {
                _endpoints.Remove(endPointToRemove);
            }

        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Exception occurred removing an input");
            return false;
        }

        return true;

    }

    private bool RemoveOutput(Device outputToRemove)
    {
        try
        {
            _configuration.Outputs.Remove(outputToRemove);

            var endPointToRemove = _endpoints.Find(endpoint => endpoint.Id == $"VO{outputToRemove.Number}");
            if (endPointToRemove != null)
            {
                _endpoints.Remove(endPointToRemove);
            }

        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Exception occurred removing an input");
            return false;
        }

        return true;

    }

    private void ProcessBadgeSwipe(string parameters)
    {
        var badgeAction = JsonConvert.DeserializeObject<BadgeSwipeAction>(parameters);
        if (badgeAction == null || badgeAction.CardData == null) return;

        _logger?.LogInformation("A card has been presented to the reader");
        var accessPoint = _endpoints.Where(endpoint => endpoint is IAccess).Cast<IAccess>()
            .SingleOrDefault(accessPoint =>
                $"VR{badgeAction.ReaderNumber}" == accessPoint.Id);

        AccessCredentialReceived?.Invoke(this, new AccessCredentialReceivedEventArgs(
                accessPoint, new VirtualCredentialReceivedHandler(badgeAction.CardData)));
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

    private class VirtualCredentialReceivedHandler(string cardData) : ICredentialReceivedHandler
    {
        public bool IsValid()
        {
            return true;
        }
        public string MatchingCardData { get; } = cardData;
    }
}