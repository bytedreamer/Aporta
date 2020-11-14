using System;
using System.Threading.Tasks;

namespace Aporta.Extensions.Endpoint
{
    public interface IEndpoint : IExtension
    {
        Guid ExtensionId { get; }
        
        string Id { get; }

        Task<bool> GetOnlineStatus();
    }
}