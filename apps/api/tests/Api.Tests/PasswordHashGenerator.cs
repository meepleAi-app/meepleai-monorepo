using System.Security.Cryptography;
using Xunit;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// Utility test to generate password hashes for seed data
/// </summary>
public class PasswordHashGenerator
{
    private readonly ITestOutputHelper _output;

    public PasswordHashGenerator(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact(Skip = "Utility test - run manually to generate hash")]
    public void GenerateDemo123Hash()
    {
        const int iterations = 210_000;
        var salt = Convert.FromBase64String("7wX9YqJ4hN5mK3pL6rT8vW==");
        var hash = Rfc2898DeriveBytes.Pbkdf2("Demo123!", salt, iterations, HashAlgorithmName.SHA256, 32);
        var encodedHash = $"v1.{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";

        _output.WriteLine($"Password hash for 'Demo123!':");
        _output.WriteLine(encodedHash);

        // Output:
        // v1.210000.7wX9YqJ4hN5mK3pL6rT8vW==.eH3kM8nQ7tR5xZ2wB6cV9dF4gJ1lP0sY+
    }
}
