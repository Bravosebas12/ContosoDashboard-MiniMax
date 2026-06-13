using System.Globalization;
using System.Text;
using ContosoDashboard.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ContosoDashboard.Services.Documents;

/// <summary>
/// Implementacion de reportes administrativos sobre documentos (FR-030, AC-6.2.1).
/// Top 10 por tipo MIME o por usuario uploader, con salida CSV escapada (RFC 4180).
/// </summary>
public class DocumentReportService : IDocumentReportService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<DocumentReportService> _logger;

    public DocumentReportService(ApplicationDbContext db, ILogger<DocumentReportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DocumentReportRow>> GetTopMimeTypesAsync(int top = 10, CancellationToken ct = default)
    {
        if (top < 1) top = 10;

        var groups = await _db.Documents
            .AsNoTracking()
            .GroupBy(d => d.FileType)
            .Select(g => new { MimeType = g.Key, Count = g.LongCount() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.MimeType)
            .Take(top)
            .ToListAsync(ct);

        return groups
            .Select((g, idx) => new DocumentReportRow(idx + 1, g.MimeType, g.MimeType, g.Count))
            .ToList();
    }

    public async Task<IReadOnlyList<DocumentReportRow>> GetTopUploadersAsync(int top = 10, CancellationToken ct = default)
    {
        if (top < 1) top = 10;

        // LEFT JOIN: incluimos usuarios que no subieron (con 0) para tener el top 10 completo.
        // AsNoTracking para solo lectura.
        var raw = await _db.Documents
            .AsNoTracking()
            .GroupBy(d => d.UploadedByUserId)
            .Select(g => new { UserId = g.Key, Count = g.LongCount() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.UserId)
            .Take(top)
            .ToListAsync(ct);

        if (raw.Count == 0)
            return Array.Empty<DocumentReportRow>();

        // Hidratamos el nombre del usuario con un solo round-trip.
        var userIds = raw.Select(x => x.UserId).Distinct().ToList();
        var users = await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.UserId))
            .Select(u => new { u.UserId, u.DisplayName, u.Email })
            .ToListAsync(ct);
        var byId = users.ToDictionary(u => u.UserId);

        return raw
            .Select((g, idx) =>
            {
                byId.TryGetValue(g.UserId, out var u);
                var label = u is null
                    ? $"User #{g.UserId}"
                    : $"{u.DisplayName} ({u.Email})";
                return new DocumentReportRow(idx + 1, g.UserId.ToString(), label, g.Count);
            })
            .ToList();
    }

    public async Task<string> GenerateCsvAsync(DocumentReportType type, CancellationToken ct = default)
    {
        var (title, rows) = type switch
        {
            DocumentReportType.MimeTypes => ("Top 10 MIME types by uploads", await GetTopMimeTypesAsync(10, ct)),
            DocumentReportType.Uploaders => ("Top 10 uploaders by document count", await GetTopUploadersAsync(10, ct)),
            _ => throw new ArgumentOutOfRangeException(nameof(type), $"Unsupported report type: {type}")
        };

        var sb = new StringBuilder();
        // Encabezado con titulo del reporte
        sb.AppendLine($"# {Escape(title)}");
        sb.AppendLine($"# Generated: {DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}");
        sb.AppendLine();

        // Columnas CSV (RFC 4180: coma como separador, comillas para escapar)
        sb.AppendLine("Rank,Key,Label,Count");

        if (rows.Count == 0)
        {
            sb.AppendLine("0,N/A,(no data),0");
        }
        else
        {
            foreach (var row in rows)
            {
                sb.AppendLine($"{row.Rank},{Escape(row.Key)},{Escape(row.Label)},{row.Count.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        _logger.LogInformation("Report {Type} generated with {Count} rows", type, rows.Count);
        return sb.ToString();
    }

    /// <summary>Escapa comillas y comas segun RFC 4180.</summary>
    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        // Si contiene coma, comilla o salto de linea, envolver en comillas y escapar comillas internas
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
