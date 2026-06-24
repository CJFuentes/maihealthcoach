using MAIHealthCoach.Domain.Users;

namespace MAIHealthCoach.Infrastructure.Auth;

/// <summary>
/// Resolves the local <see cref="User"/> for the authenticated request principal,
/// provisioning the row on first login (get-or-create). Scoped to the HTTP request so it
/// shares the request's <c>AppDbContext</c> unit of work.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Returns the local <see cref="User"/> for the current authenticated principal,
    /// creating it from the JWT <c>sub</c>/<c>email</c> claims if it does not yet exist.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when there is no authenticated principal or the token is missing the
    /// <c>sub</c> claim.
    /// </exception>
    Task<User> GetOrCreateCurrentUserAsync(CancellationToken cancellationToken = default);
}
