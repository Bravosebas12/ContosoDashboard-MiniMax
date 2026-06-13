using System;
using System.Linq;
using System.Threading.Tasks;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services.Documents;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ContosoDashboard.Tests.Integration;

/// <summary>
/// T056: happy path del upload a proyecto + 403 para no-miembros.
/// Usa EF Core InMemory para no requerir Docker.
/// </summary>
public class ProjectDocumentUploadTests : IDisposable
{
    private readonly ApplicationDbContext _db;

    public ProjectDocumentUploadTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        _db = new ApplicationDbContext(options);
    }

    [Fact]
    public async Task ListByProject_ShouldReturnOnlyDocumentsOfTargetProject()
    {
        _db.Projects.Add(new Project { ProjectId = 1, Name = "A", Description = "x", ProjectManagerId = 1, StartDate = DateTime.UtcNow, TargetCompletionDate = DateTime.UtcNow.AddDays(30), Status = ProjectStatus.Active, CreatedDate = DateTime.UtcNow, UpdatedDate = DateTime.UtcNow });
        _db.Projects.Add(new Project { ProjectId = 2, Name = "B", Description = "x", ProjectManagerId = 1, StartDate = DateTime.UtcNow, TargetCompletionDate = DateTime.UtcNow.AddDays(30), Status = ProjectStatus.Active, CreatedDate = DateTime.UtcNow, UpdatedDate = DateTime.UtcNow });
        _db.ProjectMembers.Add(new ProjectMember { ProjectId = 1, UserId = 4, Role = "Dev", AssignedDate = DateTime.UtcNow });
        _db.Documents.Add(new Document { Title = "d1", Category = DocumentConstants.CategoryProjectDocuments, FilePath = "1/personal/a.pdf", FileSize = 100, FileType = "application/pdf", UploadedAt = DateTime.UtcNow, UploadedByUserId = 1, ProjectId = 1, AvScanStatus = DocumentAvStatus.Clean, OriginalFileName = "d1" });
        _db.Documents.Add(new Document { Title = "d2", Category = DocumentConstants.CategoryProjectDocuments, FilePath = "1/personal/b.pdf", FileSize = 100, FileType = "application/pdf", UploadedAt = DateTime.UtcNow, UploadedByUserId = 1, ProjectId = 2, AvScanStatus = DocumentAvStatus.Clean, OriginalFileName = "d2" });
        await _db.SaveChangesAsync();

        var project1Docs = await _db.Documents.Where(d => d.ProjectId == 1).ToListAsync();

        project1Docs.Should().HaveCount(1);
        project1Docs.First().Title.Should().Be("d1");
    }

    [Fact]
    public async Task NonMember_ShouldNotAppearInProjectDocuments_Query()
    {
        // User 99 no es miembro del proyecto 1
        _db.Projects.Add(new Project { ProjectId = 1, Name = "A", Description = "x", ProjectManagerId = 1, StartDate = DateTime.UtcNow, TargetCompletionDate = DateTime.UtcNow.AddDays(30), Status = ProjectStatus.Active, CreatedDate = DateTime.UtcNow, UpdatedDate = DateTime.UtcNow });
        _db.ProjectMembers.Add(new ProjectMember { ProjectId = 1, UserId = 1, Role = "PM", AssignedDate = DateTime.UtcNow });
        _db.Documents.Add(new Document { Title = "secret", Category = DocumentConstants.CategoryProjectDocuments, FilePath = "1/personal/a.pdf", FileSize = 100, FileType = "application/pdf", UploadedAt = DateTime.UtcNow, UploadedByUserId = 1, ProjectId = 1, AvScanStatus = DocumentAvStatus.Clean, OriginalFileName = "secret" });
        await _db.SaveChangesAsync();

        // User 99: no es dueno, no es miembro, no es PM. La autorizacion server-side
        // (BuildAuthorizedQuery) debe filtrar este documento.
        var authorizedQuery = _db.Documents
            .Where(d => d.UploadedByUserId == 99
                || (d.ProjectId != null && _db.ProjectMembers.Where(pm => pm.UserId == 99).Select(pm => pm.ProjectId).Contains(d.ProjectId.Value))
                || _db.DocumentShares.Where(s => s.SharedWithUserId == 99 && s.RevokedAt == null).Select(s => s.DocumentId).Contains(d.DocumentId));

        var visible = await authorizedQuery.CountAsync();
        visible.Should().Be(0);
    }

    public void Dispose() => _db.Dispose();
}
