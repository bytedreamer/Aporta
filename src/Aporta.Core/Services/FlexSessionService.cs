using System;
using System.Collections.Concurrent;
using Aporta.Core.Models.Flex;

namespace Aporta.Core.Services;

public class FlexSessionService
{
    private readonly ConcurrentDictionary<string, FlexSession> _sessions = new();
    private const int SessionTimeoutMinutes = 30;

    public FlexAuthenticateResult Authenticate(string username, string password)
    {
        // Default credentials — configurable via global settings in the future
        if (username == "admin" && password == "admin")
        {
            var token = Guid.NewGuid().ToString("N");
            _sessions[token] = new FlexSession
            {
                Username = username,
                CreatedAt = DateTime.UtcNow,
            };

            return new FlexAuthenticateResult
            {
                Authenticated = true,
                SessionToken = token,
                SoftwareVersion = "1.0.0",
                TimeZone = TimeZoneInfo.Local.Id,
                ApiVersion = "1.1",
            };
        }

        return new FlexAuthenticateResult
        {
            Authenticated = false,
        };
    }

    public bool ValidateSession(string token)
    {
        if (string.IsNullOrEmpty(token))
            return false;

        if (!_sessions.TryGetValue(token, out var session))
            return false;

        if (DateTime.UtcNow - session.CreatedAt > TimeSpan.FromMinutes(SessionTimeoutMinutes))
        {
            _sessions.TryRemove(token, out _);
            return false;
        }

        // Refresh the session on each use
        session.CreatedAt = DateTime.UtcNow;
        return true;
    }

    public void Terminate(string token)
    {
        if (!string.IsNullOrEmpty(token))
            _sessions.TryRemove(token, out _);
    }

    private class FlexSession
    {
        public string Username { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
