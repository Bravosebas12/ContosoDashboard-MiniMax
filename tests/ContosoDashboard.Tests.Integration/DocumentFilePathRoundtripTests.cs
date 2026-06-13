using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ContosoDashboard.Data;
using ContosoDashboard.Services;
using ContosoDashboard.Services.Documents;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace ContosoDashboard.Tests.Integration;

/// <summary>
/// Regression test for the file-path roundtrip bug. Previously
/// <see cref="LocalFileStorageService.UploadAsync"/> generated its OWN GUID
/// (independent from <see cref="IFilePathBuilder"/>), so
/// <see cref="Models.Document.FilePath"/> in the DB never matched the file
/// on disk. Preview and Download would throw <c>FileNotFoundException</c>
/// because they trusted the DB path.
///
/// These tests use the REAL <see cref="LocalFileStorageService"/> (no mock)
/// against a temp directory, then verify the roundtrip is consistent.
/// </summary>
public class DocumentFilePathRoundtripTests : IDisposable
{
    private readonly string _tempRoot =
        Path.Combine(Path.GetTempPath(), "contoso-roundtrip", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task UploadAsync_FilePathInDb_ShouldResolveToRealFileOnDisk()
    {
        // Arrange — REAL LocalFileStorageService, not a mock.
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var db = new ApplicationDbContext(options);

        var storageLogger = Substitute.For<ILogger<LocalFileStorageService>>();
        var storage = new LocalFileStorageService(storageLogger, _tempRoot);

        var av = Substitute.For<IAntivirusScanner>();
        av.ScanAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new ScanResult(ScanStatus.Clean, null, TimeSpan.FromMilliseconds(10), "test"));

        var activity = Substitute.For<IActivityLogService>();
        var notifications = Substitute.For<INotificationService>();
        var logger = Substitute.For<ILogger<DocumentService>>();

        var service = new DocumentService(
            db, storage, av, new MimeTypeValidator(), new FilePathBuilder(),
            activity, notifications, logger);

        // Act
        const int userId = 4;
        var pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.7 roundtrip-content");
        await using var stream = new MemoryStream(pdfBytes);
        var result = await service.UploadAsync(
            stream, "roundtrip.pdf", "application/pdf",
            "Roundtrip", "desc",
            DocumentConstants.CategoryPersonalFiles,
            "tag1", projectId: null, taskId: null, currentUserId: userId);

        // Assert — path returned equals path persisted in DB
        var document = await db.Documents.FindAsync(result.DocumentId);
        document.Should().NotBeNull();
        var filePathInDb = document!.FilePath;
        filePathInDb.Should().Be(result.FilePath,
            "the FilePath returned by UploadAsync must equal the one persisted in Document.FilePath");

        // CRITICAL: the file must actually exist on disk at the recorded path.
        var fullPath = Path.Combine(
            _tempRoot,
            filePathInDb.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(fullPath).Should().BeTrue(
            $"the file '{filePathInDb}' recorded in DB must exist on disk at '{fullPath}'");

        // Contents preserved byte-for-byte.
        File.ReadAllBytes(fullPath).Should().Equal(pdfBytes);
    }

    [Fact]
    public async Task OpenForDownloadAsync_AfterUpload_ShouldReturnExactFileBytes()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var db = new ApplicationDbContext(options);

        var storageLogger = Substitute.For<ILogger<LocalFileStorageService>>();
        var storage = new LocalFileStorageService(storageLogger, _tempRoot);

        var av = Substitute.For<IAntivirusScanner>();
        av.ScanAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new ScanResult(ScanStatus.Clean, null, TimeSpan.FromMilliseconds(10), "test"));

        var activity = Substitute.For<IActivityLogService>();
        var notifications = Substitute.For<INotificationService>();
        var logger = Substitute.For<ILogger<DocumentService>>();

        var service = new DocumentService(
            db, storage, av, new MimeTypeValidator(), new FilePathBuilder(),
            activity, notifications, logger);

        // Act — upload, then read back via OpenForDownloadAsync
        const int userId = 7;
        var pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.7 download-roundtrip");
        await using var uploadStream = new MemoryStream(pdfBytes);
        var uploaded = await service.UploadAsync(
            uploadStream, "download-test.pdf", "application/pdf",
            "Download roundtrip", "desc",
            DocumentConstants.CategoryPersonalFiles,
            null, null, null, userId);

        var (downloadStream, fileName, contentType) =
            await service.OpenForDownloadAsync(uploaded.DocumentId, userId);

        // Assert
        fileName.Should().Be("download-test.pdf");
        contentType.Should().Be("application/pdf");
        await using var ms = new MemoryStream();
        await downloadStream.CopyToAsync(ms);
        ms.ToArray().Should().Equal(pdfBytes,
            "the bytes returned by OpenForDownloadAsync must match what was uploaded");
        await downloadStream.DisposeAsync();
    }

    [Fact]
    public async Task ReplaceFileAsync_OldFileRemoved_NewFileReadable()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var db = new ApplicationDbContext(options);

        var storageLogger = Substitute.For<ILogger<LocalFileStorageService>>();
        var storage = new LocalFileStorageService(storageLogger, _tempRoot);

        var av = Substitute.For<IAntivirusScanner>();
        av.ScanAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new ScanResult(ScanStatus.Clean, null, TimeSpan.FromMilliseconds(10), "test"));

        var activity = Substitute.For<IActivityLogService>();
        var notifications = Substitute.For<INotificationService>();
        var logger = Substitute.For<ILogger<DocumentService>>();

        var service = new DocumentService(
            db, storage, av, new MimeTypeValidator(), new FilePathBuilder(),
            activity, notifications, logger);

        const int userId = 9;
        var originalBytes = Encoding.ASCII.GetBytes("%PDF-1.7 original");
        await using var s1 = new MemoryStream(originalBytes);
        var uploaded = await service.UploadAsync(
            s1, "replace-me.pdf", "application/pdf",
            "Original", "desc",
            DocumentConstants.CategoryPersonalFiles,
            null, null, null, userId);
        var oldPath = Path.Combine(_tempRoot,
            uploaded.FilePath!.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(oldPath).Should().BeTrue("original file must be on disk after upload");

        // Act
        var newBytes = Encoding.ASCII.GetBytes("%PDF-1.7 replaced");
        await using var s2 = new MemoryStream(newBytes);
        await service.ReplaceFileAsync(
            uploaded.DocumentId, s2, "replaced.pdf", "application/pdf", userId);

        // Assert
        File.Exists(oldPath).Should().BeFalse("old file must be removed after replace");
        var (downloadStream, _, _) =
            await service.OpenForDownloadAsync(uploaded.DocumentId, userId);
        await using var ms = new MemoryStream();
        await downloadStream.CopyToAsync(ms);
        ms.ToArray().Should().Equal(newBytes,
            "OpenForDownloadAsync after replace must return the new bytes");
        await downloadStream.DisposeAsync();
    }

    public void Dispose()
    {
        // Cleanup is best-effort. On Windows, antivirus or delayed file handle
        // release can keep the temp dir busy; we don't want CI to fail because
        // of that. Use GC + small delay as a last resort.
        if (!Directory.Exists(_tempRoot)) return;
        try
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // Best-effort. The OS will eventually clean %TEMP%.
        }
    }
}
