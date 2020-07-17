using System;
using System.Threading.Tasks;

namespace Aporta.Extensions.Endpoint
{
    public interface IControlPoint : IEndpoint
    {
        Task<bool> Get();
        
        Task Set(bool state);
        
        event EventHandler<bool> ControlPointStateChanged;
    }
}