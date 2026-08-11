using Api.Infrastructure.Entities.SharedGameCatalog;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain;

/// <summary>
/// Verifica che SharedGameEntity dichiari il token di concorrenza ottimistica nella forma che
/// PostgreSQL può effettivamente popolare. Spec-panel recommendation C-3; #3651.
///
/// Questi test asserivano <c>PropertyType == typeof(byte[])</c> ed erano verdi mentre la
/// protezione non esisteva: il token `bytea` restava NULL su ogni riga da quando #2305 ha rimosso
/// il trigger che lo popolava, quindi EF confrontava NULL = NULL e ogni update passava. Un test
/// che fissa la forma sbagliata certifica il difetto invece di rilevarlo — la verifica di merito
/// (due scritture concorrenti, la seconda rifiutata) sta in
/// <c>SharedGameXminConcurrencyTests</c>, che richiede un vero PostgreSQL.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SharedGameEntityRowVersionTests
{
    [Fact]
    public void SharedGameEntity_HasXminConcurrencyToken()
    {
        // Arrange & Act
        var property = typeof(SharedGameEntity).GetProperty("Xmin");

        // Assert
        property.Should().NotBeNull("SharedGameEntity deve esporre il token di concorrenza xmin");
        property!.PropertyType.Should().Be(
            typeof(uint),
            "xmin è di tipo xid: Npgsql lo mappa solo su una proprietà uint. Con byte[] la colonna " +
            "diventa una bytea che Postgres non valorizza, e la concorrenza ottimistica non scatta");
    }

    [Fact]
    public void SharedGameEntity_NoLongerCarriesTheDeadByteArrayToken()
    {
        // Arrange & Act
        var legacy = typeof(SharedGameEntity).GetProperty("RowVersion");

        // Assert
        legacy.Should().BeNull(
            "il token bytea è stato rimosso da #3651 insieme alla colonna row_version: " +
            "reintrodurlo riporterebbe una protezione dichiarata ma inesistente");
    }
}
