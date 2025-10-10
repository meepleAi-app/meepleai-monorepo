using System.Security.Cryptography;

static string HashPassword(string password)
{
    const int iterations = 210_000;
    var salt = Convert.FromBase64String("7wX9YqJ4hN5mK3pL6rT8vW==");  // Fixed salt for reproducibility
    var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
    return $"v1.{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
}

Console.WriteLine(HashPassword("Demo123!"));
