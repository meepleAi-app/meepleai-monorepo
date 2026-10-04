using Api.BoundedContexts.Testing.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.Testing;

/// <summary>
/// Issue #4054 — la mappatura SQLSTATE → 4xx della rete dei seeder, provata sul seam testabile.
/// </summary>
/// <remarks>
/// Le proprietà di <c>Npgsql.PostgresException</c> non sono settabili, quindi il predicato e la
/// descrizione prendono i campi d'errore già estratti: è la stessa separazione che
/// <c>PlayRecordVersionRepository.IsVersionNumberConflict</c> usa per lo stesso motivo. Il giro
/// completo contro Postgres reale — incluso il fatto che <c>constraint_name</c> arrivi davvero — sta
/// in <c>TestSeedConstraintViolationIntegrationTests</c>: questi test coprono la tabella di verità,
/// non la connessione.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Testing")]
[Trait("Issue", "4054")]
public sealed class TestSeedPersistenceTests
{
    [Theory]
    [InlineData("23505")] // unique_violation
    [InlineData("23503")] // foreign_key_violation
    public void CallerAttributable_CoversTheTwoConstraintViolations(string sqlState)
    {
        TestSeedPersistence.IsCallerAttributable(sqlState).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("23502")] // not_null_violation: un seeder che omette una colonna è un BUG del seeder
    [InlineData("40001")] // serialization_failure
    [InlineData("57014")] // query_canceled
    [InlineData("08006")] // connection_failure
    public void CallerAttributable_LeavesEveryOtherFailureAsA500(string? sqlState)
    {
        // Il raggio è la parte che decide se la rete aiuta o nasconde: tradurre un guasto del
        // server in un 4xx direbbe al chiamante di correggere un input che era giusto, e
        // toglierebbe il 500 a chi deve accorgersene.
        TestSeedPersistence.IsCallerAttributable(sqlState).Should().BeFalse();
    }

    [Fact]
    public void Describe_UniqueViolation_IsA409ThatNamesConstraintAndTable()
    {
        var described = TestSeedPersistence.Describe("23505", "IX_users_Email", "users", inner: null);

        described.Should().BeOfType<ConflictException>();
        described.StatusCode.Should().Be(409);
        described.ErrorCode.Should().Be("conflict");
        described.Message.Should().Contain("IX_users_Email").And.Contain("users");
    }

    [Fact]
    public void Describe_ForeignKeyViolation_IsA400ThatNamesConstraintAndTable()
    {
        var described = TestSeedPersistence.Describe(
            "23503", "FK_game_night_rsvps_game_night_events_event_id", "game_night_rsvps", inner: null);

        described.Should().BeOfType<BadRequestException>();
        described.StatusCode.Should().Be(400);
        described.ErrorCode.Should().Be("bad_request");
        described.Message.Should().Contain("FK_game_night_rsvps_game_night_events_event_id")
            .And.Contain("game_night_rsvps");
    }

    [Fact]
    public void Describe_KeepsTheOriginalFailureAsInnerException()
    {
        // Senza l'inner, la traduzione CANCELLA l'unica copia del dettaglio Postgres: il chiamante
        // guadagna un messaggio leggibile e chi deve indagare perde lo stack. Entrambi servono.
        var original = new InvalidOperationException("the original Npgsql failure");

        var described = TestSeedPersistence.Describe("23505", "IX_users_Email", "users", original);

        described.InnerException.Should().BeSameAs(original);
    }
}
