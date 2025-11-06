using Aporta.Extensions.Endpoint;

namespace Aporta.Drivers.Virtual;

/// <summary>
/// An output relay endpoint
/// </summary>
public class VirtualInput : IInput
{

	private Action<VirtualInput> SetInputStateInDriver;

	public VirtualInput(string name, Guid extensionId, string id, Action<VirtualInput> SetInputState)
    {
        Name = name;
        ExtensionId = extensionId;
        Id = id;
        this.SetInputStateInDriver = SetInputState;
        
    }

	private bool _state;

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
        return Task.FromResult(_state);
    }

    /// <inheritdoc/>
    public async Task SetState(bool value)
    {
        _state = value;
        SetInputStateInDriver(this);
        return; 
    }
}