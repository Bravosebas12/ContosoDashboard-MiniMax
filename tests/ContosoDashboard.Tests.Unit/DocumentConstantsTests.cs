using ContosoDashboard.Services.Documents;
using FluentAssertions;
using Xunit;

namespace ContosoDashboard.Tests.Unit;

/// <summary>
/// Smoke test para verificar que la infraestructura de tests funciona correctamente.
/// Este test valida la lógica más simple y estable del dominio (constantes) y
/// sirve como canario para detectar problemas en el setup (referencias, paquetes,
/// FluentAssertions, etc.).
/// </summary>
public class DocumentConstantsTests
{
    [Fact]
    public void AllowedCategories_ShouldContain_6ExpectedCategories()
    {
        // Arrange & Act
        var categories = DocumentConstants.AllowedCategories;

        // Assert
        categories.Should().HaveCount(6);
        categories.Should().Contain(DocumentConstants.CategoryProjectDocuments);
        categories.Should().Contain(DocumentConstants.CategoryTeamResources);
        categories.Should().Contain(DocumentConstants.CategoryPersonalFiles);
        categories.Should().Contain(DocumentConstants.CategoryReports);
        categories.Should().Contain(DocumentConstants.CategoryPresentations);
        categories.Should().Contain(DocumentConstants.CategoryOther);
    }

    [Theory]
    [InlineData("pdf", DocumentConstants.MimePdf)]
    [InlineData("docx", DocumentConstants.MimeDocx)]
    [InlineData("xlsx", DocumentConstants.MimeXlsx)]
    [InlineData("pptx", DocumentConstants.MimePptx)]
    [InlineData("txt", DocumentConstants.MimeTxt)]
    [InlineData("jpg", DocumentConstants.MimeJpeg)]
    [InlineData("jpeg", DocumentConstants.MimeJpeg)]
    [InlineData("png", DocumentConstants.MimePng)]
    public void AllowedMimeByExtension_ShouldReturnCorrectMimeType(string extension, string expectedMime)
    {
        // Act
        var result = DocumentConstants.AllowedMimeByExtension.TryGetValue(extension, out var actualMime);

        // Assert
        result.Should().BeTrue($"extension '{extension}' debe estar en la whitelist");
        actualMime.Should().Be(expectedMime);
    }

    [Theory]
    [InlineData("exe")]
    [InlineData("bat")]
    [InlineData("zip")]
    [InlineData("")]
    [InlineData("xyz")]
    public void AllowedMimeByExtension_ShouldRejectInvalidExtensions(string extension)
    {
        // Act
        var result = DocumentConstants.AllowedMimeByExtension.TryGetValue(extension, out _);

        // Assert
        result.Should().BeFalse($"extension '{extension}' NO debe estar permitida");
    }

    [Theory]
    [InlineData(DocumentConstants.MimePdf, true)]
    [InlineData(DocumentConstants.MimeJpeg, true)]
    [InlineData(DocumentConstants.MimePng, true)]
    [InlineData(DocumentConstants.MimeDoc, false)]
    [InlineData(DocumentConstants.MimeXlsx, false)]
    [InlineData(DocumentConstants.MimeTxt, false)]
    public void IsPreviewable_ShouldReturnTrueOnlyForPdfAndImages(string mimeType, bool expected)
    {
        // Act
        var result = DocumentConstants.IsPreviewable(mimeType);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void MaxFileSizeBytes_ShouldBe25MB()
    {
        // Act
        var expectedBytes = 25L * 1024 * 1024;

        // Assert
        DocumentConstants.MaxFileSizeBytes.Should().Be(expectedBytes);
    }

    [Fact]
    public void LogRetentionDays_ShouldBe90()
    {
        // Per FR-031: minimum 90 days
        DocumentConstants.LogRetentionDays.Should().Be(90);
    }

    [Theory]
    [InlineData("pdf", DocumentConstants.MimePdf, true)]
    [InlineData("pdf", "application/octet-stream", false)]
    [InlineData("jpg", DocumentConstants.MimeJpeg, true)]
    [InlineData("jpg", "image/png", false)]
    [InlineData("exe", "application/octet-stream", false)]
    public void IsAllowed_ShouldValidateExtensionAndMimeTypeConsistency(
        string extension, string mimeType, bool expected)
    {
        // Act
        var result = DocumentConstants.IsAllowed(extension, mimeType);

        // Assert
        result.Should().Be(expected);
    }
}
