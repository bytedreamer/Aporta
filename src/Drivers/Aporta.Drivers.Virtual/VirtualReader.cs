using Aporta.Extensions.Endpoint;

namespace Aporta.Drivers.Virtual;

public class VirtualReader : IAccessPoint
{
    public VirtualReader(string name, Guid extensionId, string id)
    {
        Name = name;
        ExtensionId = extensionId;
        Id = id;
    }
    
    public string Name { get; }
    public Guid ExtensionId { get; }
    public string Id { get; }
    
    public Task<bool> GetOnlineStatus()
    {
        return Task.FromResult(true);
    }

    public Task Beep()
    {
        return Task.CompletedTask;
    }
}