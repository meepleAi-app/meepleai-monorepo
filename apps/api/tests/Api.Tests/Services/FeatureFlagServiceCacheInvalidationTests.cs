using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
[Trait("Issue", "3844")]
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
            config, mediator.Object, ambiente.Object, NullLogger<FeatureFlagService>.Instance);

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
            config, mediator.Object, ambiente.Object, NullLogger<FeatureFlagService>.Instance);

        var utente = Guid.NewGuid().ToString();
        for (var giro = 0; giro < 3; giro++)
        {
            await service.EnableFeatureForTierAsync("prova-flag", UserTier.Premium, utente);
            await service.DisableFeatureForTierAsync("prova-flag", UserTier.Premium, utente);
        }

        creazioni.Should().Be(1, "solo il primissimo passaggio crea la configurazione");
    }
}
