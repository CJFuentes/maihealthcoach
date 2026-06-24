namespace MAIHealthCoach.Api.Features.Foods;

/// <summary>
/// Pure-static validator for the <c>GET /api/v1/foods</c> search query parameters. Returns a
/// dictionary of field errors keyed by query-parameter name, compatible with
/// <c>Results.ValidationProblem(errors)</c>. Never throws; an empty dictionary means the request is
/// valid.
/// </summary>
internal static class FoodSearchValidator
{
    private const int MaxQueryLength = 200;
    private const int MinPageSize = 1;
    private const int MaxPageSize = 50;

    /// <summary>
    /// Validates the search parameters and returns a dictionary of field errors. An empty
    /// dictionary means the request is valid.
    /// </summary>
    /// <param name="q">The free-text query (required, non-blank, max 200 chars).</param>
    /// <param name="page">The 1-based page (must be &gt;= 1).</param>
    /// <param name="pageSize">Optional page-size cap; when supplied must be between 1 and 50.</param>
    internal static IDictionary<string, string[]> Validate(string? q, int page, int? pageSize)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(q))
        {
            errors["q"] = ["A search query (q) is required."];
        }
        else if (q.Trim().Length > MaxQueryLength)
        {
            errors["q"] = [$"Search query (q) must not exceed {MaxQueryLength} characters."];
        }

        if (page < 1)
        {
            errors["page"] = ["Page must be 1 or greater."];
        }

        if (pageSize is { } size && size is < MinPageSize or > MaxPageSize)
        {
            errors["pageSize"] = [$"Page size must be between {MinPageSize} and {MaxPageSize}."];
        }

        return errors;
    }
}
