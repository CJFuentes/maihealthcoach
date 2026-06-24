using MAIHealthCoach.Domain.Common;

namespace MAIHealthCoach.Domain.Users;

/// <summary>
/// A local application user, provisioned from a Clerk-issued identity on first
/// authenticated request. The local <see cref="EntityBase.Id"/> is the stable internal
/// key used by all other aggregates; <see cref="ClerkUserId"/> is the external subject
/// (the JWT <c>sub</c> claim) used to link the identity provider's user to this row.
/// </summary>
public sealed class User : EntityBase
{
    /// <summary>
    /// The Clerk user identifier — the JWT <c>sub</c> (subject) claim. Unique per user
    /// and stable across sessions; backed by a unique index so a Clerk identity maps to
    /// exactly one local <see cref="User"/>.
    /// </summary>
    public string ClerkUserId { get; private set; } = string.Empty;

    /// <summary>
    /// The user's primary email, taken from the JWT <c>email</c> claim. May be empty when
    /// the token does not carry an email (e.g. some OAuth flows); never <see langword="null"/>.
    /// </summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Parameterless constructor reserved for EF Core materialization.</summary>
    private User()
    {
    }

    /// <summary>
    /// Creates a new <see cref="User"/> linked to a Clerk identity. The internal key and
    /// audit timestamps are assigned here so the entity is fully initialized before it is
    /// added to the change tracker.
    /// </summary>
    /// <param name="clerkUserId">The Clerk subject claim. Required.</param>
    /// <param name="email">The user's email. May be empty but not <see langword="null"/>.</param>
    public static User Create(string clerkUserId, string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clerkUserId);
        ArgumentNullException.ThrowIfNull(email);

        var now = DateTimeOffset.UtcNow;
        return new User
        {
            ClerkUserId = clerkUserId,
            Email = email,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }
}
