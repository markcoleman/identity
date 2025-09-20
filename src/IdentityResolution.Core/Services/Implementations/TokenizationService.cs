using System.Security.Cryptography;
using System.Text;
using IdentityResolution.Core.Services;
using Microsoft.Extensions.Logging;

namespace IdentityResolution.Core.Services.Implementations;

/// <summary>
/// Tokenization service for secure handling of sensitive identifiers
/// </summary>
public class TokenizationService : ITokenizationService
{
    private readonly ILogger<TokenizationService> _logger;
    private readonly string _salt = "IdentityResolution-SSN-Salt-2024"; // In production, use proper key management

    public TokenizationService(ILogger<TokenizationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generate a deterministic token for an SSN using SHA256 with salt
    /// </summary>
    /// <param name="ssn">The SSN to tokenize</param>
    /// <returns>A deterministic token that can be used for matching</returns>
    public string TokenizeSSN(string ssn)
    {
        if (string.IsNullOrWhiteSpace(ssn))
        {
            return string.Empty;
        }

        // Clean SSN - remove all non-digits
        var cleanedSSN = System.Text.RegularExpressions.Regex.Replace(ssn, @"[^\d]", "");
        
        if (cleanedSSN.Length != 9)
        {
            _logger.LogWarning("Invalid SSN format provided for tokenization");
            return string.Empty;
        }

        // Combine with salt for security
        var input = cleanedSSN + _salt;
        
        // Generate deterministic hash
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        
        // Convert to base64 for storage/comparison
        var token = Convert.ToBase64String(hashBytes);
        
        _logger.LogDebug("Generated SSN token");
        return token;
    }

    /// <summary>
    /// Validate if a token matches an SSN
    /// </summary>
    /// <param name="ssn">The SSN to validate</param>
    /// <param name="token">The token to validate against</param>
    /// <returns>True if the token matches the SSN</returns>
    public bool ValidateSSNToken(string ssn, string token)
    {
        if (string.IsNullOrWhiteSpace(ssn) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var generatedToken = TokenizeSSN(ssn);
        return string.Equals(generatedToken, token, StringComparison.Ordinal);
    }
}