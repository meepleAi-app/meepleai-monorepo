using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// #3844 — abilitare un feature flag per tier funzionava, disabilitarlo dava 500.
///
/// <para>
/// Non erano due percorsi diversi: sono simmetrici, entrambi «cerca, aggiorna se c'e', crea se non
/// c'e'». Il problema e' che la ricerca e' <b>in cache</b>, e in cache finisce anche il «non
/// esiste»: <c>enable</c> cercava (assente, cache negativa), creava la riga, e nessuno invalidava.
/// <c>disable</c> rileggeva quel «non esiste», prendeva il ramo «crea» e violava il vincolo di
/// unicita' su <c>(Key, Environment)</c> — <c>23505</c>, cioe' 500.
/// </para>
/// <para>
/// L'asimmetria del sintomo era la prova: il primo dei due riusciva sempre, il secondo falliva
/// sempre. Il test riproduce esattamente quella sequenza, con una cache che si comporta come
/// quella vera — memorizza anche i null e dimentica solo se qualcuno glielo chiede.
/// </para>
/// <para>
/// #4599 — i test del percorso <b>globale</b> (<c>EnableFeatureAsync</c> / <c>DisableFeatureAsync</c>) stanno
/// in fondo: il codice è lo stesso del percorso per tier (lettura in cache, poi <c>InvalidateAsync</c>) e non
/// aveva un test dedicato. Sono guardie: passano anche sul codice di prima, e smettono di passare se si toglie
/// l'invalidazione da uno dei due rami. Subito dopo, i test del recupero quando la creazione perde la gara con
/// un'altra richiesta (23505): il caso vero, col database vero, è in
/// <c>FeatureFlagConcurrentCreateIntegrationTests</c>.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
[Trait("Issue", "3844")]
[Trait("Issue", "4599")]
public class FeatureFlagServiceCacheInvalidationTests
{
    /// <summary>
    /// Configurazione finta con la stessa insidia di quella vera: memorizza l'esito della ricerca,
    /// <b>compreso il null</b>, e lo dimentica solo su <c>InvalidateAsync</c>.
    /// </summary>
    private sealed class ConfigurazioneConCache : IConfigurationService
    {
        private readonly Dictionary<string, SystemConfigurationDto?> _cache = new(StringComparer.Ordinal);
        private readonly Dictionary<string, SystemConfigurationDto> _archivio = new(StringComparer.Ordinal);

        public int LettureDalDatabase { get; private set; }

        /// <summary>Simula la scrittura fatta altrove (dal comando inviato via mediator).</summary>
        public void ScriviDirettamente(string key, string environment, string valore)
        {
            _archivio[Chiave(key, environment)] = new SystemConfigurationDto(
                Id: Guid.NewGuid().ToString(),
                Key: key,
                Value: valore,
                ValueType: "Boolean",
                Description: null,
                Category: "FeatureFlags",
                IsActive: true,
                RequiresRestart: false,
                Environment: environment,
                Version: 1,
                PreviousValue: null,
                CreatedAt: DateTime.UtcNow,
                UpdatedAt: DateTime.UtcNow,
                CreatedByUserId: Guid.Empty.ToString(),
                UpdatedByUserId: null,
                LastToggledAt: null);
        }

        /// <summary>Simula l'aggiornamento fatto altrove (da <c>UpdateConfigValueCommand</c>).</summary>
        public void AggiornaDirettamente(Guid id, string valore)
        {
            var chiave = _archivio.Single(r => string.Equals(r.Value.Id, id.ToString(), StringComparison.Ordinal)).Key;
            _archivio[chiave] = _archivio[chiave] with { Value = valore };
        }

        public string IdDellaRiga(string key, string environment) => _archivio[Chiave(key, environment)].Id;

        private static string Chiave(string key, string? environment) => $"{key}:{environment}";

        public Task<SystemConfigurationDto?> GetConfigurationByKeyAsync(
            string key, string? environment = null, CancellationToken cancellationToken = default)
        {
            var k = Chiave(key, environment);
            if (_cache.TryGetValue(k, out var inCache))
            {
                return Task.FromResult(inCache);
            }

            LettureDalDatabase++;
            _archivio.TryGetValue(k, out var trovato);
            _cache[k] = trovato;
            return Task.FromResult<SystemConfigurationDto?>(trovato);
        }

        public Task<T?> GetValueAsync<T>(string key, T? defaultValue = default, string? environment = null)
            => Task.FromResult(defaultValue);

