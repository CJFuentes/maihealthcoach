namespace MAIHealthCoach.Domain.Food;

/// <summary>
/// A single Open Food Facts-derived serving portion, used to refresh or seed a
/// <see cref="FoodItem"/>'s default serving from an external fetch. This is a transient
/// transport value passed into <see cref="FoodItem.RefreshFromSource"/> (and the equivalent
/// create path); it is <em>not</em> persisted directly — the aggregate translates it into a
/// mapped <see cref="ServingSize"/>.
/// </summary>
/// <remarks>
/// It is distinct from the canonical "100 g" serving every food carries. When OFF provides no
/// usable serving (no positive grams-equivalent, or a serving that merely duplicates the
/// canonical 100 g portion), callers pass <see langword="null"/> rather than an
/// <see cref="OffServing"/> with a zero quantity.
/// </remarks>
/// <param name="Label">Human-readable portion label, e.g. "30 g" or "1 cup (250 g)".</param>
/// <param name="Quantity">Numeric quantity in <paramref name="Unit"/> (e.g. <c>30</c> for "30 g").</param>
/// <param name="Unit">Unit of <paramref name="Quantity"/>, e.g. "g" or "ml".</param>
/// <param name="GramsEquivalent">Mass of the serving in grams. Must be positive to be usable.</param>
public sealed record OffServing(string Label, decimal Quantity, string Unit, decimal GramsEquivalent);
