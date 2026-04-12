# MeepleDev DevTools (backend)

Codice attivo SOLO in `Debug` build + `Development` environment.
Escluso dal Release build via `<Compile Remove>` in `Api.csproj`.

## Come aggiungere un nuovo mock service

1. Verifica che esista `I{Service}` interface nel namespace `Api.Services` (o simile)
2. Crea `DevTools/MockImpls/Mock{Service}.cs` che implementa `I{Service}`
3. In `DevToolsServiceCollectionExtensions.cs`, aggiungi:
   ```csharp
   services.AddMockAwareService<IService, RealService, MockService>("service-name");
   ```
4. Aggiungi `MOCK_SERVICE` env var a `infra/.env.dev.local.example`
5. Aggiungi voce a `MockToggleStateProvider` known keys
6. Scrivi unit test in `tests/Api.Tests/DevTools/Mock{Service}Tests.cs`

Vedi `MockLlmService.cs` come template canonico.
