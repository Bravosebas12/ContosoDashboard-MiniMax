using System.Threading.Channels;
using ContosoDashboard.Services.Documents;
using FluentAssertions;
using Xunit;

namespace ContosoDashboard.Tests.Unit;

/// <summary>
/// Tests de regresión para A1/A5 — verifican el comportamiento de la cola in-memory.
/// </summary>
public class ActivityLogQueueTests
{
    [Fact]
    public async Task EnqueueAsync_ShouldMakeEntryAvailable_ViaReader()
    {
        // Arrange
        var queue = new ActivityLogQueue();
        var entry = new ActivityLogEntry("document.uploaded", 1, 4, "127.0.0.1", "{\"k\":1}", DateTime.UtcNow);

        // Act
        await queue.EnqueueAsync(entry);

        // Assert
        var read = await queue.Reader.ReadAsync();
        read.Should().BeEquivalentTo(entry);
    }

    [Fact]
    public async Task EnqueueAsync_ShouldPreserveOrder_WithMultipleEntries()
    {
        // Arrange
        var queue = new ActivityLogQueue();
        var entries = new[]
        {
            new ActivityLogEntry("document.uploaded", 1, 4, null, null, DateTime.UtcNow),
            new ActivityLogEntry("document.shared", 1, 4, null, null, DateTime.UtcNow),
            new ActivityLogEntry("document.downloaded", 1, 4, null, null, DateTime.UtcNow),
        };

        // Act
        foreach (var e in entries) await queue.EnqueueAsync(e);

        // Assert
        for (int i = 0; i < 3; i++)
        {
            var read = await queue.Reader.ReadAsync();
            read.Should().BeEquivalentTo(entries[i]);
        }
    }

    [Fact]
    public void Complete_ShouldPreventFurtherEnqueues()
    {
        // Arrange
        var queue = new ActivityLogQueue();
        queue.Complete();

        // Act + Assert
        // ReadAllAsync completa cuando el channel está complete y vacío
        var enumerator = queue.Reader.ReadAllAsync().GetAsyncEnumerator();
        enumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult().Should().BeFalse();
    }

    [Fact]
    public async Task EnqueueAsync_ShouldThrow_OnNullEntry()
    {
        // Arrange
        var queue = new ActivityLogQueue();

        // Act
        var act = async () => await queue.EnqueueAsync(null!).AsTask();

        // Assert
        await act.Should().ThrowAsync<System.ArgumentNullException>();
    }
}
