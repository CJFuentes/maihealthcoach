namespace MAIHealthCoach.Application.Exercise;

/// <summary>
/// Estimates the calories burned during a physical activity using the standard MET formula.
/// The service is stateless and has no external dependencies, so it can be unit-tested by
/// constructing it directly and is registered as a Singleton (issue #33). The exercise log
/// (issue #34) consumes it per logged exercise.
/// </summary>
/// <remarks>
/// <para><strong>MET formula</strong></para>
/// <para>kcal = MET × weightKg × durationHours</para>
/// <para>
/// MET (Metabolic Equivalent of Task) is the activity's metabolic rate relative to rest, where
/// 1.0 MET ≈ 1 kcal per kilogram of body weight per hour. Multiplying by body weight in
/// kilograms and duration in decimal hours yields total kilocalories expended.
/// </para>
/// <para><strong>Rounding</strong></para>
/// <para>
/// The raw product is rounded to the nearest integer using
/// <see cref="MidpointRounding.AwayFromZero"/>. Fractional kcal imply false precision given the
/// ±10–20% inherent estimation error of MET-based calculations; integer output is consistent
/// with how <c>GoalsCalculator</c> reports calorie targets.
/// </para>
/// <para><strong>Reference</strong></para>
/// <para>
/// Ainsworth BE et al. (2011). 2011 Compendium of Physical Activities. Med Sci Sports Exerc.
/// </para>
/// </remarks>
public sealed class CaloriesBurnedCalculator
{
    /// <summary>
    /// Estimates calories burned for a single activity session using
    /// <c>kcal = MET × weightKg × durationHours</c>.
    /// </summary>
    /// <param name="metValue">
    /// MET (Metabolic Equivalent of Task) for the activity. Must be greater than zero. Typical
    /// range: 1.0 (slow walking) to ~23.0 (elite sprinting). Standard values are published in
    /// the Compendium of Physical Activities (Ainsworth et al., 2011).
    /// </param>
    /// <param name="weightKg">User body weight in kilograms. Must be greater than zero.</param>
    /// <param name="durationHours">
    /// Duration of the activity in decimal hours (e.g. 30 minutes = 0.5). Must be greater than
    /// zero.
    /// </param>
    /// <returns>Estimated kilocalories burned, rounded to the nearest integer.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="metValue"/>, <paramref name="weightKg"/>, or
    /// <paramref name="durationHours"/> is zero or negative.
    /// </exception>
    /// <example>
    /// A 70 kg person running (MET 9.8) for 30 minutes (0.5 h):
    /// kcal = 9.8 × 70 × 0.5 = 343 kcal.
    /// </example>
    public int EstimateKcal(decimal metValue, double weightKg, double durationHours)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(metValue);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(weightKg);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationHours);

        var raw = (double)metValue * weightKg * durationHours;
        return (int)Math.Round(raw, MidpointRounding.AwayFromZero);
    }
}