        public Task InvalidateAsync(string key, string? environment = null, CancellationToken cancellationToken = default)
        {
            _cache.Remove(Chiave(key, environment));
            return Task.CompletedTask;
        }
    }

    private static ConfigurationDto ConfigurazioneFinta() => new(
        Id: Guid.NewGuid(),
        Key: "prova-flag.Tier.premium",
        Value: "true",
        ValueType: "Boolean",
        Description: null,
        Category: "FeatureFlags",
        IsActive: true,
        RequiresRestart: false,
        Environment: "Development",
        Version: 1,
        CreatedAt: DateTime.UtcNow,
        UpdatedAt: DateTime.UtcNow);

    [Fact]
    public async Task DisabilitareDopoAverAbilitato_AggiornaInveceDiRicreare()
    {
        var config = new ConfigurazioneConCache();
        var comandi = new List<object>();

        var ambiente = new Mock<IWebHostEnvironment>();
        ambiente.SetupGet(e => e.EnvironmentName).Returns("Development");

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<IRequest<ConfigurationDto>>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((c, _) =>
            {
                comandi.Add(c);
                // La creazione scrive davvero: e' quello che rende il secondo tentativo un
                // duplicato, ed e' il fatto che la cache non conosceva.
                if (c is CreateConfigurationCommand creazione)
                {
                    config.ScriviDirettamente(creazione.Key, creazione.Environment!, creazione.Value);
                }
            })
            .ReturnsAsync(() => ConfigurazioneFinta());

        var service = new FeatureFlagService(
            config, mediator.Object, ambiente.Object, NullLogger<FeatureFlagService>.Instance,
            MediatorScopeFactory.For(mediator.Object));

        await service.EnableFeatureForTierAsync("prova-flag", UserTier.Premium, Guid.NewGuid().ToString());
        await service.DisableFeatureForTierAsync("prova-flag", UserTier.Premium, Guid.NewGuid().ToString());

        comandi.OfType<CreateConfigurationCommand>().Should().HaveCount(1,
            "la riga esiste gia' dopo l'abilitazione: una seconda creazione viola il vincolo di " +
            "unicita' su (Key, Environment) e l'endpoint risponde 500 (#3844)");
        comandi.OfType<UpdateConfigValueCommand>().Should().HaveCount(1,
            "disabilitare deve AGGIORNARE la riga esistente");
    }

    [Fact]
    public async Task CicloRipetuto_NonCreaMaiUnaSecondaRiga()
    {
        var config = new ConfigurazioneConCache();
        var creazioni = 0;

        var ambiente = new Mock<IWebHostEnvironment>();
        ambiente.SetupGet(e => e.EnvironmentName).Returns("Development");

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<IRequest<ConfigurationDto>>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((c, _) =>
            {
                if (c is CreateConfigurationCommand creazione)
                {
                    creazioni++;
                    config.ScriviDirettamente(creazione.Key, creazione.Environment!, creazione.Value);
                }
            })
            .ReturnsAsync(() => ConfigurazioneFinta());

        var service = new FeatureFlagService(
            config, mediator.Object, ambiente.Object, NullLogger<FeatureFlagService>.Instance,
            MediatorScopeFactory.For(mediator.Object));

        var utente = Guid.NewGuid().ToString();
        for (var giro = 0; giro < 3; giro++)
        {
            await service.EnableFeatureForTierAsync("prova-flag", UserTier.Premium, utente);
            await service.DisableFeatureForTierAsync("prova-flag", UserTier.Premium, utente);
        }

        creazioni.Should().Be(1, "solo il primissimo passaggio crea la configurazione");
    }

    // ========================================
    // PERCORSO GLOBALE (#4599)
    // ========================================

    private const string Ambiente = "Development";
    private const string FlagGlobale = "prova-flag-globale";

    private sealed record Scenario(FeatureFlagService Service, ConfigurazioneConCache Config, List<object> Comandi);

