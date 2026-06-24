namespace MAIHealthCoach.Api.Features.Diary;

/// <summary>
/// Request body for <c>POST /api/v1/me/diary</c>. All fields are required.
/// </summary>
/// <param name="FoodItemId">The food to log. Must exist in the database.</param>
/// <param name="ServingSizeId">The serving size to use. Must belong to <paramref name="FoodItemId"/>.</param>
/// <param name="Quantity">Number of servings. Must be greater than zero.</param>
/// <param name="MealType">Meal slot: Breakfast, Lunch, Dinner, or Snack (case-insensitive).</param>
/// <param name="Date">The calendar date in <c>YYYY-MM-DD</c> format. Future dates are permitted.</param>
public record CreateDiaryEntryRequest(
    Guid FoodItemId,
    Guid ServingSizeId,
    decimal Quantity,
    string MealType,
    string Date);

/// <summary>
/// Request body for <c>PUT /api/v1/me/diary/{id}</c>. All fields are required. Changing
/// <see cref="Date"/> or <see cref="MealType"/> implements the "copy/move to another day or
/// meal" acceptance criterion. The entry's food cannot be changed (delete and re-add instead).
/// </summary>
/// <param name="ServingSizeId">Replacement serving. Must belong to the entry's existing food.</param>
/// <param name="Quantity">Replacement quantity. Must be greater than zero.</param>
/// <param name="MealType">Replacement meal slot (Breakfast/Lunch/Dinner/Snack).</param>
/// <param name="Date">Replacement date in <c>YYYY-MM-DD</c> format.</param>
public record UpdateDiaryEntryRequest(
    Guid ServingSizeId,
    decimal Quantity,
    string MealType,
    string Date);

/// <summary>
/// Computed nutrition for a single diary entry, scaled to the grams actually consumed:
/// <c>gramsConsumed = ServingSize.GramsEquivalent * Quantity</c>, then
/// <c>FoodItem.NutritionPer100g.ScaleToGrams(gramsConsumed)</c>. Micros are
/// <see langword="null"/> when the source food did not provide them.
/// </summary>
public record DiaryEntryNutritionResponse(
    decimal EnergyKcal,
    decimal ProteinG,
    decimal CarbohydrateG,
    decimal FatG,
    decimal? SugarsG,
    decimal? FiberG,
    decimal? SaturatedFatG,
    decimal? SodiumMg);

/// <summary>
/// Response body for a single diary entry, including the nutrition computed for the logged
/// quantity and serving.
/// </summary>
/// <param name="Id">Entry internal identifier (UUIDv7).</param>
/// <param name="FoodItemId">The logged food's identifier.</param>
/// <param name="FoodName">Display name of the food.</param>
/// <param name="FoodBrand">Brand of the food, or <see langword="null"/>.</param>
/// <param name="ServingSizeId">The chosen serving's identifier.</param>
/// <param name="ServingLabel">Human-readable serving label (e.g. "1 cup").</param>
/// <param name="ServingGramsEquivalent">Grams represented by one unit of the serving.</param>
/// <param name="Quantity">Number of servings logged.</param>
/// <param name="MealType">Meal slot as a string.</param>
/// <param name="Date">Calendar date of the entry as a <c>YYYY-MM-DD</c> string.</param>
/// <param name="Nutrition">Nutrition scaled to the consumed quantity.</param>
/// <param name="CreatedAt">UTC instant the entry was first created.</param>
/// <param name="UpdatedAt">UTC instant the entry was last modified.</param>
public record DiaryEntryResponse(
    Guid Id,
    Guid FoodItemId,
    string FoodName,
    string? FoodBrand,
    Guid ServingSizeId,
    string ServingLabel,
    decimal ServingGramsEquivalent,
    decimal Quantity,
    string MealType,
    string Date,
    DiaryEntryNutritionResponse Nutrition,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// A group of diary entries for a single meal slot within a day.
/// </summary>
/// <param name="MealType">The meal slot name (Breakfast/Lunch/Dinner/Snack).</param>
/// <param name="Entries">Entries within this meal, ordered by <c>CreatedAt</c> ascending.</param>
public record DiaryMealGroup(
    string MealType,
    IReadOnlyList<DiaryEntryResponse> Entries);

/// <summary>
/// Response body for <c>GET /api/v1/me/diary?date=YYYY-MM-DD</c>. Contains all of the user's
/// diary entries for the requested date, grouped by meal slot in the canonical
/// Breakfast → Lunch → Dinner → Snack order. Meal groups with no entries are omitted.
/// </summary>
/// <param name="Date">The queried date as a <c>YYYY-MM-DD</c> string.</param>
/// <param name="Meals">Entry groups, one per meal slot that has at least one entry.</param>
public record DiaryDayResponse(
    string Date,
    IReadOnlyList<DiaryMealGroup> Meals);
