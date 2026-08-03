using System.Text.Json;
using System.Text.Json.Serialization;

namespace Skill.Suite.Marker.Map;

/// <summary>
/// Reads a <see cref="PartRule"/> written either as a bare string or as an object.
/// </summary>
public sealed class PartRuleConverter : JsonConverter<PartRule>
{
    /// <inheritdoc />
    public override PartRule Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var id = reader.GetString();
            return string.IsNullOrWhiteSpace(id)
                ? throw new JsonException("A part name must not be empty.")
                : PartRule.FromId(id);
        }

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"A part must be a string or an object, found {reader.TokenType}.");

        string? objectId = null;
        var segments = new List<string>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            var property = reader.GetString();
            reader.Read();

            if (string.Equals(property, "id", StringComparison.OrdinalIgnoreCase))
            {
                objectId = reader.GetString();
            }
            else if (string.Equals(property, "segments", StringComparison.OrdinalIgnoreCase))
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    segments.Add(reader.GetString()!);
                }
                else if (reader.TokenType == JsonTokenType.StartArray)
                {
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (reader.TokenType == JsonTokenType.String) segments.Add(reader.GetString()!);
                    }
                }
            }
            else
            {
                reader.Skip();
            }
        }

        return string.IsNullOrWhiteSpace(objectId)
            ? throw new JsonException("A part object must carry a non-empty \"id\".")
            : new PartRule { Id = objectId, Segments = segments };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, PartRule value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("id", value.Id);
        writer.WriteStartArray("segments");
        foreach (var segment in value.Segments) writer.WriteStringValue(segment);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
