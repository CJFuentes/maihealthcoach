using MAIHealthCoach.Application.Account;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAIHealthCoach.Infrastructure.Account;

/// <summary>
/// EF Core implementation of <see cref="IAccountDeletionService"/> (issue #46). Erases a user and
/// all owned data with set-based <c>ExecuteDeleteAsync</c> calls ordered to satisfy every restrict
/// foreign key, wrapped in a single transaction so a partial failure rolls back cleanly.
/// </summary>
internal sealed class AccountDeletionService : IAccountDeletionService
{
    private readonly AppDbContext _db;

    public AccountDeletionService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task DeleteAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Idempotent: nothing to erase if the user does not exist. Returning here also avoids
        // opening a transaction for a no-op.
        if (!await _db.Users.AnyAsync(u => u.Id == userId, cancellationToken))
        {
            return;
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // ExecuteDeleteAsync issues raw set-based SQL (no change tracking). The order below is
            // bottom-up through the foreign-key graph so each delete runs after its dependents are
            // gone, satisfying the Restrict FKs without relying on EF-side cascade.

            // Coach messages reference both the conversation and the user — delete before either.
            await _db.CoachMessages.Where(m => m.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // Conversations reference the user; safe now that their messages are gone.
            await _db.Conversations.Where(c => c.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // Exercise log entries reference the user (and a catalog activity we keep/delete later).
            await _db.ExerciseLogEntries.Where(e => e.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // Food diary entries reference the user (and a food/serving we keep/delete later).
            await _db.DiaryEntries.Where(e => e.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // This user's own favorite-food join rows; delete before any custom foods they point at.
            await _db.UserFavoriteFoods.Where(f => f.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // Any OTHER user's favorites that point at this user's custom foods. UserFavoriteFood ->
            // FoodItem is ON DELETE CASCADE, so the DB would normally remove these when the custom
            // foods are deleted below — but ExecuteDeleteAsync emits raw SQL and relies on DB-level
            // cascade, which SQLite skips when FK enforcement is off. Delete them explicitly so the
            // food delete never trips a Restrict-style failure and so behaviour is identical across
            // providers. (Custom foods are creator-scoped, so this set is normally empty.)
            await _db.UserFavoriteFoods
                .Where(f => _db.FoodItems
                    .Where(fi => fi.CreatedByUserId == userId)
                    .Select(fi => fi.Id)
                    .Contains(f.FoodItemId))
                .ExecuteDeleteAsync(cancellationToken);

            // Serving sizes of the user's CUSTOM foods. Delete before the foods themselves so the
            // ServingSize -> FoodItem FK is satisfied. Keyed via a subquery over the custom food ids.
            await _db.ServingSizes
                .Where(s => _db.FoodItems
                    .Where(fi => fi.CreatedByUserId == userId)
                    .Select(fi => fi.Id)
                    .Contains(s.FoodItemId))
                .ExecuteDeleteAsync(cancellationToken);

            // The user's CUSTOM foods only (CreatedByUserId == userId). Shared/seeded foods
            // (CreatedByUserId == null) are left intact — they are not personal and are shared.
            await _db.FoodItems.Where(f => f.CreatedByUserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // The user's CUSTOM exercise activities only. Shared/seeded activities survive.
            await _db.ExerciseActivities.Where(a => a.CreatedByUserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // Weight measurements hang off the UserProfile (WeightMeasurement has NO UserId, only
            // UserProfileId). The DB FK is ON DELETE CASCADE, so deleting the profile would normally
            // remove them — but ExecuteDeleteAsync emits raw SQL and relies on DB-level cascade,
            // which SQLite skips when FK enforcement is off. Delete them explicitly first, keyed via
            // a subquery on the profile id, so erasure is correct regardless of FK enforcement.
            await _db.WeightMeasurements
                .Where(w => _db.UserProfiles
                    .Where(p => p.UserId == userId)
                    .Select(p => p.Id)
                    .Contains(w.UserProfileId))
                .ExecuteDeleteAsync(cancellationToken);

            // The health profile itself, now that its weight measurements are gone.
            await _db.UserProfiles.Where(p => p.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // Goal-target overrides reference the user.
            await _db.UserGoalTargets.Where(t => t.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // Water log entries reference the user.
            await _db.WaterLogEntries.Where(e => e.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // Push device registrations reference the user (issue #48).
            await _db.DeviceRegistrations.Where(d => d.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // The user's single push-reminder preferences row (issue #48).
            await _db.ReminderPreferences.Where(r => r.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // Finally the user row, now that nothing references it.
            await _db.Users.Where(u => u.Id == userId)
                .ExecuteDeleteAsync(cancellationToken);

            await tx.CommitAsync(cancellationToken);

            // The set-based deletes bypassed the change tracker; clear it so any entities tracked
            // earlier in this scope's lifetime can't be re-materialized as stale state.
            _db.ChangeTracker.Clear();
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
