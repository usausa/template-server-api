namespace Template.Web.Application.HealthChecks;

using System.Text.Json;

using Microsoft.Extensions.Diagnostics.HealthChecks;

public static class HealthCheckWriter
{
    public static Task WriteResponse(HttpContext context, HealthReport healthReport)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("status", healthReport.Status.ToString());
            writer.WriteStartObject("results");

            foreach (var healthReportEntry in healthReport.Entries)
            {
                writer.WriteStartObject(healthReportEntry.Key);
                writer.WriteString("status", healthReportEntry.Value.Status.ToString());
                writer.WriteString("description", healthReportEntry.Value.Description);
                writer.WriteStartObject("data");

                foreach (var item in healthReportEntry.Value.Data)
                {
                    writer.WritePropertyName(item.Key);
                    JsonSerializer.Serialize(writer, item.Value, item.Value.GetType());
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return context.Response.WriteAsync(Encoding.UTF8.GetString(ms.ToArray()));
    }
}
