using MAIHealthCoach.Domain.Exercise;

namespace MAIHealthCoach.Domain.Tests.Exercise;

/// <summary>
/// Pure-domain tests for the <see cref="ExerciseActivity"/> aggregate root (issue #33) — no EF,
/// no database. These guard the creation invariants (required name, positive MET, ownership) for
/// both seeded shared activities and user-created custom activities.
/// </summary>
public sealed class ExerciseActivityTests
{
    [Fact]
    public void Create_SetsAllFields_WithNullOwner()
    {
        var before = DateTimeOffset.UtcNow;

        var activity = ExerciseActivity.Create("Running", ExerciseCategory.Cardio, 9.8m);

        Assert.Equal("Running", activity.Name);
        Assert.Equal(ExerciseCategory.Cardio, activity.Category);
        Assert.Equal(9.8m, activity.MetValue);
        Assert.Null(activity.CreatedByUserId);
        Assert.NotEqual(Guid.Empty, activity.Id);
        Assert.True(activity.CreatedAt >= before);
        Assert.Equal(activity.CreatedAt, activity.UpdatedAt);
    }

    [Fact]
    public void Create_TrimsName()
    {
        var activity = ExerciseActivity.Create("  Yoga  ", ExerciseCategory.Flexibility, 2.5m);

        Assert.Equal("Yoga", activity.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_ThrowsArgumentException(string name)
    {
        Assert.Throws<ArgumentException>(
            () => ExerciseActivity.Create(name, ExerciseCategory.Cardio, 5.0m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Create_WithNonPositiveMet_ThrowsArgumentOutOfRangeException(double met)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ExerciseActivity.Create("Walking", ExerciseCategory.Cardio, (decimal)met));
    }

    [Fact]
    public void CreateCustom_SetsOwnerAndAllFields()
    {
        var ownerId = Guid.NewGuid();

        var activity = ExerciseActivity.CreateCustom(
            ownerId, "Rock Climbing", ExerciseCategory.Other, 7.5m);

        Assert.Equal(ownerId, activity.CreatedByUserId);
        Assert.Equal("Rock Climbing", activity.Name);
        Assert.Equal(ExerciseCategory.Other, activity.Category);
        Assert.Equal(7.5m, activity.MetValue);
    }

    [Fact]
    public void CreateCustom_TrimsName()
    {
        var activity = ExerciseActivity.CreateCustom(
            Guid.NewGuid(), "  Spinning  ", ExerciseCategory.Cardio, 8.5m);

        Assert.Equal("Spinning", activity.Name);
    }

    [Fact]
    public void CreateCustom_WithEmptyOwner_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => ExerciseActivity.CreateCustom(Guid.Empty, "Hiking", ExerciseCategory.Cardio, 6.0m));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateCustom_WithBlankName_ThrowsArgumentException(string name)
    {
        Assert.Throws<ArgumentException>(
            () => ExerciseActivity.CreateCustom(Guid.NewGuid(), name, ExerciseCategory.Cardio, 5.0m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2.5)]
    public void CreateCustom_WithNonPositiveMet_ThrowsArgumentOutOfRangeException(double met)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ExerciseActivity.CreateCustom(
                Guid.NewGuid(), "Custom", ExerciseCategory.Cardio, (decimal)met));
    }
}
