using System;
using System.Linq;
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
/// Tests T095: las 3 reglas de authorization de sharing per Clarifications Q1.
/// - Owner (uploader) puede compartir con cualquier usuario.
/// - Project Manager solo puede compartir dentro de su proyecto.
/// - Otros roles no pueden compartir (403 / unauthorized).
/// </summary>
public class DocumentShareServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly IActivityLogService _activityLog;
    private readonly INotificationService _notifications;
    private readonly ILogger<DocumentShareService> _logger;
    private readonly DocumentShareService _sut;

    public DocumentShareServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        _db = new ApplicationDbContext(options);
        _activityLog = Substitute.For<IActivityLogService>();
        _notifications = Substitute.For<INotificationService>();
        _logger = Substitute.For<ILogger<DocumentShareService>>();
        _sut = new DocumentShareService(_db, _notifications, _activityLog, _logger);
    }

    [Fact]
    public async Task ShareAsync_ShouldThrow_WhenSelfShare()
    {
        _db.Users.Add(NewUser(1, "alice", "Alice", UserRole.Employee));
        _db.Documents.Add(NewDoc(1, uploader: 1));
        await _db.SaveChangesAsync();

        var act = async () => await _sut.ShareAsync(
            new ShareRequest(DocumentId: 1, TargetUserId: 1, Permission: DocumentSharePermission.Read, ExpiresAt: null),
            currentUserId: 1);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*yourself*");
    }

    [Fact]
    public async Task ShareAsync_ShouldThrowUnauthorized_WhenNonOwnerAndNonPM()
    {
        // userId=4 es un Employee que NO es dueno del doc y NO es PM del proyecto
        _db.Users.Add(NewUser(2, "uploader", "Camille", UserRole.Employee));
        _db.Users.Add(NewUser(3, "target", "Target", UserRole.Employee));
        _db.Users.Add(NewUser(4, "intruder", "Ni", UserRole.Employee));
        _db.Projects.Add(new Project { ProjectId = 1, Name = "A", Description = "x", ProjectManagerId = 2, StartDate = DateTime.UtcNow, TargetCompletionDate = DateTime.UtcNow.AddDays(30), Status = ProjectStatus.Active, CreatedDate = DateTime.UtcNow, UpdatedDate = DateTime.UtcNow });
        _db.Documents.Add(NewDoc(1, uploader: 2, projectId: 1));
        await _db.SaveChangesAsync();

        var act = async () => await _sut.ShareAsync(
            new ShareRequest(DocumentId: 1, TargetUserId: 3, Permission: DocumentSharePermission.Read, ExpiresAt: null),
            currentUserId: 4);

        await act.Should().ThrowAsync<DocumentUnauthorizedAccessException>();
    }

    [Fact]
    public async Task ShareAsync_ShouldAccept_WhenOwnerSharesWithAnyone()
    {
        _db.Users.AddRange(
            NewUser(2, "uploader", "Camille", UserRole.ProjectManager),
            NewUser(4, "target", "Ni", UserRole.Employee));
        _db.Projects.Add(new Project { ProjectId = 1, Name = "A", Description = "x", ProjectManagerId = 2, StartDate = DateTime.UtcNow, TargetCompletionDate = DateTime.UtcNow.AddDays(30), Status = ProjectStatus.Active, CreatedDate = DateTime.UtcNow, UpdatedDate = DateTime.UtcNow });
        _db.Documents.Add(NewDoc(1, uploader: 2, projectId: 1));
        await _db.SaveChangesAsync();

        var result = await _sut.ShareAsync(
            new ShareRequest(DocumentId: 1, TargetUserId: 4, Permission: DocumentSharePermission.Read, ExpiresAt: null),
            currentUserId: 2);

        result.Should().NotBeNull();
        (await _db.DocumentShares.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ShareAsync_ShouldRestrictPM_ToProjectMembers()
    {
        // userId=2 es PM del proyecto 1, pero target=5 NO es miembro
        _db.Users.AddRange(
            NewUser(2, "pm", "Camille", UserRole.ProjectManager),
            NewUser(5, "outsider", "Out", UserRole.Employee));
        _db.Projects.Add(new Project { ProjectId = 1, Name = "A", Description = "x", ProjectManagerId = 2, StartDate = DateTime.UtcNow, TargetCompletionDate = DateTime.UtcNow.AddDays(30), Status = ProjectStatus.Active, CreatedDate = DateTime.UtcNow, UpdatedDate = DateTime.UtcNow });
        _db.Documents.Add(NewDoc(1, uploader: 3, projectId: 1)); // uploader != PM
        await _db.SaveChangesAsync();

        var act = async () => await _sut.ShareAsync(
            new ShareRequest(DocumentId: 1, TargetUserId: 5, Permission: DocumentSharePermission.Read, ExpiresAt: null),
            currentUserId: 2);

        await act.Should().ThrowAsync<DocumentValidationException>()
            .WithMessage("*within the project*");
    }

    [Fact]
    public async Task ShareAsync_ShouldEnqueueNotificationToTarget()
    {
        _db.Users.AddRange(
            NewUser(2, "uploader", "Camille", UserRole.ProjectManager),
            NewUser(4, "target", "Ni", UserRole.Employee));
        _db.Documents.Add(NewDoc(1, uploader: 2));
        await _db.SaveChangesAsync();

        await _sut.ShareAsync(
            new ShareRequest(DocumentId: 1, TargetUserId: 4, Permission: DocumentSharePermission.Read, ExpiresAt: null),
            currentUserId: 2);

        await _notifications.Received(1).EnqueueAsync(
            Arg.Is<Notification>(n => n.UserId == 4),
            Arg.Any<System.Threading.CancellationToken>());
    }

    private static User NewUser(int id, string email, string name, UserRole role) => new()
    {
        UserId = id,
        Email = $"{email}@contoso.com",
        DisplayName = name,
        Department = "Eng",
        JobTitle = name,
        Role = role,
        AvailabilityStatus = AvailabilityStatus.Available,
        CreatedDate = DateTime.UtcNow,
        EmailNotificationsEnabled = true,
        InAppNotificationsEnabled = true
    };

    private static Document NewDoc(int id, int uploader, int? projectId = null) => new()
    {
        DocumentId = id,
        Title = $"d{id}",
        Category = DocumentConstants.CategoryPersonalFiles,
        FilePath = $"{uploader}/personal/{Guid.NewGuid():N}.pdf",
        FileSize = 1024,
        FileType = "application/pdf",
        UploadedAt = DateTime.UtcNow,
        UploadedByUserId = uploader,
        ProjectId = projectId,
        AvScanStatus = DocumentAvStatus.Clean,
        OriginalFileName = $"d{id}"
    };

    public void Dispose() => _db.Dispose();
}
