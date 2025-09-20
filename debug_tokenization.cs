using IdentityResolution.Core.Services.Implementations;
using Microsoft.Extensions.Logging;

class Program {
    static void Main() {
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<TokenizationService>();
        var tokenizer = new TokenizationService(logger);
        
        var ssn1 = "123-45-6789";
        var ssn2 = "123456789";
        
        var token1 = tokenizer.TokenizeSSN(ssn1);
        var token2 = tokenizer.TokenizeSSN(ssn2);
        
        Console.WriteLine($"SSN1: {ssn1} -> Token: {token1}");
        Console.WriteLine($"SSN2: {ssn2} -> Token: {token2}");
        Console.WriteLine($"Tokens equal: {token1 == token2}");
    }
}
