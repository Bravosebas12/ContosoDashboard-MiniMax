using System;
using System.IO;
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
/// Tests T055 + T057: autorizacion de projectId en UploadAsync y notificacion a team members.
/// </summary>
public class ProjectMembershipAuthorizationTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IAntivirusScanner _av;
    private readonly IMimeTypeValidator _mime;
    private readonly IFilePathBuilder _path;
    private readonly IActivityLogService _activityLog;
    private readonly INotificationService _notifications;
    private readonly ILogger<DocumentService> _logger;
    private readonly DocumentService _sut;

    public ProjectMembershipAuthorizationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        _db = new ApplicationDbContext(options);
        _storage = Substitute.For<IFileStorageService>();
        _av = Substitute.For<IAntivirusScanner>();
        _mime = Substitute.For<IMimeTypeValidator>();
        _path = Substitute.For<IFilePathBuilder>();
        _activityLog = Substitute.For<IActivityLogService>();
        _notifications = Substitute.For<INotificationService>();
        _logger = Substitute.For<ILogger<DocumentService>>();

        _av.ScanAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new ScanResult(ScanStatus.Clean, null, TimeSpan.FromMilliseconds(10), "test"));
        _mime.ValidateAndDetectAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(DocumentConstants.MimePdf);
        _path.BuildPath(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .Returns(call => $"{call.ArgAt<string>(0)}/personal/guid.pdf");
        _storage.UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns("1/personal/guid.pdf");

        _sut = new DocumentService(_db, _storage, _av, _mime, _path, _activityLog, _notifications, _logger);
    }

    [Fact]
    public async Task UploadAsync_ShouldThrowValidation_WhenProjectIdProvidedAndUserNotMember()
    {
        // ProjectId=1 pertenece a otro PM (userId=2), currentUserId=99 no es miembro ni PM
        _db.Projects.Add(new Project
        {
            ProjectId = 1, Name = "Q4 Roadmap", Description = "x", ProjectManagerId = 2,
            StartDate = DateTime.UtcNow, TargetCompletionDate = DateTime.UtcNow.AddDays(30),
            Status = ProjectStatus.Active, CreatedDate = DateTime.UtcNow, UpdatedDate = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.7 content"));

        var act = async () => await _sut.UploadAsync(
            stream, "file.pdf", "application/pdf",
            "Title", "desc", DocumentConstants.CategoryProjectDocuments,
            null, projectId: 1, taskId: null, currentUserId: 99);

        await act.Should().ThrowAsync<DocumentValidationException>()
            .Where(e => e.Errors.Any(msg => msg.Contains("project", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task UploadAsync_ShouldAccept_WhenProjectManagerUploads()
    {
        // El userId=2 es el PM del proyecto 1
        _db.Projects.Add(new Project
        {
            ProjectId = 1, Name = "Q4 Roadmap", Description = "x", ProjectManagerId = 2,
            StartDate = DateTime.UtcNow, TargetCompletionDate = DateTime.UtcNow.AddDays(30),
            Status = ProjectStatus.Active, CreatedDate = DateTime.UtcNow, UpdatedDate = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.7 content"));

        var result = await _sut.UploadAsync(
            stream, "file.pdf", "application/pdf",
            "Q4 Status", "x", DocumentConstants.CategoryProjectDocuments,
            null, projectId: 1, taskId: null, currentUserId: 2);

        result.DocumentId.Should().BeGreaterThan(0);
        (await _db.Documents.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UploadAsync_ShouldEnqueueNotifications_ToOtherProjectMembers()
    {
        _db.Projects.Add(new Project
        {
            ProjectId = 1, Name = "Q4 Roadmap", Description = "x", ProjectManagerId = 2,
            StartDate = DateTime.UtcNow, TargetCompletionDate = DateTime.UtcNow.AddDays(30),
            Status = ProjectStatus.Active, CreatedDate = DateTime.UtcNow, UpdatedDate = DateTime.UtcNow
        });
        _db.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = 1, UserId = 3, Role = "TeamLead", AssignedDate = DateTime.UtcNow.AddDays(-10) },
            new ProjectMember { ProjectId = 1, UserId = 4, Role = "Developer", AssignedDate = DateTime.UtcNow.AddDays(-5) }
        );
        await _db.SaveChangesAsync();

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.7 content"));

        await _sut.UploadAsync(
            stream, "file.pdf", "application/pdf",
            "Q4 Status", "x", DocumentConstants.CategoryProjectDocuments,
            null, projectId: 1, taskId: null, currentUserId: 2);

        // T057: el uploader (PM userId=2) NO se notifica, pero los 2 miembros sí
        await _notifications.Received(1).EnqueueAsync(
            Arg.Is<Notification>(n => n.UserId == 3),
            Arg.Any<System.Threading.CancellationToken>());
        await _notifications.Received(1).EnqueueAsync(
            Arg.Is<Notification>(n => n.UserId == 4),
            Arg.Any<System.Threading.CancellationToken>());
        await _notifications.DidNotReceive().EnqueueAsync(
            Arg.Is<Notification>(n => n.UserId == 2),
            Arg.Any<System.Threading.CancellationToken>());
    }

    [Fact]
    public async Task UploadAsync_ShouldAccept_WhenTeamMemberUploads()
    {
        // userId=4 es miembro del proyecto (no PM)
        _db.Projects.Add(new Project
        {
            ProjectId = 1, Name = "Q4 Roadmap", Description = "x", ProjectManagerId = 2,
            StartDate = DateTime.UtcNow, TargetCompletionDate = DateTime.UtcNow.AddDays(30),
            Status = ProjectStatus.Active, CreatedDate = DateTime.UtcNow, UpdatedDate = DateTime.UtcNow
        });
        _db.ProjectMembers.Add(new ProjectMember { ProjectId = 1, UserId = 4, Role = "Developer", AssignedDate = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.7 content"));

        var result = await _sut.UploadAsync(
            stream, "file.pdf", "application/pdf",
            "Dev notes", "x", DocumentConstants.CategoryProjectDocuments,
            null, projectId: 1, taskId: null, currentUserId: 4);

        result.DocumentId.Should().BeGreaterThan(0);
    }

    public void Dispose() => _db.Dispose();
}
