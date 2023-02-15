using Aporta.Extensions.Endpoint;

namespace Aporta.Drivers.IonoPiMax;

public class Relay : IControlPoint
{
    public Relay(string name, Guid extensionId, string id)
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

    public async Task<bool> GetState()
    {
        return (await File.ReadAllTextAsync(Id)).Trim() == "1";
    }

    public async Task SetState(bool state)
    {
        await File.WriteAllTextAsync(Id, state ? "1" : "0");
    }
}