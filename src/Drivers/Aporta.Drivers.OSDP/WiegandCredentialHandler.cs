using System.Collections;
using System.Text;
using Aporta.Extensions.Hardware;
using Microsoft.Extensions.Logging;

namespace Aporta.Drivers.OSDP;

/// <summary>
/// Represents a handler for Wiegand credentials received.
/// </summary>
public class WiegandCredentialHandler : ICredentialReceivedHandler
{
    private readonly string _doorName;
    private readonly string _matchingCardData;
    private readonly bool _isValid;
    private readonly ILogger _logger;

    /// <summary>
    /// Represents a handler for Wiegand credentials received.
    /// </summary>
    public WiegandCredentialHandler(BitArray cardData, ushort bitCount, string doorName, ILogger logger)
    {
        _doorName = doorName;
        _logger = logger;

        _matchingCardData = BuildRawBitString(cardData);

        _isValid = cardData.Count == bitCount;
        
        _logger.LogInformation(
            "Credential with {BitCount} bits and raw data of {@CardData} received on door '{Name}'",
            bitCount, _matchingCardData, doorName);
    }

    /// <inheritdoc />
    public bool IsValid()
    {
        if (!_isValid)
        {
            _logger.LogWarning("Door '{Name}' card read doesn't match bit count", _doorName);
        }

        return _isValid;
    }

    /// <inheritdoc />
    string ICredentialReceivedHandler.MatchingCardData => _matchingCardData;
    
    private static string BuildRawBitString(BitArray cardData)
    {
        var cardNumberBuilder = new StringBuilder();
        foreach (bool bit in cardData)
        {
            cardNumberBuilder.Append(bit ? "1" : "0");
        }

        return cardNumberBuilder.ToString();
    }
}