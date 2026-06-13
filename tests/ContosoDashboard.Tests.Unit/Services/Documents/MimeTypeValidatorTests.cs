using System.IO;
using System.Text;
using System.Threading.Tasks;
using ContosoDashboard.Services.Documents;
using FluentAssertions;
using Xunit;

namespace ContosoDashboard.Tests.Unit.Services.Documents;

public class MimeTypeValidatorTests
{
    private readonly MimeTypeValidator _sut = new();

    [Fact]
    public async Task ValidateAndDetectAsync_ShouldReturnPdfMime_ForValidPdfMagicBytes()
    {
        await using var stream = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7 test"));

        var detected = await _sut.ValidateAndDetectAsync(stream, "pdf");

        detected.Should().Be(DocumentConstants.MimePdf);
    }

    [Fact]
    public async Task ValidateAndDetectAsync_ShouldThrow_WhenMagicBytesDoNotMatchDeclaredExtension()
    {
        // PNG signature while declared extension says pdf.
        var pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        await using var stream = new MemoryStream(pngHeader);

        var act = async () => await _sut.ValidateAndDetectAsync(stream, "pdf");

        await act.Should().ThrowAsync<InvalidDataException>();
    }
}
