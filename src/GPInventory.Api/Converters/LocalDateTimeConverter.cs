using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GPInventory.Api.Converters
{
    public class LocalDateTimeConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            var dateString = reader.GetString();
            if (string.IsNullOrEmpty(dateString))
                return null;

            // Intentar parsear la fecha como se recibió sin conversión de zona horaria
            if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dateTime))
            {
                return dateTime;
            }

            // Si falla, intentar con diferentes formatos
            if (DateTime.TryParse(dateString, out var parsedDate))
            {
                return parsedDate;
            }

            return null;
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                // Escribir la fecha en formato ISO pero sin conversión de zona horaria
                writer.WriteStringValue(value.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }

    public class LocalDateTimeConverterNonNullable : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var dateString = reader.GetString();
            if (string.IsNullOrEmpty(dateString))
                return DateTime.MinValue;

            // Intentar parsear la fecha como se recibió sin conversión de zona horaria
            if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dateTime))
            {
                return dateTime;
            }

            // Si falla, intentar con diferentes formatos
            if (DateTime.TryParse(dateString, out var parsedDate))
            {
                return parsedDate;
            }

            return DateTime.MinValue;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            // Escribir la fecha en formato ISO pero sin conversión de zona horaria
            writer.WriteStringValue(value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        }
    }
}
