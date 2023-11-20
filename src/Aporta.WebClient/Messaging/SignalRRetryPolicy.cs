using System;
using Microsoft.AspNetCore.SignalR.Client;

namespace Aporta.WebClient.Messaging;

public class SignalRRetryPolicy : IRetryPolicy
{
    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        return retryContext.PreviousRetryCount switch
        {
            < 5 => TimeSpan.FromSeconds(3),
            < 20 => TimeSpan.FromSeconds(10),
            _ => TimeSpan.FromSeconds(30)
        };
    }
}