using System.Threading.Tasks;

namespace Aporta.Extensions.Endpoint;

public interface IMonitorPoint : IEndpoint
{
    Task<bool> GetState();
}