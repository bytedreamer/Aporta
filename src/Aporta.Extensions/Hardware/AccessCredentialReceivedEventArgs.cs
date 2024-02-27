using System;
using Aporta.Extensions.Endpoint;

namespace Aporta.Extensions.Hardware;

/// <summary>
/// Represents the event arguments for when an access credential is received.
/// </summary>
public class AccessCredentialReceivedEventArgs : EventArgs
{
    /// <summary>
    /// Represents the event arguments for when an access credential is received.
    /// </summary>
    public AccessCredentialReceivedEventArgs(IAccess access, ICredentialReceivedHandler handler)
    {
        Access = access;
        Handler = handler;
    }

    /// <summary>
    /// The access hardware that handled the card read.
    /// </summary>
    public IAccess Access { get; }

    /// <summary>
    /// Implementation of an interface that determines if access should be granted.
    /// </summary>
    public ICredentialReceivedHandler Handler { get; }
}

/// <summary>
/// Represents the interface for handling received credentials that determines if access should be granted.
/// </summary>
public interface ICredentialReceivedHandler
{
    /// <summary>
    /// Check if the received credentials are valid.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the credentials are valid; otherwise, <c>false</c>.
    /// </returns>
    public bool IsValid();

    /// <summary>
    /// Represents a card data to match with enrolled credential record.
    /// </summary>
    public string MatchingCardData { get; }
}