using MAIHealthCoach.Application.Exercise;

namespace MAIHealthCoach.Application.Tests.Exercise;

/// <summary>
/// Unit tests for <see cref="CaloriesBurnedCalculator"/> (issue #33). The calculator is stateless
/// and dependency-free, so it is constructed directly. These verify the MET formula
/// <c>kcal = MET × weightKg × durationHours</c> against known sample cases and confirm the
/// guard clauses reject non-positive inputs.
/// </summary>
public sealed class CaloriesBurnedCalculatorTests
{
    private readonly CaloriesBurnedCalculator _sut = new();

    // 70 kg running (MET 9.8) for 30 min (0.5 h): 9.8 × 70 × 0.5 = 343.0 → 343.
    [Fact]
    public void EstimateKcal_Running30Min_70kg_Returns343()
    {
        Assert.Equal(343, _sut.EstimateKcal(metValue: 9.8m, weightKg: 70, durationHours: 0.5));
    }

    // 80 kg walking (MET 4.3) for 60 min (1.0 h): 4.3 × 80 × 1.0 = 344.0 → 344.
    [Fact]
    public void EstimateKcal_Walking60Min_80kg_Returns344()
    {
        Assert.Equal(344, _sut.EstimateKcal(metValue: 4.3m, weightKg: 80, durationHours: 1.0));
    }

    // 65 kg yoga (MET 2.5) for 45 min (0.75 h): 2.5 × 65 × 0.75 = 121.875 → 122 (AwayFromZero).
    [Fact]
    public void EstimateKcal_Yoga45Min_65kg_Returns122()
    {
        Assert.Equal(122, _sut.EstimateKcal(metValue: 2.5m, weightKg: 65, durationHours: 0.75));
    }

    // Exact integer result: 2.0 × 60 × 1.25 = 150.0 → 150.
    [Fact]
    public void EstimateKcal_ExactIntegerResult_NoRounding()
    {
        Assert.Equal(150, _sut.EstimateKcal(metValue: 2.0m, weightKg: 60, durationHours: 1.25));
    }

    // Midpoint rounds away from zero: 1.0 × 1 × 0.5 = 0.5 → 1.
    [Fact]
    public void EstimateKcal_MidpointHalf_RoundsAwayFromZero()
    {
        Assert.Equal(1, _sut.EstimateKcal(metValue: 1.0m, weightKg: 1, durationHours: 0.5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EstimateKcal_NonPositiveMet_ThrowsArgumentOutOfRangeException(double met)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _sut.EstimateKcal((decimal)met, weightKg: 70, durationHours: 1.0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    public void EstimateKcal_NonPositiveWeight_ThrowsArgumentOutOfRangeException(double weight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _sut.EstimateKcal(metValue: 5.0m, weightKg: weight, durationHours: 1.0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    public void EstimateKcal_NonPositiveDuration_ThrowsArgumentOutOfRangeException(double hours)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _sut.EstimateKcal(metValue: 5.0m, weightKg: 70, durationHours: hours));
    }
}
