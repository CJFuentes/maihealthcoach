using MAIHealthCoach.Domain.UserProfiles;

namespace MAIHealthCoach.Api.Features.Profile;

/// <summary>
/// Pure-static validator for <see cref="UpdateProfileRequest"/>. Returns a dictionary of
/// field errors keyed by field name (camelCase), compatible with
/// <c>Results.ValidationProblem(errors)</c>. Never throws; unknown enum string values
/// produce field errors rather than exceptions.
/// </summary>
internal static class ProfileValidator
{
    // Reference date for age calculations. Using DateOnly.FromDateTime(DateTime.UtcNow)
    // so the calculation is deterministic relative to server UTC.
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// Validates <paramref name="request"/> and returns a dictionary of field errors.
    /// An empty dictionary means the request is valid.
    /// </summary>
    internal static IDictionary<string, string[]> Validate(UpdateProfileRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (request.HeightCm.HasValue)
        {
            if (request.HeightCm.Value is < 50 or > 272)
            {
                errors["heightCm"] = ["Height must be between 50 and 272 cm."];
            }
        }

        if (request.WeightKg.HasValue)
        {
            if (request.WeightKg.Value is < 20 or > 500)
            {
                errors["weightKg"] = ["Weight must be between 20 and 500 kg."];
            }
        }

        if (request.DateOfBirth.HasValue)
        {
            var dob = request.DateOfBirth.Value;
            var today = Today;
            var age = today.Year - dob.Year;
            if (dob > today.AddYears(-age)) age--;

            if (age < 13)
            {
                errors["dateOfBirth"] = ["User must be at least 13 years old."];
            }
            else if (age > 120)
            {
                errors["dateOfBirth"] = ["Date of birth is implausibly far in the past (> 120 years ago)."];
            }
        }

        if (request.BiologicalSex is not null
            && !Enum.TryParse<BiologicalSex>(request.BiologicalSex, ignoreCase: true, out _))
        {
            errors["biologicalSex"] =
            [
                $"'{request.BiologicalSex}' is not a valid value for biologicalSex. " +
                $"Accepted values: {string.Join(", ", Enum.GetNames<BiologicalSex>())}.",
            ];
        }

        if (request.ActivityLevel is not null
            && !Enum.TryParse<ActivityLevel>(request.ActivityLevel, ignoreCase: true, out _))
        {
            errors["activityLevel"] =
            [
                $"'{request.ActivityLevel}' is not a valid value for activityLevel. " +
                $"Accepted values: {string.Join(", ", Enum.GetNames<ActivityLevel>())}.",
            ];
        }

        if (request.PrimaryGoal is not null
            && !Enum.TryParse<PrimaryGoal>(request.PrimaryGoal, ignoreCase: true, out _))
        {
            errors["primaryGoal"] =
            [
                $"'{request.PrimaryGoal}' is not a valid value for primaryGoal. " +
                $"Accepted values: {string.Join(", ", Enum.GetNames<PrimaryGoal>())}.",
            ];
        }

        if (request.Units is not null
            && !Enum.TryParse<UnitsPreference>(request.Units, ignoreCase: true, out _))
        {
            errors["units"] =
            [
                $"'{request.Units}' is not a valid value for units. " +
                $"Accepted values: {string.Join(", ", Enum.GetNames<UnitsPreference>())}.",
            ];
        }

        if (request.DietType is not null
            && !Enum.TryParse<DietType>(request.DietType, ignoreCase: true, out _))
        {
            errors["dietType"] =
            [
                $"'{request.DietType}' is not a valid value for dietType. " +
                $"Accepted values: {string.Join(", ", Enum.GetNames<DietType>())}.",
            ];
        }

        if (request.Allergies is not null && request.Allergies.Length > 1024)
        {
            errors["allergies"] = ["Allergies text must not exceed 1 024 characters."];
        }

        return errors;
    }
}
