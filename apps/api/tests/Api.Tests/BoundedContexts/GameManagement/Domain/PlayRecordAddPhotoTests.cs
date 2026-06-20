using System;
using System.Linq;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2436")]
public class PlayRecordAddPhotoTests
{
    private static PlayRecord NewRecord(Guid creator) =>
        PlayRecord.CreateFreeForm(Guid.NewGuid(), "Catan", creator, DateTime.UtcNow.AddDays(-1),
            PlayRecordVisibility.Private, SessionScoringConfig.CreateDefault());

    [Fact]
    public void AddPhoto_AppendsPhotoAndRaisesEvent()
    {
        var creator = Guid.NewGuid();
        var record = NewRecord(creator);
        record.ClearDomainEvents();
        var photoId = Guid.NewGuid();

        record.AddPhoto(photoId, "blob/u.jpg", "blob/t.jpg", 100, "sha", "ocr", 0.9, "cap", creator);

        record.Photos.Should().HaveCount(1);
        record.Photos[0].Id.Should().Be(photoId);
        record.DomainEvents.OfType<PlayRecordPhotoUploadedEvent>().Should().HaveCount(1);
    }

    [Fact]
    public void AddPhoto_Eleventh_Throws()
    {
        var creator = Guid.NewGuid();
        var record = NewRecord(creator);
        for (var i = 0; i < 10; i++)
            record.AddPhoto(Guid.NewGuid(), $"u{i}", null, 1, $"sha{i}", null, null, null, creator);

        var act = () => record.AddPhoto(Guid.NewGuid(), "u", null, 1, "shaX", null, null, null, creator);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RestorePhoto_AppendsWithoutEvent()
    {
        var record = NewRecord(Guid.NewGuid());
        record.ClearDomainEvents();

        record.RestorePhoto(Guid.NewGuid(), "u", null, 1, "sha", null, null, null, Guid.NewGuid(),
            DateTime.UtcNow);

        record.Photos.Should().HaveCount(1);
        record.DomainEvents.Should().BeEmpty();
    }
}
