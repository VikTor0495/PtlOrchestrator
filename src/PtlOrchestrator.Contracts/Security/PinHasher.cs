using System.Security.Cryptography;

namespace PtlOrchestrator.Contracts.Security;

/// <summary>
/// Hash dei PIN operatore (PBKDF2-SHA256 con salt casuale). Vive nei contratti perché il formato
/// fa parte del protocollo di sincronizzazione: il server lo genera, la postazione lo verifica
/// anche senza rete. Formato: <c>v1.{iterazioni}.{salt base64}.{hash base64}</c>.
/// </summary>
public static class PinHasher
{
    private const string Version = "v1";
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public static string Hash(string pin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pin);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Derive(pin, salt, Iterations);

        return string.Join('.', Version, Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(key));
    }

    public static bool Verify(string pin, string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(pin) || !TryParse(storedHash, out var iterations, out var salt, out var expected))
            return false;

        var actual = Derive(pin, salt, iterations);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static bool TryParse(string? storedHash, out int iterations, out byte[] salt, out byte[] key)
    {
        iterations = 0;
        salt = [];
        key = [];

        var parts = storedHash?.Split('.');
        if (parts is not [Version, var iterationsText, var saltText, var keyText])
            return false;

        try
        {
            salt = Convert.FromBase64String(saltText);
            key = Convert.FromBase64String(keyText);
        }
        catch (FormatException)
        {
            return false;
        }

        return int.TryParse(iterationsText, out iterations) && iterations > 0 && key.Length == KeySize;
    }

    private static byte[] Derive(string pin, byte[] salt, int iterations)
        => Rfc2898DeriveBytes.Pbkdf2(pin, salt, iterations, HashAlgorithmName.SHA256, KeySize);
}
