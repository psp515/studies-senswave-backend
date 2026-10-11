using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Senswave.Abstractions.Resulting;
using Senswave.Infrastructure.Grpc.Errors;
using System.Text.Json.Nodes;

namespace Senswave.Infrastructure.Grpc.Mapping;

/// <summary>
/// Conversions between proto scalar conventions (Spec 0) and CLR types. Used only by <c>*GrpcExtensions</c>.
/// Malformed input surfaces as <c>InvalidArgument</c>, never as an unhandled exception.
/// </summary>
public static class ProtoMapping
{
    public static Guid ParseGuid(string value, string field)
    {
        if (Guid.TryParse(value, out var guid))
            return guid;

        throw InvalidField(field, $"'{field}' must be a valid GUID.");
    }

    /// <summary>Empty string means "not set" and maps to <see langword="null"/>.</summary>
    public static Guid? ParseOptionalGuid(string? value, string field) =>
        string.IsNullOrEmpty(value) ? null : ParseGuid(value, field);

    /// <summary>Empty string maps to <see cref="Guid.Empty"/>, mirroring an omitted REST Guid field.</summary>
    public static Guid ParseGuidOrEmpty(string? value, string field) =>
        ParseOptionalGuid(value, field) ?? Guid.Empty;

    public static Timestamp ToTimestamp(this DateTime value) =>
        Timestamp.FromDateTime(value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public static Timestamp? ToTimestamp(this DateTime? value) =>
        value.HasValue ? value.Value.ToTimestamp() : null;

    public static Struct ToStruct(this JsonObject? value) =>
        value is null ? new Struct() : Struct.Parser.ParseJson(value.ToJsonString());

    public static JsonObject ToJsonObject(this Struct? value, string field)
    {
        if (value is null)
            return [];

        return JsonNode.Parse(JsonFormatter.Default.Format(value)) as JsonObject
            ?? throw InvalidField(field, $"'{field}' must be a JSON object.");
    }

    public static Value ToProtoValue(this JsonNode? value) =>
        value is null ? Value.ForNull() : Value.Parser.ParseJson(value.ToJsonString());

    public static JsonValue ToJsonValue(this Value? value, string field)
    {
        if (value is null || value.KindCase == Value.KindOneofCase.None)
            return JsonValue.Create(string.Empty);

        return JsonNode.Parse(JsonFormatter.Default.Format(value)) as JsonValue
            ?? throw InvalidField(field, $"'{field}' must be a JSON scalar.");
    }

    /// <summary>Proto3 scalars cannot be absent; 0 means "use the REST default".</summary>
    public static int OrDefault(this int value, int defaultValue) => value == 0 ? defaultValue : value;

    private static SenswaveRpcException InvalidField(string field, string description) =>
        new([Error.Validation($"Invalid{char.ToUpperInvariant(field[0])}{field[1..]}", description)]);
}
