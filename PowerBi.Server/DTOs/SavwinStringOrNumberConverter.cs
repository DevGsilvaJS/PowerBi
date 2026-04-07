using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PowerBi.Server.DTOs;

/// <summary>
/// SavWin costuma enviar identificadores como string ou número no JSON; <see cref="System.Text.Json"/>
/// não preenche <c>string?</c> quando o token é número — isso zerava <c>CODIGODAVENDA</c>.
/// </summary>
public sealed class SavwinStringOrNumberConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => NumberTokenToString(ref reader),
            JsonTokenType.Null => null,
            _ => null
        };
    }

    private static string NumberTokenToString(ref Utf8JsonReader reader)
    {
        if (reader.TryGetInt64(out var l))
        {
            return l.ToString(CultureInfo.InvariantCulture);
        }

        if (reader.TryGetDouble(out var d))
        {
            return d.ToString("G17", CultureInfo.InvariantCulture);
        }

        try
        {
            return reader.GetDecimal().ToString(CultureInfo.InvariantCulture);
        }
        catch
        {
            return string.Empty;
        }
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }
}
