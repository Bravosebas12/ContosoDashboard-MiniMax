using System.Security.Claims;
using ContosoDashboard.Services.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ContosoDashboard.Pages.DocumentFiles;

[Authorize(Policy = "Employee")]
public class DownloadModel : PageModel
{
    private readonly IDocumentService _documentService;

    public DownloadModel(IDocumentService documentService)
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

            Response.Headers["X-Content-Type-Options"] = "nosniff";

            return File(stream, contentType, fileName);
        }
        catch (DocumentNotFoundException)  { return NotFound(); }
        catch (DocumentUnauthorizedAccessException) { return Forbid(); }
    }
}
