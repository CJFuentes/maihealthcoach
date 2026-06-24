namespace MAIHealthCoach.Infrastructure.Configuration;

/// <summary>
/// Strongly-typed configuration for Clerk, bound from the <c>Clerk</c> configuration section.
/// These values describe how the API will validate Clerk-issued JWTs in a later milestone
/// (issue #12). No authentication is wired up yet; this class only establishes the config
/// contract so the values can be supplied via appsettings, environment variables
/// (e.g. <c>Clerk__Authority</c>), or .NET user-secrets without any code changes later.
/// </summary>
/// <remarks>
/// None of these values are secret on their own — the Clerk <em>secret key</em> is not held
/// here. Authority/Issuer/JwksUrl/Audience are public metadata used for token validation.
/// </remarks>
public sealed class ClerkOptions
{
    /// <summary>Configuration section name this class binds from.</summary>
    public const string SectionName = "Clerk";

    /// <summary>
    /// The Clerk Frontend API origin that acts as the OpenID Connect authority,
    /// e.g. <c>https://your-instance.clerk.accounts.dev</c>. Used to discover JWKS and
    /// validate the token issuer when JWT validation is wired up.
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// The expected token <c>iss</c> (issuer) claim. When left blank, callers should fall
    /// back to <see cref="Authority"/>, which is the issuer for Clerk-issued tokens.
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// The JSON Web Key Set endpoint used to retrieve signing keys, e.g.
    /// <c>https://your-instance.clerk.accounts.dev/.well-known/jwks.json</c>. When left
    /// blank, callers should derive it from <see cref="Authority"/>.
    /// </summary>
    public string JwksUrl { get; set; } = string.Empty;

    /// <summary>
    /// The expected token <c>aud</c> (audience) claim, if the deployment configures one.
    /// Optional: Clerk session tokens do not always carry an audience.
    /// </summary>
    public string Audience { get; set; } = string.Empty;
}
