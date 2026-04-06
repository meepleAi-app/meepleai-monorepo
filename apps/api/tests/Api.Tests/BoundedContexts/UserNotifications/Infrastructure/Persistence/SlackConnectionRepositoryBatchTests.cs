using Api.BoundedContexts.UserNotifications.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure.Persistence;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserNotifications")]
public sealed class SlackConnectionRepositoryBatchTests
{
    [Fact]
    public void ChunkSize_IsConservativelySet_At500()
    {
        // The chunk size constant prevents PostgreSQL parameter overload.
        // This test documents the agreed limit — change requires spec-panel review.
        const int expectedChunkSize = 500;

        var type = typeof(SlackConnectionRepository);
        var field = type.GetField("BatchChunkSize",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        field.Should().NotBeNull("BatchChunkSize constant should exist in SlackConnectionRepository");
        field!.GetValue(null).Should().Be(expectedChunkSize);
    }
}
