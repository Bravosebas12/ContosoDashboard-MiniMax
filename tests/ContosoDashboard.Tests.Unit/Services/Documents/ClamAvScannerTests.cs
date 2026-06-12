using System.IO;
using System.Text;
using System.Threading.Tasks;
using ContosoDashboard.Services.Documents;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace ContosoDashboard.Tests.Unit.Services.Documents;

public class ClamAvScannerTests
{
    [Fact]
    public async Task ScanAsync_ShouldReturnNotScanned_WhenClamAvUnavailable_AndDegradedModeEnabled()
    {
        var logger = Substitute.For<ILogger<ClamAvScanner>>();
        var options = Options.Create(new AntivirusOptions
        {
            Host = "127.0.0.1",
            Port = 1,
            Timeout = System.TimeSpan.FromMilliseconds(300),
            AllowDegradedMode = true,
        });
        var sut = new ClamAvScanner(logger, options);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));

        var result = await sut.ScanAsync(stream, "file.txt");

        result.Status.Should().Be(ScanStatus.NotScanned);
    }

    [Fact]
    public async Task ScanAsync_ShouldReturnError_WhenClamAvUnavailable_AndDegradedModeDisabled()
    {
        var logger = Substitute.For<ILogger<ClamAvScanner>>();
        var options = Options.Create(new AntivirusOptions
        {
            Host = "127.0.0.1",
            Port = 1,
            Timeout = System.TimeSpan.FromMilliseconds(300),
            AllowDegradedMode = false,
        });
        var sut = new ClamAvScanner(logger, options);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));

        var result = await sut.ScanAsync(stream, "file.txt");

        result.Status.Should().Be(ScanStatus.Error);
    }
}
