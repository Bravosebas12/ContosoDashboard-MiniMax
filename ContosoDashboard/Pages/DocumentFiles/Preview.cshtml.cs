using System.Net.Mime;
using System.Security.Claims;
using ContosoDashboard.Services.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ContosoDashboard.Pages.DocumentFiles;

[Authorize(Policy = "Employee")]
public class PreviewModel : PageModel
{
    private readonly IDocumentService _documentService;

    public PreviewModel(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var nameId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(nameId, out var userId))
            return Unauthorized();

        try
        {
            var (stream, fileName, contentType) = await _documentService.OpenForDownloadAsync(id, userId);

            // Para que el navegador renderice el PDF inline (no descargue):
            // - Content-Disposition: inline
            // - X-Content-Type-Options: nosniff (heredado del middleware global)
            // NO forzamos CSP: sandbox aquí porque eso aísla el iframe de su padre y
            // algunos navegadores interpretan la combinación sandbox + iframe como descarga.
            // La CSP global (frame-src 'self') ya permite enmarcar contenido same-origin.
            var cd = new ContentDisposition { FileName = fileName, DispositionType = "inline" };
            Response.Headers["Content-Disposition"] = cd.ToString();

            return File(stream, contentType);
        }
        catch (DocumentNotFoundException) { return NotFound(); }
        catch (DocumentUnauthorizedAccessException) { return Forbid(); }
    }
}
