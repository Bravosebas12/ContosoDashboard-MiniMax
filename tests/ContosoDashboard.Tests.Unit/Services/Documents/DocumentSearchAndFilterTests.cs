using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Services.Documents;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace ContosoDashboard.Tests.Unit.Services.Documents;

/// <summary>
/// Tests T065: ListAsync con filtros combinados, ordenamiento y paginacion.
/// + SearchAsync con LIKE-based search.
/// </summary>
public class DocumentSearchAndFilterTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly DocumentService _sut;

    public DocumentSearchAndFilterTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        _db = new ApplicationDbContext(options);

        var storage = Substitute.For<IFileStorageService>();
        var av = Substitute.For<IAntivirusScanner>();
        var mime = Substitute.For<IMimeTypeValidator>();
        var path = Substitute.For<IFilePathBuilder>();
        var activity = Substitute.For<IActivityLogService>();
        var notif = Substitute.For<INotificationService>();
        var logger = Substitute.For<ILogger<DocumentService>>();

        _sut = new DocumentService(_db, storage, av, mime, path, activity, notif, logger);
    }

    [Fact]
    public async Task ListAsync_ShouldFilterByCategory()
    {
        await SeedAsync("budget.pdf", DocumentConstants.CategoryReports, "alice");
        await SeedAsync("notes.txt", DocumentConstants.CategoryPersonalFiles, "alice");
        await SeedAsync("plan.docx", DocumentConstants.CategoryProjectDocuments, "alice");

        var result = await _sut.ListAsync(
            new DocumentListFilter(Category: DocumentConstants.CategoryReports),
            DocumentSortBy.Title, SortDirection.Asc, 1, 25, currentUserId: 1);

        result.Items.Should().HaveCount(1);
        result.Items[0].Title.Should().Be("budget.pdf");
    }

    [Fact]
    public async Task ListAsync_ShouldFilterByProjectId()
    {
        _db.Projects.Add(new Project { ProjectId = 5, Name = "Q4", Description = "x", ProjectManagerId = 1, StartDate = DateTime.UtcNow, TargetCompletionDate = DateTime.UtcNow.AddDays(30), Status = ProjectStatus.Active, CreatedDate = DateTime.UtcNow, UpdatedDate = DateTime.UtcNow });
        _db.ProjectMembers.Add(new ProjectMember { ProjectId = 5, UserId = 1, Role = "PM", AssignedDate = DateTime.UtcNow });
        await _db.Documents.AddRangeAsync(
            NewDoc("in-q4.pdf", DocumentConstants.CategoryProjectDocuments, uploader: 1, projectId: 5),
            NewDoc("out.pdf", DocumentConstants.CategoryPersonalFiles, uploader: 1, projectId: null));
        await _db.SaveChangesAsync();

        var result = await _sut.ListAsync(
            new DocumentListFilter(ProjectId: 5),
            DocumentSortBy.UploadedAt, SortDirection.Desc, 1, 25, currentUserId: 1);

        result.Items.Should().HaveCount(1);
        result.Items[0].Title.Should().Be("in-q4.pdf");
    }

    [Fact]
    public async Task ListAsync_ShouldSortByFileSizeDescending()
    {
        await SeedAsync("small.pdf", DocumentConstants.CategoryPersonalFiles, "alice", fileSize: 100);
        await SeedAsync("large.pdf", DocumentConstants.CategoryPersonalFiles, "alice", fileSize: 5_000_000);
        await SeedAsync("medium.pdf", DocumentConstants.CategoryPersonalFiles, "alice", fileSize: 1_000_000);

        var result = await _sut.ListAsync(
            new DocumentListFilter(UploadedByUserId: 1),
            DocumentSortBy.FileSize, SortDirection.Desc, 1, 25, currentUserId: 1);

        result.Items.Should().HaveCount(3);
        result.Items[0].FileSize.Should().Be(5_000_000);
        result.Items[1].FileSize.Should().Be(1_000_000);
        result.Items[2].FileSize.Should().Be(100);
    }

    [Fact]
    public async Task ListAsync_ShouldPaginateCorrectly()
    {
        for (int i = 0; i < 7; i++)
            await SeedAsync($"doc{i:00}.pdf", DocumentConstants.CategoryPersonalFiles, "alice");

        var page1 = await _sut.ListAsync(
            new DocumentListFilter(UploadedByUserId: 1),
            DocumentSortBy.Title, SortDirection.Asc, 1, 3, currentUserId: 1);
        var page2 = await _sut.ListAsync(
            new DocumentListFilter(UploadedByUserId: 1),
            DocumentSortBy.Title, SortDirection.Asc, 2, 3, currentUserId: 1);
        var page3 = await _sut.ListAsync(
            new DocumentListFilter(UploadedByUserId: 1),
            DocumentSortBy.Title, SortDirection.Asc, 3, 3, currentUserId: 1);

        page1.Items.Should().HaveCount(3);
        page2.Items.Should().HaveCount(3);
        page3.Items.Should().HaveCount(1);
        page1.TotalCount.Should().Be(7);
        page1.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task SearchAsync_ShouldMatchTitleDescriptionAndTags()
    {
        await SeedAsync("budget-q4.pdf", DocumentConstants.CategoryReports, "alice", description: "Annual budget", tags: "finance,q4");
        await SeedAsync("roadmap.pdf", DocumentConstants.CategoryProjectDocuments, "alice", description: "Q4 plan");
        await SeedAsync("holiday.jpg", DocumentConstants.CategoryPersonalFiles, "alice", description: "Beach");

        var result = await _sut.SearchAsync("q4", 1, 25, currentUserId: 1);

        // LIKE-based: encuentra por "q4" en title ("budget-q4.pdf") y description ("Q4 plan")
        result.Items.Count.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnEmpty_WhenNoMatch()
    {
        await SeedAsync("doc1.pdf", DocumentConstants.CategoryPersonalFiles, "alice");
        var result = await _sut.SearchAsync("xyznonexistent", 1, 25, currentUserId: 1);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnEmpty_WhenQueryIsBlank()
    {
        await SeedAsync("doc1.pdf", DocumentConstants.CategoryPersonalFiles, "alice");
        var result = await _sut.SearchAsync("   ", 1, 25, currentUserId: 1);
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    private async Task SeedAsync(string title, string category, string tag, string description = null, string tags = null, long fileSize = 1024)
    {
        _db.Documents.Add(NewDoc(title, category, uploader: 1, fileSize: fileSize, description: description, tags: tags));
        await _db.SaveChangesAsync();
    }

    private static Document NewDoc(string title, string category, int uploader, long fileSize = 1024, int? projectId = null, string description = null, string tags = null) => new()
    {
        Title = title,
        Description = description,
        Category = category,
        FilePath = $"{uploader}/personal/{Guid.NewGuid():N}.pdf",
        FileSize = fileSize,
        FileType = "application/pdf",
        Tags = tags,
        UploadedAt = DateTime.UtcNow,
        UploadedByUserId = uploader,
        ProjectId = projectId,
        TaskId = null,
        AvScanStatus = DocumentAvStatus.Clean,
        OriginalFileName = title
    };

    public void Dispose() => _db.Dispose();
}
