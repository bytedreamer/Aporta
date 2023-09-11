using System.Threading.Tasks;

namespace Aporta.Extensions.Endpoint;

public interface IInput : IEndpoint
{
    Task<bool> GetState();
}