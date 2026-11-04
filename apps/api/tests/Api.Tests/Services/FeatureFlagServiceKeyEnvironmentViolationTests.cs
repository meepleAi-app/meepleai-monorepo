using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// #4599 — <c>FeatureFlagService</c> recupera solo la violazione dell'indice univoco su
/// <c>(Key, Environment)</c>: SQLSTATE 23505 <b>e</b> quel nome di vincolo. Il predicato è puro, sul modello
/// di <c>NotificationDedupePipelineBehavior.IsNotificationDedupViolation</c>.
/// </summary>
/// <remarks>
/// Il nome <c>IX_system_configurations_Key_Environment</c> è scritto qui per esteso, non preso da
/// <c>FeatureFlagService.KeyEnvironmentIndexName</c>: un test che confronta la costante con sé stessa non vede
/// una modifica della costante. Che il nome sia quello vero lo prova
/// <c>FeatureFlagConcurrentCreateIntegrationTests</c> contro lo schema migrato.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
[Trait("Issue", "4599")]
public class FeatureFlagServiceKeyEnvironmentViolationTests
{
    [Fact]
    public void TheIndexNameIsTheOneEfGivesToTheKeyEnvironmentIndex() =>
        FeatureFlagService.KeyEnvironmentIndexName.Should().Be("IX_system_configurations_Key_Environment");

    [Fact]
    public void UniqueViolationOnTheKeyEnvironmentIndex_IsRecognised() =>
        FeatureFlagService.IsKeyEnvironmentViolation(
            PostgresErrorCodes.UniqueViolation, "IX_system_configurations_Key_Environment").Should().BeTrue();

    [Theory]
    [InlineData("23505", "PK_system_configurations")] // another unique constraint of the same table
    [InlineData("23505", "IX_system_configurations_Category")]
    [InlineData("23505", null)]
    [InlineData("23505", "")]
    [InlineData("23503", "IX_system_configurations_Key_Environment")] // foreign_key_violation
    [InlineData("40001", "IX_system_configurations_Key_Environment")] // serialization_failure
    [InlineData(null, "IX_system_configurations_Key_Environment")]
    [InlineData(null, null)]
    public void AnythingElse_IsNotRecognised(string? sqlState, string? constraintName) =>
        FeatureFlagService.IsKeyEnvironmentViolation(sqlState, constraintName).Should().BeFalse();

    [Fact]
    public void TheConstraintNameIsCaseSensitive() =>
        FeatureFlagService.IsKeyEnvironmentViolation(
            PostgresErrorCodes.UniqueViolation, "ix_system_configurations_key_environment").Should().BeFalse(
            "Postgres reports the name as created; a different spelling is a different constraint");
}
