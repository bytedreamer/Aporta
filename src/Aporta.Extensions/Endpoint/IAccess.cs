using System.Threading.Tasks;

namespace Aporta.Extensions.Endpoint;

public interface IAccess : IEndpoint
{
    Task AccessGrantedNotification();
    
    Task AccessDeniedNotification();
}