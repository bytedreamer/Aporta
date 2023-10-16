using System;
using System.Threading.Tasks;
using Aporta.Extensions.Endpoint;

namespace Aporta.Drivers.TestDriver;

public class TestAccess : IAccess
{
    public string Name { get; internal set; }
        
    public Guid ExtensionId { get; internal set; }
        
    public string Id { get; internal set;}
        
    public Task<bool> GetOnlineStatus()
    {
        throw new NotImplementedException();
    }

    public Task AccessGrantedNotification()
    {
        throw new NotImplementedException();
    }

    public Task AccessDeniedNotification()
    {
        throw new NotImplementedException();
    }
}