using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ContosoDashboard.Services.Documents;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace ContosoDashboard.Tests.Unit.Services.Documents;

public class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly LocalFileStorageService _sut;

    public LocalFileStorageServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "contoso-tests", Guid.NewGuid().ToString("N"));
        var logger = Substitute.For<ILogger<LocalFileStorageService>>();
        _sut = new LocalFileStorageService(logger, _tempRoot);
    }

    [Fact]
    public async Task UploadAndDownload_ShouldRoundTripContent()
    {
        var payload = Encoding.UTF8.GetBytes("hello-document");
        await using var source = new MemoryStream(payload);

        var relativePath = await _sut.UploadAsync(source, "4/personal", "txt");
        await using var downloaded = await _sut.DownloadAsync(relativePath);
        using var reader = new StreamReader(downloaded, Encoding.UTF8);
        var content = await reader.ReadToEndAsync();

        content.Should().Be("hello-document");
    }

    [Fact]
    public async Task DeleteAsync_ShouldBeIdempotent_WhenFileDoesNotExist()
    {
        var act = async () => await _sut.DeleteAsync("4/personal/missing.txt");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DownloadAsync_ShouldThrow_ForPathTraversal()
    {
        var act = async () => await _sut.DownloadAsync("../outside.txt");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
