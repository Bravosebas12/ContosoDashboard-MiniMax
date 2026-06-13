using System.Text;
using ContosoDashboard.Services.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ContosoDashboard.Pages.AdminReports;

/// <summary>
/// Endpoint /admin/reports/documents/{type} per FR-030 y AC-6.2.1.
/// Devuelve CSV con top 10 registros segun tipo:
///   - types: top 10 MIME types por uploads
///   - uploaders: top 10 usuarios por uploads
/// Solo accesible para usuarios con rol Administrator (politica "Administrator").
/// </summary>
[Authorize(Policy = "Administrator")]
public class DocumentsModel : PageModel
{
    private readonly IDocumentReportService _reportService;

    public DocumentsModel(IDocumentReportService reportService)
    {
        _reportService = reportService;
    }

    public async Task<IActionResult> OnGetAsync(string type, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(type))
            return BadRequest("Report type is required. Valid values: 'types' or 'uploaders'.");

        DocumentReportType reportType;
        switch (type.ToLowerInvariant())
        {
            case "types":
            case "mimetypes":
                reportType = DocumentReportType.MimeTypes;
                break;
            case "uploaders":
            case "users":
                reportType = DocumentReportType.Uploaders;
                break;
            default:
                return BadRequest($"Unknown report type '{type}'. Valid values: 'types' or 'uploaders'.");
        }

        var csv = await _reportService.GenerateCsvAsync(reportType, ct);

        // Headers de respuesta para descarga
        var bytes = Encoding.UTF8.GetBytes(csv);
        var filename = $"documents_{type}_top10_{DateTime.UtcNow:yyyyMMdd}.csv";

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Disposition"] = $"attachment; filename=\"{filename}\"";

        return File(bytes, "text/csv");
    }
}
