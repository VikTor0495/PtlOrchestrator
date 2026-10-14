using System.Globalization;
using System.Text;
using PtlOrchestrator.Contracts.Operations;

namespace PtlOrchestrator.Api.Services;

/// <summary>
/// Esporta i turni in CSV pensato per Excel in italiano: separatore ";" e date ISO in UTC.
/// </summary>
public static class SessionCsvWriter
{
    private const char Separator = ';';

    private static readonly string[] Header =
    [
        "Turno", "Operatore", "Postazione", "Inizio (UTC)", "Fine (UTC)", "Durata (min)",
        "Prodotti accettati", "Prodotti rifiutati", "Moduli errati", "Timeout", "Guasti"
    ];

    public static string Write(IEnumerable<WorkSessionSummary> sessions)
    {
        var builder = new StringBuilder();
        AppendRow(builder, Header);

        foreach (var session in sessions)
        {
            AppendRow(builder,
            [
                session.Id.ToString(),
                session.OperatorName,
                session.StationId,
                FormatDate(session.StartedAt),
                session.EndedAt is null ? string.Empty : FormatDate(session.EndedAt.Value),
                session.EndedAt is null ? string.Empty : Math.Round((session.EndedAt.Value - session.StartedAt).TotalMinutes, 1).ToString(CultureInfo.InvariantCulture),
                session.ProductsAccepted.ToString(CultureInfo.InvariantCulture),
                session.ProductsRejected.ToString(CultureInfo.InvariantCulture),
                session.WrongModulePresses.ToString(CultureInfo.InvariantCulture),
                session.Timeouts.ToString(CultureInfo.InvariantCulture),
                session.Faults.ToString(CultureInfo.InvariantCulture)
            ]);
        }

        return builder.ToString();
    }

    private static string FormatDate(DateTimeOffset value)
        => value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static void AppendRow(StringBuilder builder, IReadOnlyList<string> cells)
    {
        for (var i = 0; i < cells.Count; i++)
        {
            if (i > 0)
                builder.Append(Separator);

            builder.Append(Escape(cells[i]));
        }

        builder.Append("\r\n");
    }

    private static string Escape(string value)
    {
        // un nome che inizia con = + - @ verrebbe interpretato da Excel come formula
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@')
            value = "'" + value;

        return value.Contains(Separator) || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
