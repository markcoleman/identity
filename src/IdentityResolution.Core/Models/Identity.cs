namespace IdentityResolution.Core.Models;

/// <summary>
/// Represents an identity record with various attributes for matching
/// </summary>
public class Identity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// When this identity record was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this identity record was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Personal information
    /// </summary>
    public PersonalInfo PersonalInfo { get; set; } = new();

    /// <summary>
    /// Contact information
    /// </summary>
    public ContactInfo ContactInfo { get; set; } = new();

    /// <summary>
    /// Various identifiers
    /// </summary>
    public List<Identifier> Identifiers { get; set; } = new();

    /// <summary>
    /// Additional attributes as key-value pairs
    /// </summary>
    public Dictionary<string, string> Attributes { get; set; } = new();

    /// <summary>
    /// Source system or dataset this identity came from
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Confidence score of this identity record (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; } = 1.0;
}

/// <summary>
/// Personal information associated with an identity
/// </summary>
public class PersonalInfo
{
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? FullName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public Address? Address { get; set; }
}

/// <summary>
/// Contact information for an identity
/// </summary>
public class ContactInfo
{
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AlternatePhone { get; set; }
    public string? Website { get; set; }
}

/// <summary>
/// Address information
/// </summary>
public class Address
{
    public string? Street1 { get; set; }
    public string? Street2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }

    public override string ToString()
    {
        var parts = new List<string?> { Street1, Street2, City, State, PostalCode, Country }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(", ", parts);
    }
}

/// <summary>
/// Various types of identifiers
/// </summary>
public class Identifier
{
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Source { get; set; }
    public DateTime? IssuedDate { get; set; }
    public DateTime? ExpirationDate { get; set; }

    public Identifier() { }

    public Identifier(string type, string value, string? source = null)
    {
        Type = type;
        Value = value;
        Source = source;
    }
}

/// <summary>
/// Common identifier types
/// </summary>
public static class IdentifierTypes
{
    public const string SocialSecurityNumber = "SSN";
    public const string DriversLicense = "DL";
    public const string Passport = "PASSPORT";
    public const string NationalId = "NATIONAL_ID";
    public const string TaxId = "TAX_ID";
    public const string EmployeeId = "EMPLOYEE_ID";
    public const string CustomerId = "CUSTOMER_ID";
    public const string AccountNumber = "ACCOUNT_NUMBER";
}
