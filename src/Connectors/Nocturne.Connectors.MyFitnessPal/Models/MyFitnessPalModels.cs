using System.Text.Json.Serialization;

namespace Nocturne.Connectors.MyFitnessPal.Models;

/// <summary>
/// Represents nutritional contents from MyFitnessPal
/// </summary>
public class NutritionalContents
{
    [JsonPropertyName("energy")]
    public Energy Energy { get; set; } = new();

    [JsonPropertyName("fat")]
    public double? Fat { get; set; }

    [JsonPropertyName("saturated_fat")]
    public double? SaturatedFat { get; set; }

    [JsonPropertyName("polyunsaturated_fat")]
    public double? PolyunsaturatedFat { get; set; }

    [JsonPropertyName("monounsaturated_fat")]
    public double? MonounsaturatedFat { get; set; }

    [JsonPropertyName("trans_fat")]
    public double? TransFat { get; set; }

    [JsonPropertyName("cholesterol")]
    public double? Cholesterol { get; set; }

    [JsonPropertyName("sodium")]
    public double? Sodium { get; set; }

    [JsonPropertyName("potassium")]
    public double? Potassium { get; set; }

    [JsonPropertyName("carbohydrates")]
    public double? Carbohydrates { get; set; }

    [JsonPropertyName("fiber")]
    public double? Fiber { get; set; }

    [JsonPropertyName("sugar")]
    public double? Sugar { get; set; }

    [JsonPropertyName("protein")]
    public double? Protein { get; set; }

    [JsonPropertyName("vitamin_a")]
    public double? VitaminA { get; set; }

    [JsonPropertyName("vitamin_c")]
    public double? VitaminC { get; set; }

    [JsonPropertyName("calcium")]
    public double? Calcium { get; set; }

    [JsonPropertyName("iron")]
    public double? Iron { get; set; }

    [JsonPropertyName("added_sugars")]
    public double? AddedSugars { get; set; }

    [JsonPropertyName("vitamin_d")]
    public double? VitaminD { get; set; }

    [JsonPropertyName("sugar_alcohols")]
    public double? SugarAlcohols { get; set; }
}

/// <summary>
/// Represents energy information from MyFitnessPal
/// </summary>
public class Energy
{
    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public double Value { get; set; }
}

/// <summary>
/// Represents serving size information from MyFitnessPal
/// </summary>
public class ServingSize
{
    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;

    [JsonPropertyName("nutrition_multiplier")]
    public double NutritionMultiplier { get; set; }
}

/// <summary>
/// Represents food information from MyFitnessPal
/// </summary>
public class MyFitnessPalFood
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("brand_name")]
    public string BrandName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("public")]
    public bool Public { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("verified")]
    public bool Verified { get; set; }

    [JsonPropertyName("serving_sizes")]
    public List<ServingSize> ServingSizes { get; set; } = new();

    [JsonPropertyName("nutritional_contents")]
    public NutritionalContents NutritionalContents { get; set; } = new();
}

/// <summary>
/// Represents a food entry from MyFitnessPal
/// </summary>
public class FoodEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("client_id")]
    public string? ClientId { get; set; }

    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("meal_name")]
    public string MealName { get; set; } = string.Empty;

    [JsonPropertyName("meal_position")]
    public int MealPosition { get; set; }

    [JsonPropertyName("food")]
    public MyFitnessPalFood Food { get; set; } = new();

    [JsonPropertyName("serving_size")]
    public ServingSize ServingSize { get; set; } = new();

    [JsonPropertyName("servings")]
    public double Servings { get; set; }

    [JsonPropertyName("meal_food_id")]
    public string MealFoodId { get; set; } = string.Empty;

    [JsonPropertyName("nutritional_contents")]
    public NutritionalContents NutritionalContents { get; set; } = new();

    [JsonPropertyName("geolocation")]
    public Dictionary<string, object> Geolocation { get; set; } = new();

    [JsonPropertyName("image_ids")]
    public List<string> ImageIds { get; set; } = new();

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("consumed_at")]
    public string? ConsumedAt { get; set; }

    [JsonPropertyName("logged_at")]
    public string? LoggedAt { get; set; }

    [JsonPropertyName("logged_at_offset")]
    public string? LoggedAtOffset { get; set; }
}

/// <summary>
/// Represents a diary entry from MyFitnessPal
/// </summary>
public class DiaryEntry
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("food_entries")]
    public List<FoodEntry> FoodEntries { get; set; } = new();

    [JsonPropertyName("exercise_entries")]
    public List<ExerciseEntry> ExerciseEntries { get; set; } = new();
}

/// <summary>
/// Represents the response from MyFitnessPal diary API
/// </summary>
public class DiaryResponse : List<DiaryEntry> { }

/// <summary>
/// Represents exercise information from MyFitnessPal
/// </summary>
public class Exercise
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("mets")]
    public double Mets { get; set; }

    [JsonPropertyName("mets_double")]
    public double MetsDouble { get; set; }

    [JsonPropertyName("public")]
    public bool Public { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Represents exercise content data from MyFitnessPal
/// </summary>
public class ExerciseContent
{
    [JsonPropertyName("data")]
    public Dictionary<string, object> Data { get; set; } = new();

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Represents an exercise entry from MyFitnessPal
/// </summary>
public class ExerciseEntry
{
    [JsonPropertyName("duration")]
    public int Duration { get; set; }

    [JsonPropertyName("tags")]
    public List<object> Tags { get; set; } = new();

    [JsonPropertyName("energy")]
    public Energy Energy { get; set; } = new();

    [JsonPropertyName("contents")]
    public List<ExerciseContent> Contents { get; set; } = new();

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("is_calorie_adjustment")]
    public bool IsCalorieAdjustment { get; set; }

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("exercise")]
    public Exercise Exercise { get; set; } = new();

    [JsonPropertyName("avg_heart_rate")]
    public double? AvgHeartRate { get; set; }

    [JsonPropertyName("max_heart_rate")]
    public double? MaxHeartRate { get; set; }

    [JsonPropertyName("distance")]
    public double? Distance { get; set; }

    [JsonPropertyName("max_speed")]
    public double? MaxSpeed { get; set; }

    [JsonPropertyName("elevation_change")]
    public double? ElevationChange { get; set; }

    [JsonPropertyName("start_time")]
    public string? StartTime { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;
}

/// <summary>
/// Request model for MyFitnessPal API
/// </summary>
public class MyFitnessPalRequest
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("show_food_diary")]
    public int ShowFoodDiary { get; set; } = 1;

    [JsonPropertyName("show_food_notes")]
    public int ShowFoodNotes { get; set; } = 1;

    [JsonPropertyName("show_exercise_diary")]
    public int ShowExerciseDiary { get; set; } = 0;

    [JsonPropertyName("show_exercise_notes")]
    public int ShowExerciseNotes { get; set; } = 0;
}
