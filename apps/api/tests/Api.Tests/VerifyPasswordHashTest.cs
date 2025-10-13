using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Api.Tests;

public class VerifyPasswordHashTest
{
    private readonly ITestOutputHelper _output;

    public VerifyPasswordHashTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GeneratePasswordHash_ForDemo123()
    {
        const string password = "Demo123!";
        const int iterations = 210_000;

        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
        var encoded = $"v1.{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";

        _output.WriteLine($"Password: {password}");
        _output.WriteLine($"Hash: {encoded}");

        // Write to file for easy access
        var outputPath = Path.Combine(Path.GetTempPath(), "demo_password_hash.txt");
        File.WriteAllText(outputPath, $"Password: {password}\nHash: {encoded}\n");
        _output.WriteLine($"Written to: {outputPath}");

        // Verify it works
        Assert.True(VerifyPassword(password, encoded));
    }

    [Fact]
    public void VerifySeedDataPasswordHash()
    {
        const string seedHash = "v1.210000.8dE/5q2EBcd0MSLdKi8x6g==.zx114sOsC0WtjeEBN0aYdaqQxbcxWcJfwEbNQ5id1fM=";
        const string password = "Demo123!";

        var result = VerifyPassword(password, seedHash);

        _output.WriteLine($"Password: {password}");
        _output.WriteLine($"Hash: {seedHash}");
        _output.WriteLine($"Verification Result: {result}");

        Assert.True(result, "Seed data password hash should verify correctly for 'Demo123!'");
    }

    private static bool VerifyPassword(string password, string encodedHash)
    {
        try
        {
            var parts = encodedHash.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4 || parts[0] != "v1")
            {
                return false;
            }

            if (!int.TryParse(parts[1], out var iterations))
            {
                return false;
            }

            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);

            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(hash, expected);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
