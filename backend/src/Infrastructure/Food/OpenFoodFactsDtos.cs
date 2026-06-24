using System.Text.Json;
using System.Text.Json.Serialization;

namespace MAIHealthCoach.Infrastructure.Food;

/// <summary>
/// Response envelope for <c>GET api/v2/product/{barcode}.json</c>. Open Food Facts reports
/// <c>status == 1</c> when the product was found and <c>status == 0</c> when it was not.
/// </summary>
internal sealed class OffProductResponse
{
    /// <summary>1 when the product was found, 0 when not found.</summary>
    [JsonPropertyName("status")]
    public int Status { get; init; }

    /// <summary>Human-readable status (e.g. "product found").</summary>
    [JsonPropertyName("status_verbose")]
    public string? StatusVerbose { get; init; }

    /// <summary>The requested product code, echoed back.</summary>
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    /// <summary>The product payload when found; otherwise <see langword="null"/>.</summary>
    [JsonPropertyName("product")]
    public OffProduct? Product { get; init; }
}

/// <summary>Response envelope for <c>GET api/v2/search</c>.</summary>
internal sealed class OffSearchResponse
{
    /// <summary>Total number of matching products across all pages.</summary>
    [JsonPropertyName("count")]
    public int Count { get; init; }

    /// <summary>The 1-based page number this response represents.</summary>
    [JsonPropertyName("page")]
    public int Page { get; init; }

    /// <summary>The products on this page; <see langword="null"/> when absent.</summary>
    [JsonPropertyName("products")]
    public List<OffProduct>? Products { get; init; }
}

/// <summary>
/// A single Open Food Facts product. Tolerant by design: <see cref="ServingQuantity"/> and the
/// nutriment values may arrive as JSON numbers <em>or</em> strings, so they are captured as raw
/// <see cref="JsonElement"/> values and parsed defensively by the mapper.
/// </summary>
internal sealed class OffProduct
{
    /// <summary>The product barcode/code.</summary>
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    /// <summary>The product display name.</summary>
    [JsonPropertyName("product_name")]
    public string? ProductName { get; init; }

    /// <summary>The brand(s), often a comma-separated list.</summary>
    [JsonPropertyName("brands")]
    public string? Brands { get; init; }

    /// <summary>The raw serving-size string, e.g. "30 g" or "1 cup (250 g)".</summary>
    [JsonPropertyName("serving_size")]
    public string? ServingSize { get; init; }

    /// <summary>Serving quantity; may be a JSON number or string. Parsed defensively.</summary>
    [JsonPropertyName("serving_quantity")]
    public JsonElement ServingQuantity { get; init; }

    /// <summary>
    /// The nutriment map keyed by OFF nutriment field (e.g. "energy-kcal_100g"); each value may be
    /// a JSON number or string. Parsed defensively by the mapper.
    /// </summary>
    [JsonPropertyName("nutriments")]
    public Dictionary<string, JsonElement>? Nutriments { get; init; }
}
