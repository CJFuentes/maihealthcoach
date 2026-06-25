using MAIHealthCoach.Domain.Notifications;
using MAIHealthCoach.Infrastructure.Auth;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAIHealthCoach.Api.Features.Notifications;

/// <summary>
/// Registers the push-device registration endpoints on the supplied versioned route builder (issue
/// #48).
/// <list type="bullet">
///   <item><description><c>POST   /me/devices</c>      — register or refresh a device token; 201/200.</description></item>
///   <item><description><c>GET    /me/devices</c>      — list the caller's registered devices.</description></item>
///   <item><description><c>DELETE /me/devices/{id}</c> — unregister one of the caller's devices.</description></item>
/// </list>
/// All endpoints require authorization and scope every query to the current user's id. Registering a
/// token already owned by another user is a device <em>handoff</em>: the row is reassigned to the
/// caller rather than rejected, resolving the unique-token race the same way <c>ProfileEndpoints</c>
/// resolves its first-create race.
/// </summary>
internal static class DeviceEndpoints
{
    internal static RouteGroupBuilder MapDeviceEndpoints(this RouteGroupBuilder group)
    {
        var devices = group.MapGroup("/me/devices").RequireAuthorization();

        devices.MapPost("/", RegisterDeviceAsync)
            .WithName("RegisterDevice");

        devices.MapGet("/", ListDevicesAsync)
            .WithName("ListDevices");

        devices.MapDelete("/{id:guid}", UnregisterDeviceAsync)
            .WithName("UnregisterDevice");

        return group;
    }

    // ── POST /api/v1/me/devices ───────────────────────────────────────────────────

    private static async Task<IResult> RegisterDeviceAsync(
        RegisterDeviceRequest request,
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        var errors = DeviceValidator.ValidateRegister(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        DeviceValidator.TryParsePlatform(request.Platform, out var platform);

        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        // Same token already registered to this user: refresh its metadata in place.
        var existing = await db.DeviceRegistrations
            .FirstOrDefaultAsync(d => d.Token == request.Token && d.UserId == user.Id, ct);

        if (existing is not null)
        {
            existing.Update(platform, request.Name);
            await db.SaveChangesAsync(ct);
            return Results.Ok(Map(existing));
        }

        var entry = DeviceRegistration.Create(user.Id, request.Token, platform, request.Name);
        db.DeviceRegistrations.Add(entry);

        try
        {
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/me/devices/{entry.Id}", Map(entry));
        }
        catch (DbUpdateException) when (TokenExists(db, request.Token))
        {
            // We lost the unique-token race: another insert for the same token committed between our
            // read and our write. Two sub-cases, both recovered the same way — detach our failed
            // insert, re-read the winning row, and refresh it for the caller (reassigning the owner
            // when it belongs to a different user — a device handoff). Mirrors ProfileEndpoints'
            // race-resolution pattern, but keyed on the token's existence rather than its owner so a
            // same-user concurrent duplicate is recovered too (and never surfaces a 500).
            db.ChangeTracker.Clear();

            var owned = await db.DeviceRegistrations
                .FirstAsync(d => d.Token == request.Token, ct);

            if (owned.UserId != user.Id)
            {
                owned.ReassignTo(user.Id);
            }

            owned.Update(platform, request.Name);

            await db.SaveChangesAsync(ct);
            return Results.Ok(Map(owned));
        }
    }

    // ── GET /api/v1/me/devices ────────────────────────────────────────────────────

    private static async Task<IResult> ListDevicesAsync(
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        var list = await db.DeviceRegistrations
            .AsNoTracking()
            .Where(d => d.UserId == user.Id)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

        return Results.Ok(list.Select(Map).ToList());
    }

    // ── DELETE /api/v1/me/devices/{id} ────────────────────────────────────────────

    private static async Task<IResult> UnregisterDeviceAsync(
        Guid id,
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        // Scope by UserId so another user's device is indistinguishable from a missing one (404).
        var entry = await db.DeviceRegistrations
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == user.Id, ct);

        if (entry is null)
        {
            return Results.Problem(
                title: "Device not found.",
                detail: $"No device with id '{id}' exists for this user.",
                statusCode: StatusCodes.Status404NotFound);
        }

        db.DeviceRegistrations.Remove(entry);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    // Exception filter (must be synchronous) — only swallows DbUpdateException when a row with this
    // token now exists, i.e. we genuinely lost the unique-token race (to a same-user duplicate or a
    // cross-user handoff). Any other DbUpdateException propagates to the global exception handler.
    private static bool TokenExists(AppDbContext db, string token) =>
        db.DeviceRegistrations
            .AsNoTracking()
            .Any(d => d.Token == token);

    private static DeviceResponse Map(DeviceRegistration e) =>
        new(
            Id: e.Id,
            Token: e.Token,
            Platform: e.Platform.ToString(),
            Name: e.Name,
            LastSeenAt: e.LastSeenAt,
            CreatedAt: e.CreatedAt,
            UpdatedAt: e.UpdatedAt);
}
