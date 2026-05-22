namespace CareerVault.Api.Services;

public sealed record IngestionTemporalContext(
    DateTimeOffset ReferenceDateTime,
    string Source,
    string TimeZoneId)
{
    private const string SaoPauloTimeZoneId = "America/Sao_Paulo";

    public static IngestionTemporalContext Now(string source) =>
        new(ToSaoPaulo(DateTimeOffset.UtcNow), source, SaoPauloTimeZoneId);

    public static IngestionTemporalContext FromUnixSeconds(long unixSeconds, string source) =>
        new(ToSaoPaulo(DateTimeOffset.FromUnixTimeSeconds(unixSeconds)), source, SaoPauloTimeZoneId);

    private static DateTimeOffset ToSaoPaulo(DateTimeOffset utcDateTime)
    {
        var timeZone = FindSaoPauloTimeZone();
        return TimeZoneInfo.ConvertTime(utcDateTime, timeZone);
    }

    private static TimeZoneInfo FindSaoPauloTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(SaoPauloTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        }
    }
}
