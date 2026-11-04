using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Xunit;

namespace Api.Tests.SharedKernel.Infrastructure;

/// <summary>
/// #4839 — <see cref="PersistenceFailure.IsInfrastructure"/> decides whether an event handler lets a failure go up
/// (the outbox retries it) or swallows it (it would fail the same way at every retry).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "4839")]
public sealed class PersistenceFailureTests
{
    [Fact]
    public void IsInfrastructure_ATimeout_IsTrue() =>
        PersistenceFailure.IsInfrastructure(new TimeoutException()).Should().BeTrue();

    [Fact]
    public void IsInfrastructure_AnExhaustedRetryOfTheExecutionStrategy_IsTrue() =>
        PersistenceFailure.IsInfrastructure(new RetryLimitExceededException("gave up", new TimeoutException()))
            .Should().BeTrue();

    [Theory]
    [InlineData("40P01")] // deadlock detected
    [InlineData("57P03")] // cannot connect now (server starting up)
    [InlineData("53300")] // too many connections
    public void IsInfrastructure_ATransientPostgresError_IsTrue_EvenWrappedBySaveChanges(string sqlState)
    {
        var postgres = new PostgresException("transient", "ERROR", "ERROR", sqlState);

        PersistenceFailure.IsInfrastructure(postgres).Should().BeTrue(sqlState);
        PersistenceFailure.IsInfrastructure(new DbUpdateException("save failed", postgres)).Should().BeTrue(sqlState);
    }

    [Fact]
    public void IsInfrastructure_ALostConnection_IsTrue() =>
        PersistenceFailure.IsInfrastructure(new DbUpdateException("save failed", new NpgsqlException("lost", new IOException())))
            .Should().BeTrue();

    [Theory]
    [InlineData("23503")] // foreign key violation: the row the record points at is gone
    [InlineData("23505")] // unique violation: somebody else wrote it
    [InlineData("22001")] // value too long for the column
    public void IsInfrastructure_APostgresRefusalOfTheData_IsFalse(string sqlState)
    {
        var postgres = new PostgresException("refused", "ERROR", "ERROR", sqlState);

        PersistenceFailure.IsInfrastructure(new DbUpdateException("save failed", postgres)).Should().BeFalse(sqlState);
    }

    [Fact]
    public void IsInfrastructure_AValidationOfTheRecord_IsFalse() =>
        PersistenceFailure.IsInfrastructure(new ValidationException("Duration cannot exceed 30 days")).Should().BeFalse();

    [Fact]
    public void IsInfrastructure_ABugInTheCode_IsFalse() =>
        PersistenceFailure.IsInfrastructure(new InvalidOperationException("sequence contains no elements")).Should().BeFalse();

    [Fact]
    public void IsInfrastructure_NullException_Throws()
    {
        var act = () => PersistenceFailure.IsInfrastructure(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