    /// <summary>
    /// Servizio con la configurazione finta e un mediator che applica davvero i comandi alla configurazione
    /// finta, come farebbe il database. Con <paramref name="creazione"/> la scrittura della creazione la fa
    /// il test: serve a far fallire la creazione come la farebbe l'indice univoco.
    /// </summary>
    private static Scenario CreaScenario(Action<CreateConfigurationCommand, ConfigurazioneConCache>? creazione = null)
    {
        var config = new ConfigurazioneConCache();
        var comandi = new List<object>();

        var ambiente = new Mock<IWebHostEnvironment>();
        ambiente.SetupGet(e => e.EnvironmentName).Returns(Ambiente);

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<IRequest<ConfigurationDto>>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((c, _) =>
            {
                comandi.Add(c);
                switch (c)
                {
                    case CreateConfigurationCommand nuova when creazione is not null:
                        creazione(nuova, config);
                        break;
                    case CreateConfigurationCommand nuova:
                        config.ScriviDirettamente(nuova.Key, nuova.Environment, nuova.Value);
                        break;
                    case UpdateConfigValueCommand aggiornamento:
                        config.AggiornaDirettamente(aggiornamento.ConfigId, aggiornamento.NewValue);
                        break;
                }
            })
            .ReturnsAsync(() => ConfigurazioneFinta());

        var service = new FeatureFlagService(
            config, mediator.Object, ambiente.Object, NullLogger<FeatureFlagService>.Instance,
            MediatorScopeFactory.For(mediator.Object));
        return new Scenario(service, config, comandi);
    }

    private static Task ImpostaGlobaleAsync(FeatureFlagService service, bool abilita) =>
        abilita
            ? service.EnableFeatureAsync(FlagGlobale, userId: Guid.NewGuid().ToString())
            : service.DisableFeatureAsync(FlagGlobale, userId: Guid.NewGuid().ToString());

    [Fact]
    public async Task Globale_DisabilitareDopoAverAbilitato_AggiornaInveceDiRicreare()
    {
        var s = CreaScenario();

        await s.Service.EnableFeatureAsync(FlagGlobale, userId: Guid.NewGuid().ToString());
        await s.Service.DisableFeatureAsync(FlagGlobale, userId: Guid.NewGuid().ToString());

        s.Comandi.OfType<CreateConfigurationCommand>().Should().HaveCount(1,
            "la riga esiste gia' dopo l'abilitazione: una seconda creazione viola il vincolo di " +
            "unicita' su (Key, Environment) e l'endpoint risponde 500 (#3844)");
        s.Comandi.OfType<UpdateConfigValueCommand>().Should().HaveCount(1,
            "disabilitare deve AGGIORNARE la riga esistente");
    }

