using System;
using System.Security.Cryptography;
using System.Text;
using Wakeel.Application.Interfaces;

namespace Wakeel.Infrastructure.Security;

public class Sha256RefreshTokenHasher : IRefreshTokenHasher
{
    public string Hash(string rawToken)
    {
        if (string.IsNullOrEmpty(rawToken))
            throw new ArgumentException("Token cannot be null or empty.", nameof(rawToken));

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}