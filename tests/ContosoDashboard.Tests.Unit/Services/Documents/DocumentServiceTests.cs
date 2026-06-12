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

namespace ContosoDashboard.Tests.Unit.Services.Documents;

public class DocumentServiceTests
{
    [Fact]
    public async Task UploadAsync_ShouldThrowValidationException_WhenTitleIsMissing()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.7 content"));

        var act = async () => await service.UploadAsync(
            stream,
            "file.pdf",
            "application/pdf",
            string.Empty,
            "desc",
            DocumentConstants.CategoryPersonalFiles,
            null,
            null,
            null,
            4);

        await act.Should().ThrowAsync<DocumentValidationException>();
    }

    [Fact]
    public async Task UploadAsync_ShouldPersistDocument_WhenInputIsValid()
    {
        await using var db = CreateDbContext();
        var storage = Substitute.For<IFileStorageService>();
        storage.UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns("4/personal/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.pdf");

        var av = Substitute.For<IAntivirusScanner>();
        av.ScanAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new ScanResult(ScanStatus.Clean, null, TimeSpan.FromMilliseconds(10), "test"));

        var mime = Substitute.For<IMimeTypeValidator>();
        mime.ValidateAndDetectAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(DocumentConstants.MimePdf);

        var pathBuilder = Substitute.For<IFilePathBuilder>();
        pathBuilder.BuildPath("4", null, "pdf").Returns("4/personal/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.pdf");

        var activity = Substitute.For<IActivityLogService>();
        var notifications = Substitute.For<INotificationService>();
        var logger = Substitute.For<ILogger<DocumentService>>();

        var service = new DocumentService(db, storage, av, mime, pathBuilder, activity, notifications, logger);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.7 content"));

        var result = await service.UploadAsync(
            stream,
            "file.pdf",
            "application/pdf",
            "My file",
            "desc",
            DocumentConstants.CategoryPersonalFiles,
            "tag1",
            null,
            null,
            4);

        result.DocumentId.Should().BeGreaterThan(0);
        (await db.Documents.CountAsync()).Should().Be(1);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static DocumentService CreateService(ApplicationDbContext db)
    {
        var storage = Substitute.For<IFileStorageService>();
        var av = Substitute.For<IAntivirusScanner>();
        av.ScanAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new ScanResult(ScanStatus.Clean, null, TimeSpan.FromMilliseconds(10), "test"));

        var mime = Substitute.For<IMimeTypeValidator>();
        mime.ValidateAndDetectAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(DocumentConstants.MimePdf);

        var pathBuilder = Substitute.For<IFilePathBuilder>();
        pathBuilder.BuildPath(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .Returns("4/personal/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.pdf");

        var activity = Substitute.For<IActivityLogService>();
        var notifications = Substitute.For<INotificationService>();
        var logger = Substitute.For<ILogger<DocumentService>>();

        return new DocumentService(db, storage, av, mime, pathBuilder, activity, notifications, logger);
    }
}
