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
/// T115: tests de upload con taskId (snapshot del ProjectId del task).
/// Per AC-4.1.2: el ProjectId del doc se copia del task en el momento de la asociacion
/// y NO se re-evalua si el task cambia de proyecto.
/// </summary>
public class TaskAttachmentTests : IDisposable
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

    public TaskAttachmentTests()
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
            .Returns(call => $"{call.ArgAt<string>(0)}/{call.ArgAt<int?>(1) ?? 0}/guid.pdf");
        _storage.UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns("4/1/guid.pdf");

        _sut = new DocumentService(_db, _storage, _av, _mime, _path, _activityLog, _notifications, _logger);
    }

    [Fact]
    public async Task UploadAsync_WhenTaskIdProvided_ShouldSnapshotProjectIdFromTask()
    {
        // Tarea 1 pertenece al proyecto 1
        _db.Projects.Add(new Project { ProjectId = 1, Name = "Q4", Description = "x", ProjectManagerId = 1, StartDate = DateTime.UtcNow, TargetCompletionDate = DateTime.UtcNow.AddDays(30), Status = ProjectStatus.Active, CreatedDate = DateTime.UtcNow, UpdatedDate = DateTime.UtcNow });
        _db.Tasks.Add(new TaskItem
        {
            TaskId = 1,
            Title = "Test task",
            Description = "x",
            Priority = TaskPriority.Medium,
            Status = Models.TaskStatus.InProgress,
            DueDate = DateTime.UtcNow.AddDays(7),
            AssignedUserId = 4,
            CreatedByUserId = 2,
            ProjectId = 1,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.7 task attachment"));

        var result = await _sut.UploadAsync(
            stream, "task-doc.pdf", "application/pdf",
            "Task attachment", "x", DocumentConstants.CategoryProjectDocuments,
            null, projectId: null, taskId: 1, currentUserId: 4);

        result.DocumentId.Should().BeGreaterThan(0);
        var doc = await _db.Documents.FindAsync(result.DocumentId);
        doc!.ProjectId.Should().Be(1); // snapshot del task
        doc.TaskId.Should().Be(1);
    }

    [Fact]
    public async Task UploadAsync_WhenTaskChangesProjectLater_NewDocumentKeepsOriginalProjectId()
    {
        // AC-4.1.2: si el task cambia de proyecto, los docs ya subidos NO se re-evaluan
        _db.Projects.AddRange(
            new Project { ProjectId = 1, Name = "Q4", Description = "x", ProjectManagerId = 1, StartDate = DateTime.UtcNow, TargetCompletionDate = DateTime.UtcNow.AddDays(30), Status = ProjectStatus.Active, CreatedDate = DateTime.UtcNow, UpdatedDate = DateTime.UtcNow },
            new Project { ProjectId = 2, Name = "Q1", Description = "y", ProjectManagerId = 1, StartDate = DateTime.UtcNow, TargetCompletionDate = DateTime.UtcNow.AddDays(30), Status = ProjectStatus.Active, CreatedDate = DateTime.UtcNow, UpdatedDate = DateTime.UtcNow });
        _db.Tasks.Add(new TaskItem
        {
            TaskId = 1, Title = "t", Description = "x", Priority = TaskPriority.Medium,
            Status = Models.TaskStatus.InProgress, DueDate = DateTime.UtcNow.AddDays(7),
            AssignedUserId = 4, CreatedByUserId = 2, ProjectId = 1,
            CreatedDate = DateTime.UtcNow, UpdatedDate = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.7 first attachment"));

        var result = await _sut.UploadAsync(
            stream, "doc.pdf", "application/pdf",
            "First", "x", DocumentConstants.CategoryProjectDocuments,
            null, projectId: null, taskId: 1, currentUserId: 4);

        // Simulamos cambio de proyecto del task (migración)
        var task = await _db.Tasks.FindAsync(1);
        task!.ProjectId = 2;
        await _db.SaveChangesAsync();

        // El doc sigue con ProjectId=1 (snapshot original)
        var doc = await _db.Documents.FindAsync(result.DocumentId);
        doc!.ProjectId.Should().Be(1);
    }

    public void Dispose() => _db.Dispose();
}
