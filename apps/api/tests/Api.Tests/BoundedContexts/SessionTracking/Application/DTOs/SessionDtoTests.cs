using Api.BoundedContexts.SessionTracking.Application.DTOs;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.DTOs;

public class SessionDtoTests
{
    [Fact]
    public void DefaultDto_ScoringType_And_ScoreData_AreNull()
    {
        var dto = new SessionDto();

        dto.ScoringType.Should().BeNull();
        dto.ScoreData.Should().BeNull();
    }

    [Fact]
    public void Dto_With_Scoring_Roundtrips_Both_Fields()
    {
        var dto = new SessionDto
        {
            ScoringType = "Points",
            ScoreData = "{\"scores\":[]}"
        };

        dto.ScoringType.Should().Be("Points");
        dto.ScoreData.Should().Be("{\"scores\":[]}");
    }
}
