using IdentityResolution.Core.Models;
using IdentityResolution.Core.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IdentityResolution.Tests.Core.Services;

public class DataNormalizationServiceTests
{
    private readonly DataNormalizationService _service;

    public DataNormalizationServiceTests()
    {
        _service = new DataNormalizationService(NullLogger<DataNormalizationService>.Instance);
    }

    [Fact]
    public void NormalizeName_WithMixedCase_ShouldReturnTitleCase()
    {
        // Arrange
        var input = "jOhN dOe";

        // Act
        var result = _service.NormalizeName(input);

        // Assert
        Assert.Equal("John Doe", result);
    }

    [Fact]
    public void NormalizeName_WithExtraSpaces_ShouldRemoveExtraSpaces()
    {
        // Arrange
        var input = "  John   Doe  ";

        // Act
        var result = _service.NormalizeName(input);

        // Assert
        Assert.Equal("John Doe", result);
    }

    [Fact]
    public void NormalizeEmail_WithMixedCase_ShouldReturnLowerCase()
    {
        // Arrange
        var input = "John.Doe@Example.COM";

        // Act
        var result = _service.NormalizeEmail(input);

        // Assert
        Assert.Equal("john.doe@example.com", result);
    }

    [Fact]
    public void NormalizeEmail_WithInvalidEmail_ShouldReturnEmpty()
    {
        // Arrange
        var input = "not-an-email";

        // Act
        var result = _service.NormalizeEmail(input);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void NormalizePhone_WithUsPhoneNumber_ShouldReturnFormattedPhone()
    {
        // Arrange
        var input = "(555) 123-4567";

        // Act
        var result = _service.NormalizePhone(input);

        // Assert
        Assert.Equal("(555) 123-4567", result);
    }

    [Fact]
    public void NormalizePhone_WithUnformattedUsPhone_ShouldReturnFormattedPhone()
    {
        // Arrange
        var input = "5551234567";

        // Act
        var result = _service.NormalizePhone(input);

        // Assert
        Assert.Equal("(555) 123-4567", result);
    }

    [Fact]
    public void NormalizeIdentity_ShouldNormalizeAllFields()
    {
        // Arrange
        var identity = new Identity
        {
            PersonalInfo = new PersonalInfo
            {
                FirstName = "jOhN",
                LastName = "dOe",
                Gender = "male"
            },
            ContactInfo = new ContactInfo
            {
                Email = "John.Doe@Example.COM",
                Phone = "5551234567"
            },
            Identifiers = new List<Identifier>
            {
                new Identifier(IdentifierTypes.SocialSecurityNumber, "123-45-6789")
            }
        };

        // Act
        var result = _service.NormalizeIdentity(identity);

        // Assert
        Assert.Equal("John", result.PersonalInfo.FirstName);
        Assert.Equal("Doe", result.PersonalInfo.LastName);
        Assert.Equal("MALE", result.PersonalInfo.Gender);
        Assert.Equal("john.doe@example.com", result.ContactInfo.Email);
        Assert.Equal("(555) 123-4567", result.ContactInfo.Phone);
        Assert.Equal("123456789", result.Identifiers[0].Value);
    }
}