using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace MAIHealthCoach.Api.Tests.Auth;

/// <summary>
/// Mints locally-signed RS256 JWTs for auth integration tests. The matching public key is
/// injected into the test host's <c>JwtBearerOptions</c> so the real
/// <c>JwtBearerHandler</c> validation pipeline runs end-to-end without ever contacting
/// Clerk's JWKS endpoint.
/// </summary>
internal static class JwtTestHelper
{
    internal const string TestIssuer = "https://test.clerk.local";

    /// <summary>
    /// Process-wide RSA key pair. The private key signs test tokens; the public key is
    /// handed to the test host as the issuer signing key. Stable for the test run so a
    /// token minted here validates against the key the host trusts.
    /// </summary>
    private static readonly RSA Rsa = RSA.Create(2048);

    /// <summary>The public signing key the test host must trust to validate test tokens.</summary>
    internal static SecurityKey PublicSigningKey { get; } =
        new RsaSecurityKey(Rsa.ExportParameters(includePrivateParameters: false))
        {
            KeyId = "test-key-1",
        };

    private static SigningCredentials SigningCredentials { get; } =
        new(
            new RsaSecurityKey(Rsa) { KeyId = "test-key-1" },
            SecurityAlgorithms.RsaSha256);

    /// <summary>
    /// Creates a signed JWT carrying the given <paramref name="sub"/> and
    /// <paramref name="email"/> claims, valid for five minutes (or already expired when
    /// <paramref name="expired"/> is <see langword="true"/>).
    /// </summary>
    internal static string CreateToken(
        string sub,
        string email,
        bool expired = false)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim> { new("sub", sub) };
        if (!string.IsNullOrEmpty(email))
        {
            claims.Add(new Claim("email", email));
        }

        // For an expired token, anchor issued/not-before in the past too so the descriptor
        // stays internally consistent (Expires must be after NotBefore).
        var issuedAt = expired ? now.AddHours(-2) : now;
        var expires = expired ? now.AddHours(-1) : now.AddMinutes(5);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = TestIssuer,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expires,
            SigningCredentials = SigningCredentials,
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.CreateEncodedJwt(descriptor);
    }
}
