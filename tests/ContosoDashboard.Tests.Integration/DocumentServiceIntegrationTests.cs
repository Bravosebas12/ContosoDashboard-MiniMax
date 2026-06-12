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

public class DocumentServiceIntegrationTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "contoso-integration", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task UploadAsync_ShouldPersistDbRow_AndWriteFileToDisk()
    {
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
            db,
            storage,
            av,
            new MimeTypeValidator(),
            new FilePathBuilder(),
            activity,
            notifications,
            logger);

        await using var stream = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7 integration"));
        var result = await service.UploadAsync(
            stream,
            "integration.pdf",
            "application/pdf",
            "Integration upload",
            "desc",
            DocumentConstants.CategoryPersonalFiles,
            "integration",
            null,
            null,
            4);

        result.DocumentId.Should().BeGreaterThan(0);
        (await db.Documents.CountAsync()).Should().Be(1);
        Directory.GetFiles(_tempRoot, "*", SearchOption.AllDirectories).Should().NotBeEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
