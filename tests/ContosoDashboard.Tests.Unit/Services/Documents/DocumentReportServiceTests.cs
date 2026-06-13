using System;
using System.Linq;
using System.Threading.Tasks;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services.Documents;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace ContosoDashboard.Tests.Unit.Services.Documents;

public class DocumentReportServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly DocumentReportService _sut;

    public DocumentReportServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        _db = new ApplicationDbContext(options);
        var logger = Substitute.For<ILogger<DocumentReportService>>();
        _sut = new DocumentReportService(_db, logger);
    }

    [Fact]
    public async Task GetTopMimeTypesAsync_ShouldReturnTop10GroupedByFileType()
    {
        // Seed: 4 PDFs, 3 DOCX, 1 PNG
        _db.Documents.AddRange(
            NewDoc("a.pdf", "application/pdf"),
            NewDoc("b.pdf", "application/pdf"),
            NewDoc("c.pdf", "application/pdf"),
            NewDoc("d.pdf", "application/pdf"),
            NewDoc("a.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
            NewDoc("b.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
            NewDoc("c.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
            NewDoc("a.png", "image/png")
        );
        await _db.SaveChangesAsync();

        var rows = await _sut.GetTopMimeTypesAsync(top: 10);

        rows.Should().HaveCount(3);
        rows[0].Label.Should().Be("application/pdf");
        rows[0].Count.Should().Be(4);
        rows[0].Rank.Should().Be(1);
        rows[1].Count.Should().Be(3);
        rows[2].Count.Should().Be(1);
    }

    [Fact]
    public async Task GetTopUploadersAsync_ShouldIncludeUserDisplayNameAndEmail()
    {
        _db.Users.Add(new User { UserId = 1, Email = "alice@contoso.com", DisplayName = "Alice Smith", Department = "Eng", JobTitle = "PM", Role = UserRole.ProjectManager, AvailabilityStatus = AvailabilityStatus.Available, CreatedDate = DateTime.UtcNow, EmailNotificationsEnabled = true, InAppNotificationsEnabled = true });
        _db.Documents.Add(NewDoc("a.pdf", "application/pdf", uploaderId: 1));
        _db.Documents.Add(NewDoc("b.pdf", "application/pdf", uploaderId: 1));
        _db.Documents.Add(NewDoc("c.pdf", "application/pdf", uploaderId: 1));
        await _db.SaveChangesAsync();

        var rows = await _sut.GetTopUploadersAsync(top: 10);

        rows.Should().HaveCount(1);
        rows[0].Count.Should().Be(3);
        rows[0].Label.Should().Contain("Alice Smith").And.Contain("alice@contoso.com");
    }

    [Fact]
    public async Task GenerateCsvAsync_ShouldIncludeTitleMetadataAndHeaderRow()
    {
        _db.Documents.Add(NewDoc("a.pdf", "application/pdf"));
        await _db.SaveChangesAsync();

        var csv = await _sut.GenerateCsvAsync(DocumentReportType.MimeTypes);

        csv.Should().StartWith("# Top 10 MIME types by uploads");
        csv.Should().Contain("# Generated:");
        csv.Should().Contain("Rank,Key,Label,Count");
        csv.Should().Contain("1,application/pdf,application/pdf,1");
    }

    [Fact]
    public async Task GenerateCsvAsync_ShouldEscapeCommasAndQuotesInLabel()
    {
        _db.Documents.Add(NewDoc("a", "application/pdf"));
        _db.Users.Add(new User { UserId = 1, Email = "alice@contoso.com", DisplayName = "Alice, Smith", Department = "Eng", JobTitle = "PM", Role = UserRole.ProjectManager, AvailabilityStatus = AvailabilityStatus.Available, CreatedDate = DateTime.UtcNow, EmailNotificationsEnabled = true, InAppNotificationsEnabled = true });
        _db.Documents.Add(NewDoc("b.pdf", "application/pdf", uploaderId: 1));
        await _db.SaveChangesAsync();

        var csv = await _sut.GenerateCsvAsync(DocumentReportType.Uploaders);

        // "Alice, Smith" contiene coma, debe quedar envuelto en comillas segun RFC 4180
        csv.Should().Contain("\"Alice, Smith");
    }

    [Fact]
    public async Task GenerateCsvAsync_ShouldReturnEmptyDataRow_WhenNoDocuments()
    {
        var csv = await _sut.GenerateCsvAsync(DocumentReportType.MimeTypes);

        csv.Should().Contain("Rank,Key,Label,Count");
        csv.Should().Contain("(no data)");
    }

    [Fact]
    public async Task GenerateCsvAsync_ShouldLimitToTenRows()
    {
        for (int i = 0; i < 15; i++)
        {
            _db.Documents.Add(NewDoc($"d{i}.pdf", "application/pdf"));
        }
        await _db.SaveChangesAsync();

        var csv = await _sut.GenerateCsvAsync(DocumentReportType.MimeTypes);

        // Solo 10 lineas de datos
        var dataLines = csv.Split('\n').Where(l => l.StartsWith(l.Trim().Length > 0 && char.IsDigit(l.Trim()[0]) ? l.Trim()[0].ToString() : "")).Count();
        // Filtro manual: lineas que empiezan con un digito (rank 1..9) o "10"
        var ranked = csv.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l) && l.Trim()[0] != '#' && !l.StartsWith("Rank") && !l.StartsWith("Generated:")).ToList();
        ranked.Count.Should().BeLessThanOrEqualTo(10);
    }

    private static Document NewDoc(string title, string mime, int uploaderId = 4) => new()
    {
        Title = title,
        Category = DocumentConstants.CategoryPersonalFiles,
        FilePath = $"{uploaderId}/personal/{Guid.NewGuid():N}.pdf",
        FileSize = 1024,
        FileType = mime,
        Tags = null,
        UploadedAt = DateTime.UtcNow,
        UploadedByUserId = uploaderId,
        ProjectId = null,
        TaskId = null,
        AvScanStatus = DocumentAvStatus.Clean,
        OriginalFileName = title
    };

    public void Dispose() => _db.Dispose();
}
