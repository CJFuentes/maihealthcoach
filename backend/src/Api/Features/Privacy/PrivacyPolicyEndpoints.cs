namespace MAIHealthCoach.Api.Features.Privacy;

/// <summary>
/// Serves the public privacy policy (issue #46) at <c>GET /api/v1/privacy-policy</c> as markdown.
/// The policy is embedded at build time from <c>docs/privacy-policy.md</c> (see the API project's
/// <c>EmbeddedResource</c>) and loaded once into <see cref="PolicyContent"/> at type initialization,
/// so requests serve it from memory with no disk I/O.
/// </summary>
internal static class PrivacyPolicyEndpoints
{
    // Loaded once at type initialization from the embedded resource — the policy never changes at
    // runtime, so a single read avoids per-request I/O.
    private static readonly string PolicyContent = LoadPolicy();

    internal static RouteGroupBuilder MapPrivacyPolicyEndpoints(this RouteGroupBuilder group)
    {
        // Anonymous by design — a privacy policy must be readable without an account, so this
        // endpoint deliberately does NOT call RequireAuthorization.
        group.MapGet(
                "/privacy-policy",
                () => Results.Content(PolicyContent, "text/markdown; charset=utf-8"))
            .WithName("GetPrivacyPolicy");

        return group;
    }

    /// <summary>
    /// Reads the privacy policy from the embedded manifest resource. The logical name must match the
    /// <c>LogicalName</c> set on the <c>EmbeddedResource</c> in <c>MAIHealthCoach.Api.csproj</c>.
    /// </summary>
    private static string LoadPolicy()
    {
        const string resourceName = "MAIHealthCoach.Api.Resources.PrivacyPolicy.md";

        var assembly = typeof(PrivacyPolicyEndpoints).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"The embedded privacy-policy resource '{resourceName}' was not found. Ensure " +
                $"MAIHealthCoach.Api.csproj embeds docs/privacy-policy.md with this exact LogicalName.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
