using MAIHealthCoach.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MAIHealthCoach.Infrastructure.Auth;

/// <summary>
/// Configures the JWT bearer scheme from <see cref="ClerkOptions"/> to validate
/// Clerk-issued RS256 tokens against Clerk's JWKS.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Defensive by design.</strong> There may be no Clerk configuration in a given
/// environment (e.g. local/CI), and the API must still build, start, and keep the
/// anonymous health endpoints green. This configurator therefore branches:
/// </para>
/// <list type="bullet">
///   <item><description>
///   <em>Configured</em> (an <see cref="ClerkOptions.Authority"/> or
///   <see cref="ClerkOptions.JwksUrl"/> is present): wire the authority/issuer/audience so
///   the handler discovers Clerk's signing keys lazily on the first authenticated request.
///   No network call happens at startup.
///   </description></item>
///   <item><description>
///   <em>Unconfigured</em> (both blank): install token-validation parameters with an
///   <strong>empty</strong> signing-key set and no metadata source. The handler then
///   <strong>fails closed</strong> — every bearer token is rejected with <c>401</c> — without
///   ever leaving a <c>null</c> configuration manager that would throw and surface as a
///   <c>500</c>. Anonymous endpoints (health, ping) never enter the handler and are unaffected.
///   </description></item>
/// </list>
/// </remarks>
internal sealed class ClerkJwtBearerConfigureOptions : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly ClerkOptions _clerk;

    public ClerkJwtBearerConfigureOptions(IOptions<ClerkOptions> clerkOptions)
    {
        _clerk = clerkOptions.Value;
    }

    public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);

    public void Configure(string? name, JwtBearerOptions options)
    {
        // Only configure the bearer scheme; ignore any other named options.
        if (name is not null && name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        // Clerk issues 'sub'/'email' claims; keep them verbatim rather than remapping to
        // the long WS-* URIs so claim lookups in CurrentUserService stay simple.
        options.MapInboundClaims = false;

        var configured = !string.IsNullOrWhiteSpace(_clerk.Authority)
            || !string.IsNullOrWhiteSpace(_clerk.JwksUrl);

        if (!configured)
        {
            // Fail closed: no metadata source, no signing keys. Every token is rejected
            // with 401 and the handler never throws (which would become a 500).
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = [],
                NameClaimType = "sub",
                RoleClaimType = "role",
            };
            return;
        }

        // Authority drives OIDC discovery ({Authority}/.well-known/openid-configuration),
        // from which the handler resolves Clerk's jwks_uri and caches signing keys. When
        // only a JwksUrl is supplied (no Authority), point metadata at it directly.
        if (!string.IsNullOrWhiteSpace(_clerk.Authority))
        {
            options.Authority = _clerk.Authority;
            options.RequireHttpsMetadata = _clerk.Authority.StartsWith(
                "https://", StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            options.MetadataAddress = _clerk.JwksUrl;
            options.RequireHttpsMetadata = _clerk.JwksUrl.StartsWith(
                "https://", StringComparison.OrdinalIgnoreCase);
        }

        var issuer = !string.IsNullOrWhiteSpace(_clerk.Issuer)
            ? _clerk.Issuer
            : _clerk.Authority;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrWhiteSpace(issuer),
            ValidIssuer = string.IsNullOrWhiteSpace(issuer) ? null : issuer,
            ValidateAudience = !string.IsNullOrWhiteSpace(_clerk.Audience),
            ValidAudience = string.IsNullOrWhiteSpace(_clerk.Audience) ? null : _clerk.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "sub",
            RoleClaimType = "role",
        };
    }
}
