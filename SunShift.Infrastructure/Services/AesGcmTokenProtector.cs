using Microsoft.Extensions.Configuration;
using SunShift.Application.Common.Interfaces.Services;
using SunShift.Domain.Common.Constants;
using System.Security.Cryptography;

namespace SunShift.Infrastructure.Services;

public class AesGcmTokenProtector : ITokenProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public AesGcmTokenProtector(IConfiguration configuration)
    {
        string dek = configuration[ConfigurationKeys.SmartThingsTokenDek]
            ?? throw new InvalidOperationException($"Configuration key '{ConfigurationKeys.SmartThingsTokenDek}' is missing.");
        _key = Convert.FromBase64String(dek);
    }

    public string Protect(string plaintext)
    {
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        byte[] ciphertext = new byte[plaintextBytes.Length];
        byte[] tag = new byte[TagSize];

        using AesGcm aesGcm = new(_key, TagSize);
        aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        byte[] result = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSize + TagSize, ciphertext.Length);

        return Convert.ToBase64String(result);
    }

    public string Unprotect(string ciphertext)
    {
        byte[] data = Convert.FromBase64String(ciphertext);

        if (data.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Ciphertext is too short.");
        }

        byte[] nonce = data[..NonceSize];
        byte[] tag = data[NonceSize..(NonceSize + TagSize)];
        byte[] encrypted = data[(NonceSize + TagSize)..];
        byte[] plaintext = new byte[encrypted.Length];

        using AesGcm aesGcm = new(_key, TagSize);
        aesGcm.Decrypt(nonce, encrypted, tag, plaintext);

        return System.Text.Encoding.UTF8.GetString(plaintext);
    }
}
