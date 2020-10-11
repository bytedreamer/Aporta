using System.Threading.Tasks;

namespace Aporta.Extensions.Endpoint
{
    public interface IControlPoint : IEndpoint
    {
        Task<bool> GetState();
        
        Task SetState(bool state);
    }
}