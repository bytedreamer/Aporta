using Aporta.Extensions.Endpoint;

namespace Aporta.Drivers.IonoPiMax;

/// <summary>
/// A relay endpoint
/// </summary>
public class Relay : IOutput
{
    public Relay(string name, Guid extensionId, string id)
    {
        Name = name;
        ExtensionId = extensionId;
        Id = id;
    }

    /// <inheritdoc/>
    public string Name { get; }
    
    /// <inheritdoc/>
    public Guid ExtensionId { get; }
    
    /// <inheritdoc/>
    public string Id { get; }
    
    /// <inheritdoc/>
    public Task<bool> GetOnlineStatus()
    {
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public async Task<bool> GetState()
    {
        return (await File.ReadAllTextAsync(Id)).Trim() == "1";
    }

    /// <inheritdoc/>
    public async Task SetState(bool state)
    {
        await File.WriteAllTextAsync(Id, state ? "1" : "0");
    }
}