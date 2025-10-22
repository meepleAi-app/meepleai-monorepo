using System.Security.Cryptography;

string HashPassword(string password)
{
    const int iterations = 210_000;
    var salt = RandomNumberGenerator.GetBytes(16);
    var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
    return $"v1.{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
}

var password = "Demo123!";

Console.WriteLine($"Password: {password}");
Console.WriteLine();
Console.WriteLine("Admin hash:");
Console.WriteLine(HashPassword(password));
Console.WriteLine();
Console.WriteLine("Editor hash:");
Console.WriteLine(HashPassword(password));
Console.WriteLine();
Console.WriteLine("User hash:");
Console.WriteLine(HashPassword(password));
