using System.Text.RegularExpressions;
using IdentityResolution.Core.Models;
using IdentityResolution.Core.Services;
using Microsoft.Extensions.Logging;

namespace IdentityResolution.Core.Services.Implementations;

/// <summary>
/// Basic data normalization service implementation
/// </summary>
public class DataNormalizationService : IDataNormalizationService
{
    private readonly ILogger<DataNormalizationService> _logger;

    public DataNormalizationService(ILogger<DataNormalizationService> logger)
    {
        _logger = logger;
    }

    public Identity NormalizeIdentity(Identity identity)
    {
        // Create a copy to avoid modifying the original
        var normalized = new Identity
        {
            Id = identity.Id,
            CreatedAt = identity.CreatedAt,
            UpdatedAt = identity.UpdatedAt,
            Source = identity.Source,
            Confidence = identity.Confidence,
            PersonalInfo = new PersonalInfo
            {
                FirstName = NormalizeName(identity.PersonalInfo.FirstName),
                MiddleName = NormalizeName(identity.PersonalInfo.MiddleName),
                LastName = NormalizeName(identity.PersonalInfo.LastName),
                FullName = NormalizeName(identity.PersonalInfo.FullName),
                DateOfBirth = identity.PersonalInfo.DateOfBirth,
                Gender = identity.PersonalInfo.Gender?.Trim().ToUpperInvariant(),
                Address = identity.PersonalInfo.Address != null ? new Address
                {
                    Street1 = identity.PersonalInfo.Address.Street1?.Trim(),
                    Street2 = identity.PersonalInfo.Address.Street2?.Trim(),
                    City = identity.PersonalInfo.Address.City?.Trim(),
                    State = identity.PersonalInfo.Address.State?.Trim().ToUpperInvariant(),
                    PostalCode = NormalizePostalCode(identity.PersonalInfo.Address.PostalCode),
                    Country = identity.PersonalInfo.Address.Country?.Trim().ToUpperInvariant()
                } : null
            },
            ContactInfo = new ContactInfo
            {
                Email = NormalizeEmail(identity.ContactInfo.Email),
                Phone = NormalizePhone(identity.ContactInfo.Phone),
                AlternatePhone = NormalizePhone(identity.ContactInfo.AlternatePhone),
                Website = identity.ContactInfo.Website?.Trim().ToLowerInvariant()
            },
            Identifiers = identity.Identifiers.Select(id => new Identifier
            {
                Type = id.Type.Trim().ToUpperInvariant(),
                Value = NormalizeIdentifierValue(id.Value, id.Type),
                Source = id.Source,
                IssuedDate = id.IssuedDate,
                ExpirationDate = id.ExpirationDate
            }).ToList(),
            Attributes = identity.Attributes.ToDictionary(
                kvp => kvp.Key.Trim(),
                kvp => kvp.Value.Trim()
            )
        };

        _logger.LogDebug("Normalized identity {IdentityId}", identity.Id);

        return normalized;
    }

    public string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        // Remove extra whitespace and convert to title case
        var cleaned = Regex.Replace(name.Trim(), @"\s+", " ");

        // Basic title case conversion
        var words = cleaned.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpperInvariant(words[i][0]) +
                          (words[i].Length > 1 ? words[i][1..].ToLowerInvariant() : "");
            }
        }

        return string.Join(" ", words);
    }

    public string NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return string.Empty;

        var normalized = email.Trim().ToLowerInvariant();

        // Basic email validation
        if (!IsValidEmail(normalized))
        {
            _logger.LogWarning("Invalid email format detected");
            return string.Empty;
        }

        return normalized;
    }

    public string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return string.Empty;

        // Remove all non-digit characters
        var digitsOnly = Regex.Replace(phone, @"[^\d]", "");

        // Handle US phone numbers
        if (digitsOnly.Length == 10)
        {
            return $"({digitsOnly[..3]}) {digitsOnly[3..6]}-{digitsOnly[6..]}";
        }
        else if (digitsOnly.Length == 11 && digitsOnly[0] == '1')
        {
            return $"1-({digitsOnly[1..4]}) {digitsOnly[4..7]}-{digitsOnly[7..]}";
        }

        // Return as-is if we can't normalize
        return digitsOnly;
    }

    private string NormalizePostalCode(string? postalCode)
    {
        if (string.IsNullOrWhiteSpace(postalCode))
            return string.Empty;

        var cleaned = postalCode.Trim().ToUpperInvariant();

        // US ZIP code format
        if (Regex.IsMatch(cleaned, @"^\d{5}$") || Regex.IsMatch(cleaned, @"^\d{5}-\d{4}$"))
        {
            return cleaned;
        }

        // Remove spaces for other formats
        return Regex.Replace(cleaned, @"\s", "");
    }

    private string NormalizeIdentifierValue(string value, string type)
    {
        var cleaned = value?.Trim() ?? string.Empty;

        return type.ToUpperInvariant() switch
        {
            IdentifierTypes.SocialSecurityNumber => Regex.Replace(cleaned, @"[^\d]", ""),
            IdentifierTypes.DriversLicense => cleaned.ToUpperInvariant(),
            IdentifierTypes.Passport => cleaned.ToUpperInvariant(),
            _ => cleaned
        };
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
