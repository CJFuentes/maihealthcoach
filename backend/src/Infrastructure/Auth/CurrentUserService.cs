using System.Security.Claims;
using MAIHealthCoach.Domain.Users;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MAIHealthCoach.Infrastructure.Auth;

/// <summary>
/// Get-or-create provisioning of the local <see cref="User"/> from the authenticated
/// request's Clerk claims. Scoped, so it shares the request's <see cref="AppDbContext"/>.
/// </summary>
internal sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _db;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, AppDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    public async Task<User> GetOrCreateCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var principal = _httpContextAccessor.HttpContext?.User
            ?? throw new InvalidOperationException("No active HTTP context for the current request.");

        if (principal.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException("The current request is not authenticated.");
        }

        // 'sub' is the Clerk subject. MapInboundClaims is disabled, so it is not remapped
        // to ClaimTypes.NameIdentifier — read both for safety.
        var clerkUserId = principal.FindFirstValue("sub")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("The authenticated token is missing the 'sub' claim.");

        // Clerk carries the primary email in the 'email' claim. Absent on some flows; the
        // profile can be completed later, so default to empty rather than failing the request.
        var email = principal.FindFirstValue("email")
            ?? principal.FindFirstValue(ClaimTypes.Email)
            ?? string.Empty;

        var existing = await _db.Users
            .FirstOrDefaultAsync(u => u.ClerkUserId == clerkUserId, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var user = User.Create(clerkUserId, email);
        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return user;
        }
        catch (DbUpdateException) when (UserAlreadyExists(clerkUserId))
        {
            // Concurrent first-login race: another request inserted the same ClerkUserId
            // between our read and our write (unique-index violation). Detach our losing
            // insert and return the winner's row instead of surfacing the exception.
            _db.ChangeTracker.Clear();
            return await _db.Users
                .FirstAsync(u => u.ClerkUserId == clerkUserId, cancellationToken);
        }
    }

    // Exception filter: only swallow the DbUpdateException when the row now exists (i.e. we
    // genuinely lost a race). Any other failure propagates to the global exception handler.
    // Filters cannot be async, so this query is synchronous.
    private bool UserAlreadyExists(string clerkUserId) =>
        _db.Users
            .AsNoTracking()
            .Any(u => u.ClerkUserId == clerkUserId);
}
