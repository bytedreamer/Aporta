using System;
using Aporta.Extensions.Hardware;
using Microsoft.Extensions.Logging;
using PKOC.Net.MessageData;

namespace Aporta.Drivers.OSDP;

/// <summary>
/// Represents a handler for PKOC credentials received by the OSDP reader.
/// </summary>
public class PKOCCredentialHandler : ICredentialReceivedHandler
{
    private readonly string _doorName;
    private readonly ILogger _logger;
    private readonly bool _isValidSignature;

    /// <summary>
    /// Represents a handler for PKOC credentials received by the OSDP reader.
    /// </summary>
    public PKOCCredentialHandler(AuthenticationResponseData data, string doorName, ILogger logger)
    {
        _doorName = doorName;
        _logger = logger;

        MatchingCardData = BitConverter.ToString(data.PublicKey);
        _isValidSignature = data.IsValidSignature();
        
        _logger.LogInformation(
            "Credential with PKOC digital signature of {@CardData} received on door '{Name}'",
            MatchingCardData, doorName);
    }

    /// <inheritdoc />
    public bool IsValid()
    {
        if (_isValidSignature)
        {
            return true;
        }
        else
        {
            _logger.LogWarning("Door '{Name}' PKOC card didn't have a valid digital signature", _doorName);
            return false;
        }
    }

    /// <inheritdoc />
    public string MatchingCardData { get; }
}