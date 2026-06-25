using MAIHealthCoach.Domain.Common;

namespace MAIHealthCoach.Domain.Water;

/// <summary>
/// A single water-intake log entry in a user's daily hydration log (issue #31). Links a
/// <see cref="UserId">user</see> to an <see cref="AmountMl">amount of water</see> consumed on a
/// specific <see cref="Date"/>. "Quick-add" presets (250/500 ml, etc.) are simply repeated
/// <see cref="Create"/> calls with the chosen amount — there is no separate preset entity.
/// </summary>
/// <remarks>
/// Amount is stored as an integer number of millilitres, matching the unit of the daily water
/// target computed by the goals engine (<c>GoalsCalculatorOutput.WaterMl</c>), so consumed-vs-goal
/// arithmetic stays integral. The business upper bound (a sane per-entry cap) is enforced in the
/// API validator, not here; the domain only guards the fundamental "positive amount" invariant —
/// mirroring how <c>DiaryEntry</c> delegates its quantity cap to <c>DiaryEntryValidator</c>.
/// </remarks>
public sealed class WaterLogEntry : EntityBase
{
    /// <summary>Foreign key referencing the owning user's <c>Users.Id</c>.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Volume of water consumed, in millilitres. Always positive.</summary>
    public int AmountMl { get; private set; }

    /// <summary>The calendar date the water was logged against. Future dates are permitted.</summary>
    public DateOnly Date { get; private set; }

    /// <summary>Parameterless constructor reserved for EF Core materialization.</summary>
    private WaterLogEntry() { }

    /// <summary>
    /// Creates a new <see cref="WaterLogEntry"/> for the given user, amount, and date. The internal
    /// key and audit timestamps are assigned here so the entity is fully initialized before it is
    /// added to the change tracker.
    /// </summary>
    /// <param name="userId">The owning user's internal <c>Users.Id</c>.</param>
    /// <param name="amountMl">Millilitres of water consumed. Must be positive.</param>
    /// <param name="date">The calendar date of the entry.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="amountMl"/> is zero or negative.
    /// </exception>
    public static WaterLogEntry Create(Guid userId, int amountMl, DateOnly date)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amountMl);

        var now = DateTimeOffset.UtcNow;
        return new WaterLogEntry
        {
            UserId = userId,
            AmountMl = amountMl,
            Date = date,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>
    /// Updates the mutable fields of this entry. Changing <paramref name="date"/> implements the
    /// "move to another day" behaviour.
    /// </summary>
    /// <param name="amountMl">Replacement amount in millilitres. Must be positive.</param>
    /// <param name="date">Replacement date.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="amountMl"/> is zero or negative.
    /// </exception>
    public void Update(int amountMl, DateOnly date)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amountMl);

        AmountMl = amountMl;
        Date = date;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
