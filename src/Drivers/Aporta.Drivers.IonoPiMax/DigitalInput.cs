using Aporta.Extensions.Endpoint;

namespace Aporta.Drivers.IonoPiMax;

/// <summary>
/// A relay endpoint
/// </summary>
public class DigitalInput : IInput
{
    public DigitalInput(string name, Guid extensionId, string id)
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
        return (await File.ReadAllTextAsync(Id).ConfigureAwait(false)).Trim() == "1";
    }

    /// <inheritdoc/>
    public async Task SetState(bool value)
    {
        await File.WriteAllTextAsync(Id, value ? "1" : "0").ConfigureAwait(false);
    }
}