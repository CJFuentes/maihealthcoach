namespace MAIHealthCoach.Domain.Common;

/// <summary>
/// Base type for persistent domain entities. Provides a stable identity and audit
/// timestamps shared by all aggregate roots and entities. This type is deliberately
/// EF-agnostic: persistence concerns (key generation strategy, column mapping,
/// concurrency tokens) are configured in the Infrastructure layer, not here.
/// </summary>
public abstract class EntityBase
{
    /// <summary>
    /// Primary key. Uses a UUIDv7 value, which is time-ordered and therefore
    /// index-friendly (sequential inserts avoid B-tree page fragmentation).
    /// </summary>
    public Guid Id { get; protected set; } = Guid.CreateVersion7();

    /// <summary>UTC instant at which the entity was first persisted.</summary>
    public DateTimeOffset CreatedAt { get; protected set; }

    /// <summary>UTC instant at which the entity was last modified.</summary>
    public DateTimeOffset UpdatedAt { get; protected set; }

    /// <summary>
    /// Parameterless constructor reserved for EF Core materialization when
    /// rehydrating entities from the database.
    /// </summary>
    protected EntityBase() { }
}
