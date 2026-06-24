using MAIHealthCoach.Domain.Common;

namespace MAIHealthCoach.Domain.Food;

/// <summary>
/// A user's "favorite" marker for a <see cref="FoodItem"/> — a many-to-many association row
/// linking a user to a food they have starred for quick re-logging (issue #24). The pairing
/// (<see cref="UserId"/>, <see cref="FoodItemId"/>) is unique: a user can favorite a given food
/// at most once. Extends <see cref="EntityBase"/> for its own stable <c>Id</c> and audit
/// timestamps; <see cref="EntityBase.CreatedAt"/> drives the most-recent-first list ordering.
/// </summary>
/// <remarks>
/// This is a deliberately thin join entity with no navigation properties: the favorites list is
/// resolved by reading the food ids here and loading the corresponding <see cref="FoodItem"/>
/// rows separately. The food it references may be a shared Open Food Facts food or the user's own
/// custom food; visibility is enforced at the API layer when a favorite is created.
/// </remarks>
public sealed class UserFavoriteFood : EntityBase
{
    /// <summary>Foreign key to the <c>User</c> who favorited the food.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Foreign key to the favorited <see cref="FoodItem"/>.</summary>
    public Guid FoodItemId { get; private set; }

    /// <summary>Parameterless constructor reserved for EF Core materialization.</summary>
    private UserFavoriteFood() { }

    /// <summary>
    /// Creates a favorite association between a user and a food. The internal key and audit
    /// timestamps are assigned here so the entity is fully initialized before it is added to the
    /// change tracker.
    /// </summary>
    /// <param name="userId">The favoriting user's id. Required and must be non-empty.</param>
    /// <param name="foodItemId">The favorited food's id. Required and must be non-empty.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="userId"/> or <paramref name="foodItemId"/> is empty.
    /// </exception>
    public static UserFavoriteFood Create(Guid userId, Guid foodItemId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A favorite must reference a non-empty user id.", nameof(userId));
        }

        if (foodItemId == Guid.Empty)
        {
            throw new ArgumentException("A favorite must reference a non-empty food id.", nameof(foodItemId));
        }

        var now = DateTimeOffset.UtcNow;
        return new UserFavoriteFood
        {
            UserId = userId,
            FoodItemId = foodItemId,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }
}
