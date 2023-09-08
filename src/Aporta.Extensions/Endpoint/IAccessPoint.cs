using System.Threading.Tasks;

namespace Aporta.Extensions.Endpoint;

public interface IAccessPoint : IEndpoint
{
    Task Beep();
}