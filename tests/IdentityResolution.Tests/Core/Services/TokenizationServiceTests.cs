using IdentityResolution.Core.Services.Implementations;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IdentityResolution.Tests.Core.Services;

public class TokenizationServiceTests
{
    private readonly TokenizationService _service;

    public TokenizationServiceTests()
    {
        var mockLogger = new Mock<ILogger<TokenizationService>>();
        _service = new TokenizationService(mockLogger.Object);
    }

    [Fact]
    public void TokenizeSSN_WithValidSSN_ShouldReturnConsistentToken()
    {
        // Arrange
        var ssn = "123-45-6789";

        // Act
        var token1 = _service.TokenizeSSN(ssn);
        var token2 = _service.TokenizeSSN(ssn);

        // Assert
        Assert.False(string.IsNullOrEmpty(token1));
        Assert.Equal(token1, token2); // Should be deterministic
    }

    [Fact]
    public void TokenizeSSN_WithDifferentFormats_ShouldReturnSameToken()
    {
        // Arrange
        var ssn1 = "123-45-6789";
        var ssn2 = "123456789";
        var ssn3 = "123 45 6789";

        // Act
        var token1 = _service.TokenizeSSN(ssn1);
        var token2 = _service.TokenizeSSN(ssn2);
        var token3 = _service.TokenizeSSN(ssn3);

        // Assert
        Assert.Equal(token1, token2);
        Assert.Equal(token2, token3);
    }

    [Fact]
    public void TokenizeSSN_WithDifferentSSNs_ShouldReturnDifferentTokens()
    {
        // Arrange
        var ssn1 = "123-45-6789";
        var ssn2 = "987-65-4321";

        // Act
        var token1 = _service.TokenizeSSN(ssn1);
        var token2 = _service.TokenizeSSN(ssn2);

        // Assert
        Assert.NotEqual(token1, token2);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void TokenizeSSN_WithInvalidInput_ShouldReturnEmpty(string? ssn)
    {
        // Act
        var token = _service.TokenizeSSN(ssn!);

        // Assert
        Assert.Equal(string.Empty, token);
    }

    [Fact]
    public void TokenizeSSN_WithInvalidFormat_ShouldReturnEmpty()
    {
        // Arrange
        var invalidSSN = "123-45-678"; // Too short

        // Act
        var token = _service.TokenizeSSN(invalidSSN);

        // Assert
        Assert.Equal(string.Empty, token);
    }

    [Fact]
    public void ValidateSSNToken_WithValidSSNAndToken_ShouldReturnTrue()
    {
        // Arrange
        var ssn = "123-45-6789";
        var token = _service.TokenizeSSN(ssn);

        // Act
        var isValid = _service.ValidateSSNToken(ssn, token);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void ValidateSSNToken_WithDifferentSSN_ShouldReturnFalse()
    {
        // Arrange
        var ssn1 = "123-45-6789";
        var ssn2 = "987-65-4321";
        var token = _service.TokenizeSSN(ssn1);

        // Act
        var isValid = _service.ValidateSSNToken(ssn2, token);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void ValidateSSNToken_WithEmptyInputs_ShouldReturnFalse()
    {
        // Act & Assert
        Assert.False(_service.ValidateSSNToken("", "token"));
        Assert.False(_service.ValidateSSNToken("123-45-6789", ""));
        Assert.False(_service.ValidateSSNToken("", ""));
    }
}
