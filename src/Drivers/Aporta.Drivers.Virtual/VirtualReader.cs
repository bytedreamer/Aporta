using Aporta.Extensions.Endpoint;

namespace Aporta.Drivers.Virtual;

public class VirtualReader : IAccess
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

    public Task AccessGrantedNotification()
    {
        return Task.CompletedTask;
    }

    public Task AccessDeniedNotification()
    {
        return Task.CompletedTask;
    }
}