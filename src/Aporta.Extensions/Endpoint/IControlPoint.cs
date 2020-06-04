using System.Threading.Tasks;

namespace Aporta.Extensions.Endpoint
{
    public interface IControlPoint : IEndpoint
    {
        Task Set(bool state);
    }
}