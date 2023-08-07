using Aporta.Extensions.Endpoint;

namespace Aporta.Drivers.Virtual;

/// <summary>
/// An output relay endpoint
/// </summary>
public class VirtualInput : IMonitorPoint
{
    public VirtualInput(string name, Guid extensionId, string id)
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
    public Task<bool> GetState()
    {
        return Task.FromResult(true);
    }
}