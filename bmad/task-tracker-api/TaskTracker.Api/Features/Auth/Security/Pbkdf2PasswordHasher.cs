using System.Security.Cryptography;

namespace TaskTracker.Api.Features.Auth.Security;

public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public (string Hash, string Salt) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public bool Verify(string password, string storedHash, string storedSalt)
    {
        byte[] hashBytes;
        byte[] saltBytes;

        try
        {
            hashBytes = Convert.FromBase64String(storedHash);
            saltBytes = Convert.FromBase64String(storedSalt);
        }
        catch (FormatException)
        {
            return false;
        }

        var computedHash = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, Iterations, HashAlgorithmName.SHA256, hashBytes.Length);

        return CryptographicOperations.FixedTimeEquals(hashBytes, computedHash);
    }
}