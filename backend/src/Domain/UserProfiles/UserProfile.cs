using MAIHealthCoach.Domain.Common;

namespace MAIHealthCoach.Domain.UserProfiles;

/// <summary>
/// Health and preference profile for a local application user. One profile per user;
/// identified by <see cref="UserId"/> (FK to <c>Users.Id</c>), not the profile's own
/// <see cref="EntityBase.Id"/>, which exists for EF navigation purposes.
/// </summary>
/// <remarks>
/// The entity is intentionally sparse on first creation — only <see cref="UserId"/> is
/// required. All health fields are optional nullable values that can be filled in
/// progressively. Use <see cref="Update"/> to apply changes; use
/// <see cref="AddWeightMeasurement"/> to record a new body-weight reading.
/// </remarks>
public sealed class UserProfile : EntityBase
{
    /// <summary>
    /// Foreign key referencing <c>Users.Id</c>. Unique — one profile per user.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Standing height in centimetres. Valid range 50–272. <see langword="null"/> when not set.
    /// </summary>
    public double? HeightCm { get; private set; }

    /// <summary>Date of birth. <see langword="null"/> when not set.</summary>
    public DateOnly? DateOfBirth { get; private set; }

    /// <summary>Biological sex used in metabolic calculations. <see langword="null"/> when not set.</summary>
    public BiologicalSex? BiologicalSex { get; private set; }

    /// <summary>Physical activity level used for TDEE estimation. <see langword="null"/> when not set.</summary>
    public ActivityLevel? ActivityLevel { get; private set; }

    /// <summary>Primary body-composition goal. <see langword="null"/> when not set.</summary>
    public PrimaryGoal? PrimaryGoal { get; private set; }

    /// <summary>
    /// Display unit system preference. Defaults to <see cref="UnitsPreference.Metric"/>;
    /// never <see langword="null"/>.
    /// </summary>
    public UnitsPreference Units { get; private set; } = UnitsPreference.Metric;

    /// <summary>
    /// Optional dietary pattern and allergy information. <see langword="null"/> when the user
    /// has never supplied any dietary data.
    /// </summary>
    public DietaryPreferences? DietaryPreferences { get; private set; }

    // Backing field required so the collection is never null (avoids CS8618 and NullReferenceException).
    private readonly List<WeightMeasurement> _weightMeasurements = new();

    /// <summary>
    /// Chronological weight measurements for this profile. Read-only externally;
    /// mutated only through <see cref="AddWeightMeasurement"/>.
    /// </summary>
    public IReadOnlyCollection<WeightMeasurement> WeightMeasurements => _weightMeasurements;

    /// <summary>
    /// The most recently recorded body weight in kilograms, or <see langword="null"/> if
    /// no measurements have been recorded yet.
    /// </summary>
    public double? LatestWeightKg =>
        _weightMeasurements.Count == 0
            ? null
            : _weightMeasurements.MaxBy(m => m.MeasuredAt)?.WeightKg;

    /// <summary>Parameterless constructor reserved for EF Core materialization.</summary>
    private UserProfile() { }

    /// <summary>
    /// Creates a new, empty <see cref="UserProfile"/> for the given user.
    /// </summary>
    /// <param name="userId">The <c>Users.Id</c> of the owning user. Required.</param>
    public static UserProfile Create(Guid userId)
    {
        var now = DateTimeOffset.UtcNow;
        return new UserProfile
        {
            UserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>
    /// Applies a partial update to the profile. Only non-<see langword="null"/> parameters
    /// overwrite the current value; passing <see langword="null"/> leaves the field unchanged.
    /// <see cref="EntityBase.UpdatedAt"/> is bumped to <see cref="DateTimeOffset.UtcNow"/>
    /// unconditionally when this method is called.
    /// </summary>
    public void Update(
        double? heightCm,
        DateOnly? dateOfBirth,
        BiologicalSex? biologicalSex,
        ActivityLevel? activityLevel,
        PrimaryGoal? primaryGoal,
        UnitsPreference? units,
        DietaryPreferences? dietaryPreferences)
    {
        if (heightCm.HasValue) HeightCm = heightCm.Value;
        if (dateOfBirth.HasValue) DateOfBirth = dateOfBirth.Value;
        if (biologicalSex.HasValue) BiologicalSex = biologicalSex.Value;
        if (activityLevel.HasValue) ActivityLevel = activityLevel.Value;
        if (primaryGoal.HasValue) PrimaryGoal = primaryGoal.Value;
        if (units.HasValue) Units = units.Value;
        if (dietaryPreferences is not null) DietaryPreferences = dietaryPreferences;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Appends a new <see cref="WeightMeasurement"/> only when <paramref name="weightKg"/>
    /// differs from the latest stored weight by at least 0.001 kg (changed-value guard).
    /// This prevents near-duplicate rows from multi-save or repeated PUT with the same value.
    /// </summary>
    /// <param name="weightKg">New body weight in kilograms.</param>
    /// <param name="measuredAt">UTC instant of measurement (server-set in v1).</param>
    /// <returns>
    /// <see langword="true"/> if a new measurement was appended; <see langword="false"/>
    /// if the guard suppressed the append.
    /// </returns>
    public bool AddWeightMeasurement(double weightKg, DateTimeOffset measuredAt)
    {
        if (LatestWeightKg.HasValue && Math.Abs(weightKg - LatestWeightKg.Value) < 0.001)
        {
            return false;
        }

        _weightMeasurements.Add(WeightMeasurement.Create(Id, weightKg, measuredAt));
        UpdatedAt = measuredAt;
        return true;
    }
}
