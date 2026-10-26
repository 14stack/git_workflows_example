using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JobsApi.Infrastructure;

/// <summary>
/// Enum values are exchanged as snake_case strings everywhere (HTTP, database, RabbitMQ),
/// e.g. <c>BreastCancer</c> ⇄ <c>"breast_cancer"</c>. RabbitMQ messages also use snake_case
/// property names (<see cref="JsonOptions"/>) so the Python worker stays idiomatic, while the
/// HTTP API keeps the usual camelCase property names.
/// </summary>
public static class SnakeCase
{
    /// <summary>Serializer options for broker messages: snake_case properties and enum values.</summary>
    public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

    /// <summary>Converter for snake_case enum values, used by the HTTP API.</summary>
    public static JsonStringEnumConverter CreateEnumConverter() => new(JsonNamingPolicy.SnakeCaseLower);

    public static string ToSnakeCase<TEnum>(this TEnum value)
        where TEnum : struct, Enum
        => EnumNames<TEnum>.ToName[value];

    public static TEnum ParseSnakeCase<TEnum>(string value)
        where TEnum : struct, Enum
        => EnumNames<TEnum>.FromName.TryGetValue(value, out var parsed)
            ? parsed
            : throw new ArgumentOutOfRangeException(nameof(value), value, $"Unknown {typeof(TEnum).Name} value.");

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };
        options.Converters.Add(CreateEnumConverter());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    private static class EnumNames<TEnum>
        where TEnum : struct, Enum
    {
        public static readonly FrozenDictionary<TEnum, string> ToName =
            Enum.GetValues<TEnum>().ToFrozenDictionary(v => v, v => JsonNamingPolicy.SnakeCaseLower.ConvertName(v.ToString()));

        public static readonly FrozenDictionary<string, TEnum> FromName =
            ToName.ToFrozenDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);
    }
}

/// <summary>EF Core converter storing an enum as its snake_case name.</summary>
public sealed class SnakeCaseEnumConverter<TEnum>()
    : ValueConverter<TEnum, string>(v => v.ToSnakeCase(), v => SnakeCase.ParseSnakeCase<TEnum>(v))
    where TEnum : struct, Enum;
