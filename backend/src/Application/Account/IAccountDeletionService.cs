namespace MAIHealthCoach.Application.Account;

/// <summary>
/// Permanently erases a user and all data they own (issue #46) — the GDPR "right to erasure".
/// </summary>
/// <remarks>
/// The implementation deletes every owned aggregate in foreign-key dependency order within a single
/// transaction: coach messages and conversations, exercise and food diary entries, favorites,
/// the user's custom catalog rows, the health profile and its weight history, goal overrides, the
/// water log, and finally the <c>User</c> row itself. Rows the user authored in the shared catalog
/// (a <see cref="MAIHealthCoach.Domain.Food.FoodItem"/> or
/// <see cref="MAIHealthCoach.Domain.Exercise.ExerciseActivity"/> whose
/// <c>CreatedByUserId</c> equals the user's id) are deleted; shared/seeded rows
/// (<c>CreatedByUserId == null</c>) survive because they are not personal to the user and are
/// referenced by other users. The operation is idempotent: deleting a user that does not exist
/// completes silently without error.
/// </remarks>
public interface IAccountDeletionService
{
    /// <summary>
    /// Permanently deletes the user identified by <paramref name="userId"/> and all data they own,
    /// in foreign-key dependency order, within a single transaction. Idempotent — returns silently
    /// if no user with that id exists.
    /// </summary>
    /// <param name="userId">The internal <c>Users.Id</c> of the user to erase.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task DeleteAccountAsync(Guid userId, CancellationToken cancellationToken = default);
}
