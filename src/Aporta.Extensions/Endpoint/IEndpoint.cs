using System;

namespace Aporta.Extensions.Endpoint
{
    public interface IEndpoint : IExtension
    {
        int Id { get; }
    }
}