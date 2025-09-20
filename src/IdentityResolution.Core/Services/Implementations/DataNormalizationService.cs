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

    public AddressTokens NormalizeAddress(Address? address)
    {
        if (address == null)
            return new AddressTokens();

        var tokens = new AddressTokens();

        // Tokenize street address
        if (!string.IsNullOrWhiteSpace(address.Street1))
        {
            tokens.StreetTokens.AddRange(TokenizeStreetAddress(address.Street1));
        }
        if (!string.IsNullOrWhiteSpace(address.Street2))
        {
            tokens.StreetTokens.AddRange(TokenizeStreetAddress(address.Street2));
        }

        // Normalize other components
        tokens.City = NormalizeCityName(address.City);
        tokens.State = NormalizeStateName(address.State);
        tokens.PostalCode = NormalizePostalCode(address.PostalCode);
        tokens.Country = NormalizeCountryCode(address.Country);

        return tokens;
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

    private List<string> TokenizeStreetAddress(string street)
    {
        var tokens = new List<string>();

        // Clean and normalize the street address
        var cleaned = street.Trim().ToUpperInvariant();

        // Common abbreviation replacements
        cleaned = Regex.Replace(cleaned, @"\bSTREET\b", "ST", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\bAVENUE\b", "AVE", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\bROAD\b", "RD", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\bBOULEVARD\b", "BLVD", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\bDRIVE\b", "DR", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\bCOURT\b", "CT", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\bLANE\b", "LN", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\bPLACE\b", "PL", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\bCIRCLE\b", "CIR", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\bPARKWAY\b", "PKWY", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\bNORTH\b", "N", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\bSOUTH\b", "S", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\bEAST\b", "E", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\bWEST\b", "W", RegexOptions.IgnoreCase);

        // Remove punctuation and split into tokens
        cleaned = Regex.Replace(cleaned, @"[^\w\s]", " ");
        cleaned = Regex.Replace(cleaned, @"\s+", " ");

        tokens.AddRange(cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return tokens;
    }

    private string NormalizeCityName(string? city)
    {
        if (string.IsNullOrWhiteSpace(city))
            return string.Empty;

        return Regex.Replace(city.Trim().ToUpperInvariant(), @"\s+", " ");
    }

    private string NormalizeStateName(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
            return string.Empty;

        var normalized = state.Trim().ToUpperInvariant();

        // Convert common state names to abbreviations
        var stateAbbreviations = new Dictionary<string, string>
        {
            { "ALABAMA", "AL" }, { "ALASKA", "AK" }, { "ARIZONA", "AZ" }, { "ARKANSAS", "AR" },
            { "CALIFORNIA", "CA" }, { "COLORADO", "CO" }, { "CONNECTICUT", "CT" }, { "DELAWARE", "DE" },
            { "FLORIDA", "FL" }, { "GEORGIA", "GA" }, { "HAWAII", "HI" }, { "IDAHO", "ID" },
            { "ILLINOIS", "IL" }, { "INDIANA", "IN" }, { "IOWA", "IA" }, { "KANSAS", "KS" },
            { "KENTUCKY", "KY" }, { "LOUISIANA", "LA" }, { "MAINE", "ME" }, { "MARYLAND", "MD" },
            { "MASSACHUSETTS", "MA" }, { "MICHIGAN", "MI" }, { "MINNESOTA", "MN" }, { "MISSISSIPPI", "MS" },
            { "MISSOURI", "MO" }, { "MONTANA", "MT" }, { "NEBRASKA", "NE" }, { "NEVADA", "NV" },
            { "NEW HAMPSHIRE", "NH" }, { "NEW JERSEY", "NJ" }, { "NEW MEXICO", "NM" }, { "NEW YORK", "NY" },
            { "NORTH CAROLINA", "NC" }, { "NORTH DAKOTA", "ND" }, { "OHIO", "OH" }, { "OKLAHOMA", "OK" },
            { "OREGON", "OR" }, { "PENNSYLVANIA", "PA" }, { "RHODE ISLAND", "RI" }, { "SOUTH CAROLINA", "SC" },
            { "SOUTH DAKOTA", "SD" }, { "TENNESSEE", "TN" }, { "TEXAS", "TX" }, { "UTAH", "UT" },
            { "VERMONT", "VT" }, { "VIRGINIA", "VA" }, { "WASHINGTON", "WA" }, { "WEST VIRGINIA", "WV" },
            { "WISCONSIN", "WI" }, { "WYOMING", "WY" }
        };

        return stateAbbreviations.TryGetValue(normalized, out var abbreviation) ? abbreviation : normalized;
    }

    private string NormalizeCountryCode(string? country)
    {
        if (string.IsNullOrWhiteSpace(country))
            return "US"; // Default to US

        var normalized = country.Trim().ToUpperInvariant();

        // Convert common country names to ISO codes
        return normalized switch
        {
            "UNITED STATES" => "US",
            "UNITED STATES OF AMERICA" => "US",
            "USA" => "US",
            "CANADA" => "CA",
            "MEXICO" => "MX",
            _ => normalized.Length > 2 ? normalized[..2] : normalized
        };
    }
}
