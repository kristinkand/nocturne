using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.Serializers;

/// <summary>
/// JSON converter that handles flexible boolean serialization for Nightscout compatibility
/// Converts various string representations to boolean values (like "true", "false", "1", "0")
/// and handles null values gracefully
/// </summary>
public class FlexibleBooleanJsonConverter : JsonConverter<bool?>
{
    public override bool? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.String:
                var stringValue = reader.GetString();
                if (string.IsNullOrWhiteSpace(stringValue))
                    return null;

                return stringValue.ToLowerInvariant() switch
                {
                    "true" => true,
                    "false" => false,
                    "1" => true,
                    "0" => false,
                    "yes" => true,
                    "no" => false,
                    "on" => true,
                    "off" => false,
                    _ => null,
                };
            case JsonTokenType.Number:
                var numberValue = reader.GetInt32();
                return numberValue switch
                {
                    1 => true,
                    0 => false,
                    _ => null,
                };
            case JsonTokenType.Null:
                return null;
            default:
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteBooleanValue(value.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