    [Fact]
    public async Task Globale_CicloRipetuto_NonCreaMaiUnaSecondaRiga()
    {
        var s = CreaScenario();

        for (var giro = 0; giro < 3; giro++)
        {
            await s.Service.EnableFeatureAsync(FlagGlobale, userId: Guid.NewGuid().ToString());
            await s.Service.DisableFeatureAsync(FlagGlobale, userId: Guid.NewGuid().ToString());
        }

        s.Comandi.OfType<CreateConfigurationCommand>().Should().HaveCount(1, "solo il primissimo passaggio crea la configurazione");
        s.Comandi.OfType<UpdateConfigValueCommand>().Should().HaveCount(5);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Globale_ChiaveSenzaRigaConIlNullInCache_LaLetturaSuccessivaVedeLaRigaCreata(bool abilita)
    {
        var s = CreaScenario();
        (await s.Config.GetConfigurationByKeyAsync(FlagGlobale, Ambiente)).Should().BeNull(
            "setup: la prima lettura memorizza anche il «non esiste»");

        await ImpostaGlobaleAsync(s.Service, abilita);

        var dopo = await s.Config.GetConfigurationByKeyAsync(FlagGlobale, Ambiente);
        dopo.Should().NotBeNull("senza l'invalidazione la cache continua a rispondere «non esiste»");
        dopo!.Value.Should().Be(abilita ? "true" : "false");
        s.Comandi.OfType<CreateConfigurationCommand>().Should().ContainSingle();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Globale_ChiaveConRigaGiaInCache_LaLetturaSuccessivaVedeIlValoreNuovo(bool abilita)
    {
        var s = CreaScenario();
        var valoreVecchio = abilita ? "false" : "true";
        s.Config.ScriviDirettamente(FlagGlobale, Ambiente, valoreVecchio);
        (await s.Config.GetConfigurationByKeyAsync(FlagGlobale, Ambiente))!.Value.Should().Be(
            valoreVecchio, "setup: la riga e' in cache col valore vecchio");

        await ImpostaGlobaleAsync(s.Service, abilita);

        var dopo = await s.Config.GetConfigurationByKeyAsync(FlagGlobale, Ambiente);
        dopo!.Value.Should().Be(abilita ? "true" : "false",
            "senza l'invalidazione la cache restituisce il valore di prima, e chi legge il flag non vede la modifica");
        s.Comandi.OfType<UpdateConfigValueCommand>().Should().ContainSingle();
        s.Comandi.OfType<CreateConfigurationCommand>().Should().BeEmpty();
    }

    // ----------------------------------------
    // La creazione perde la gara con un'altra richiesta (23505)
    // ----------------------------------------

    private static DbUpdateException ViolazioneDiUnicita() =>
        new("An error occurred while saving the entity changes.",
            new PostgresException("duplicate key value violates unique constraint", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Globale_LaCreazionePerdeLaGara_RileggeEPortaLaRigaDellAltraRichiestaAlValoreVoluto(bool abilita)
    {
        // L'altra richiesta ha creato la riga col valore opposto a quello voluto da questa.
        var s = CreaScenario(creazione: (nuova, config) =>
        {
            config.ScriviDirettamente(nuova.Key, nuova.Environment, abilita ? "false" : "true");
            throw ViolazioneDiUnicita();
        });

        await ImpostaGlobaleAsync(s.Service, abilita);

        var aggiornamento = s.Comandi.OfType<UpdateConfigValueCommand>().Should().ContainSingle(
            "la richiesta che perde aggiorna la riga dell'altra, non ne crea una seconda").Subject;
        aggiornamento.ConfigId.ToString().Should().Be(s.Config.IdDellaRiga(FlagGlobale, Ambiente));
        aggiornamento.NewValue.Should().Be(abilita ? "true" : "false");

        // La ricerca prima della creazione aveva memorizzato «non esiste»: la rilettura ha trovato la riga
        // solo perche' la cache e' stata invalidata, e la lettura di adesso vede il valore aggiornato.
        var riga = await s.Config.GetConfigurationByKeyAsync(FlagGlobale, Ambiente);
        riga!.Value.Should().Be(abilita ? "true" : "false");
    }

    [Fact]
    public async Task PerTier_LaCreazionePerdeLaGara_RileggeEPortaLaRigaDellAltraRichiestaAlValoreVoluto()
    {
        var s = CreaScenario(creazione: (nuova, config) =>
        {
            config.ScriviDirettamente(nuova.Key, nuova.Environment, "true");
            throw ViolazioneDiUnicita();
        });

        await s.Service.DisableFeatureForTierAsync("prova-flag", UserTier.Premium, Guid.NewGuid().ToString());

        s.Comandi.OfType<UpdateConfigValueCommand>().Should().ContainSingle();
        (await s.Config.GetConfigurationByKeyAsync("prova-flag.Tier.premium", Ambiente))!.Value.Should().Be("false");
    }

    [Fact]
    public async Task Globale_ViolazioneDiUnicitaSenzaRigaDaAggiornare_RilanciaLErroreOriginale()
    {
        // Es. la riga in conflitto e' inattiva: la ricerca vede solo le attive, quindi non c'e' niente da aggiornare.
        var errore = ViolazioneDiUnicita();
        var s = CreaScenario(creazione: (_, _) => throw errore);

        var act = async () => await s.Service.EnableFeatureAsync(FlagGlobale, userId: Guid.NewGuid().ToString());

        (await act.Should().ThrowAsync<DbUpdateException>()).Which.Should().BeSameAs(errore);
        s.Comandi.OfType<UpdateConfigValueCommand>().Should().BeEmpty();
    }

    [Theory]
    [InlineData(PostgresErrorCodes.SerializationFailure)]
    [InlineData(PostgresErrorCodes.ForeignKeyViolation)] // l'autore della riga non esiste
    public async Task Globale_UnErroreDiDatabaseCheNonEUnaViolazioneDiUnicita_NonVieneRecuperato(string sqlState)
    {
        var errore = new DbUpdateException("fail", new PostgresException("fail", "ERROR", "ERROR", sqlState));
        var s = CreaScenario(creazione: (nuova, config) =>
        {
            // Anche con una riga presente: il recupero non deve scattare per un errore che non e' il 23505.
            config.ScriviDirettamente(nuova.Key, nuova.Environment, "false");
            throw errore;
        });

        var act = async () => await s.Service.EnableFeatureAsync(FlagGlobale, userId: Guid.NewGuid().ToString());

        (await act.Should().ThrowAsync<DbUpdateException>()).Which.Should().BeSameAs(errore);
        s.Comandi.OfType<UpdateConfigValueCommand>().Should().BeEmpty();
    }
}
