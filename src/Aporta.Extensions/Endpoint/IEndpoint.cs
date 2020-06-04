using System;

namespace Aporta.Extensions.Endpoint
{
    public interface IEndpoint : IExtension
    {
        Guid Id { get; }
    }
}