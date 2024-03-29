using System.Threading.Tasks;

namespace Aporta.Extensions.Endpoint;

public interface IOutput : IEndpoint
{
    Task<bool> GetState();
        
    Task SetState(bool state);
}