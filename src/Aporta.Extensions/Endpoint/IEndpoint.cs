using System;
using System.Collections.Generic;

namespace Aporta.Extensions.Endpoint
{
    public interface IEndpoint : IExtension
    {
        Guid ExtensionId { get; }
        
        string Id { get; } 
    }
}