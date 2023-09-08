using System;
using System.Threading.Tasks;
using Aporta.Extensions.Endpoint;

namespace Aporta.Drivers.TestDriver;

public class TestAccessPoint : IAccessPoint
{
    public string Name { get; internal set; }
        
    public Guid ExtensionId { get; internal set; }
        
    public string Id { get; internal set;}
        
    public Task<bool> GetOnlineStatus()
    {
        throw new NotImplementedException();
    }

    public Task Beep()
    {
        throw new NotImplementedException();
    }
}