﻿using Aporta.Drivers.Virtual.Shared;
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
        
        _configuration.Readers.Add(new Reader{Name = "Virtual Reader 1", Number = 1});
        _configuration.Readers.Add(new Reader { Name = "Virtual Reader 2", Number = 2 });
        _configuration.Readers.Add(new Reader { Name = "Virtual Reader 3", Number = 3 });
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
    public string ScrubSensitiveConfigurationData(string jsonConfigurationString)
    {
        return jsonConfigurationString;
    }

    /// <inheritdoc />
    public Task<string> PerformAction(string action, string parameters)
    {
        if (Enum.TryParse(action, out ActionType actionType))
        {
            switch (actionType)
            {
                case ActionType.BadgeSwipe:
                    ProcessBadgeSwipe(parameters);
                    break;

                case ActionType.AddReader:

                    var readerName = parameters;
                    if (string.IsNullOrWhiteSpace(readerName) && AddReader(readerName))
                    {
                        OnUpdatedEndpoints();
                    }

                    break;

                case ActionType.RemoveReader:

                    var requestedReaderToRemove = JsonConvert.DeserializeObject<Reader>(parameters);
                    if (requestedReaderToRemove != null)
                    {
                        var readerToRemove = _configuration.Readers.Find(rdr => rdr.Number == requestedReaderToRemove.Number);
                        if (readerToRemove != null && RemoveReader(readerToRemove))
                        {
                            OnUpdatedEndpoints();
                        }
                    }


                    break;
            }
        }

        return Task.FromResult(string.Empty);
    }

    public bool AddReader(string nameOfReaderToAdd)
    {
        try
        {
            var readerToAdd = new Reader() { Name = nameOfReaderToAdd, Number = GetNextReaderNumber()};
            _configuration.Readers.Add(readerToAdd);
            _endpoints.Add(new VirtualReader(readerToAdd.Name, Id, $"VR{readerToAdd.Number}"));
        } catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception occurred adding a new reader");
            return false;
        }

        return true;
    }


    public byte GetNextReaderNumber()
    {
        if (_configuration.Readers.Count == 0) {  return 1; }
        var maxReader = _configuration.Readers.MaxBy(x => x.Number);
        return (maxReader == null) ? (byte) 1 : (byte)(maxReader.Number + 1);
    }

    public bool RemoveReader(Reader readerToRemove)
    {

        try
        {
            if (readerToRemove == null) return false;   

            _configuration.Readers.Remove(readerToRemove);

            var endPointToRemove = _endpoints.Find(endpoint => endpoint.Id == $"VR{readerToRemove.Number}");
            if (endPointToRemove != null)
            {
                _endpoints.Remove(endPointToRemove);
            }

        } catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception occurred removing a reader");
            return false;
        }

        return true;

    }

    private void ProcessBadgeSwipe(string parameters)
    {
        var badgeAction = JsonConvert.DeserializeObject<BadgeSwipeAction>(parameters);
        if (badgeAction == null) return;

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