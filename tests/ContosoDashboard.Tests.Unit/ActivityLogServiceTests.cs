using ContosoDashboard.Services.Documents;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace ContosoDashboard.Tests.Unit;

/// <summary>
/// Tests de regresión para A1/A3/A5 (Opción A — Channel + BackgroundService).
/// Verifican que <see cref="ActivityLogService"/> enqueuea en lugar de hacer SaveChanges directo.
/// </summary>
public class ActivityLogServiceTests
{
    [Fact]
    public async Task LogAsync_ShouldEnqueue_WithoutBlockingOnDbContext()
    {
        // Arrange: mock de la queue (no del DbContext, que ya no se usa)
        var queue = Substitute.For<IActivityLogQueue>();
        var sut = new ActivityLogService(queue, Substitute.For<Microsoft.Extensions.Logging.ILogger<ActivityLogService>>());

        // Act
        var task = sut.LogAsync("document.uploaded", documentId: 42, userId: 4,
            ipAddress: "127.0.0.1",
            metadata: new { fileSize = 1024L });

        // Assert: el ValueTask retornó sin tocar DbContext
        // (Si LogAsync hubiera intentado SaveChangesAsync, necesitaríamos un DbContext real
        // y este test no podría ser unit-test puro.)
        await task;
        await queue.Received(1).EnqueueAsync(
            Arg.Is<ActivityLogEntry>(e =>
                e.Event == "document.uploaded" &&
                e.DocumentId == 42 &&
                e.UserId == 4 &&
                e.IpAddress == "127.0.0.1" &&
                e.MetadataJson!.Contains("1024") &&
                e.Timestamp > DateTime.UtcNow.AddSeconds(-5) &&  // Timestamp reciente
                e.Timestamp <= DateTime.UtcNow),
            Arg.Any<System.Threading.CancellationToken>());
    }

    [Fact]
    public async Task LogAsync_ShouldSerializeMetadataToJson()
    {
        // Arrange
        var queue = Substitute.For<IActivityLogQueue>();
        var sut = new ActivityLogService(queue, Substitute.For<Microsoft.Extensions.Logging.ILogger<ActivityLogService>>());

        // Act
        await sut.LogAsync("document.shared", documentId: 10, userId: 1, metadata: new
        {
            sharedWith = 7,
            permission = "Read",
            expiresAt = (DateTime?)null
        });

        // Assert
        await queue.Received(1).EnqueueAsync(
            Arg.Is<ActivityLogEntry>(e =>
                e.MetadataJson != null &&
                e.MetadataJson.Contains("\"sharedWith\":7") &&
                e.MetadataJson.Contains("\"permission\":\"Read\"")),
            Arg.Any<System.Threading.CancellationToken>());
    }

    [Fact]
    public async Task LogAsync_ShouldHandleNullMetadata_ByLeavingJsonNull()
    {
        // Arrange
        var queue = Substitute.For<IActivityLogQueue>();
        var sut = new ActivityLogService(queue, Substitute.For<Microsoft.Extensions.Logging.ILogger<ActivityLogService>>());

        // Act
        await sut.LogAsync("document.scanned", documentId: null, userId: 1, metadata: null);

        // Assert
        await queue.Received(1).EnqueueAsync(
            Arg.Is<ActivityLogEntry>(e => e.MetadataJson == null && e.DocumentId == null),
            Arg.Any<System.Threading.CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task LogAsync_ShouldThrow_WhenEventIsNullOrWhitespace(string? invalidEvent)
    {
        // Arrange
        var queue = Substitute.For<IActivityLogQueue>();
        var sut = new ActivityLogService(queue, Substitute.For<Microsoft.Extensions.Logging.ILogger<ActivityLogService>>());

        // Act
        var act = async () => await sut.LogAsync(invalidEvent!, documentId: 1, userId: 1).AsTask();

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
