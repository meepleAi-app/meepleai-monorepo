using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.DependencyInjection;

public static class DocumentProcessingServiceExtensions
{
    public static IServiceCollection AddDocumentProcessingContext(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Domain Layer
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Domain Services (stateless, can be singleton for performance)
        services.AddSingleton<TableToAtomicRuleConverter>();
        services.AddSingleton<PdfValidationDomainService>(); // PDF-09: Business validation rules
        services.AddScoped<PdfTextProcessingDomainService>(); // DDD-PHASE4: Text processing business rules
        services.AddScoped<PdfQualityValidationDomainService>(); // BGAI-012: Quality threshold enforcement

        // Infrastructure Adapters (scoped - may use file I/O)
        services.AddScoped<IPdfTableExtractor, ITextPdfTableExtractor>();
        services.AddScoped<IPdfValidator, DocnetPdfValidator>(); // PDF-09: DDD validation adapter

        // BGAI-010: Register ALL 3 extractors for orchestrator (3-stage pipeline)

        // Stage 1: Unstructured (primary - RAG-optimized, quality ≥0.80)
        services.AddHttpClient("UnstructuredService", client =>
            {
                var baseUrl = configuration["PdfProcessing:Extractor:UnstructuredService:BaseUrl"]
                              ?? "http://unstructured-service:8001";
                client.BaseAddress = new Uri(baseUrl);

                var timeoutSeconds = configuration.GetValue<int?>("PdfProcessing:Extractor:UnstructuredService:TimeoutSeconds") ?? 35;
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

                client.DefaultRequestHeaders.Add("User-Agent", "MeepleAI-Backend/1.0");
            })
            .AddPolicyHandler(GetRetryPolicy(configuration, "Unstructured"));

        services.AddScoped<UnstructuredPdfTextExtractor>();

        // Stage 2: SmolDocling (VLM fallback for complex layouts, quality ≥0.70)
        services.AddHttpClient("SmolDoclingService", client =>
            {
                var baseUrl = configuration["PdfExtraction:SmolDocling:BaseUrl"]
                              ?? "http://smoldocling-service:8002";
                client.BaseAddress = new Uri(baseUrl);

                var timeoutSeconds = configuration.GetValue<int?>("PdfExtraction:SmolDocling:TimeoutSeconds") ?? 60;
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

                client.DefaultRequestHeaders.Add("User-Agent", "MeepleAI-Backend/1.0");
            })
            .AddPolicyHandler(GetRetryPolicy(configuration, "SmolDocling"))
            .AddPolicyHandler(GetCircuitBreakerPolicy(configuration));

        services.AddScoped<SmolDoclingPdfTextExtractor>();

        // Stage 3: Docnet (local fallback, best effort)
        services.AddScoped<DocnetPdfTextExtractor>();

        // BGAI-010: Enhanced PDF Processing Orchestrator (3-stage pipeline coordinator)
        services.AddScoped<EnhancedPdfProcessingOrchestrator>(sp =>
        {
            var unstructured = sp.GetRequiredService<UnstructuredPdfTextExtractor>();
            var smoldocling = sp.GetRequiredService<SmolDoclingPdfTextExtractor>();
            var docnet = sp.GetRequiredService<DocnetPdfTextExtractor>();
            var logger = sp.GetRequiredService<ILogger<EnhancedPdfProcessingOrchestrator>>();
            var config = sp.GetRequiredService<IConfiguration>();

            return new EnhancedPdfProcessingOrchestrator(unstructured, smoldocling, docnet, logger, config);
        });

        // IPdfTextExtractor registration based on feature flag
        var extractorProvider = configuration["PdfProcessing:Extractor:Provider"] ?? "Orchestrator";

        if (extractorProvider.Equals("Orchestrator", StringComparison.OrdinalIgnoreCase))
        {
            // Use 3-stage orchestrator as default (recommended for production)
            services.AddScoped<OrchestratedPdfTextExtractor>();
            services.AddScoped<IPdfTextExtractor, OrchestratedPdfTextExtractor>();
        }
        else if (extractorProvider.Equals("Unstructured", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IPdfTextExtractor, UnstructuredPdfTextExtractor>();
        }
        else if (extractorProvider.Equals("SmolDocling", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IPdfTextExtractor, SmolDoclingPdfTextExtractor>();
        }
        else
        {
            services.AddScoped<IPdfTextExtractor, DocnetPdfTextExtractor>();
        }

        return services;
    }

    /// <summary>
    /// Get Polly retry policy for PDF extraction services
    /// </summary>
    /// <param name="configuration">Configuration</param>
    /// <param name="serviceName">Service name (Unstructured or SmolDocling)</param>
    /// <returns>Retry policy with exponential backoff</returns>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(
        IConfiguration configuration,
        string serviceName)
    {
        var configKey = serviceName == "Unstructured"
            ? "PdfProcessing:Extractor:UnstructuredService:MaxRetries"
            : "PdfExtraction:SmolDocling:MaxRetries";

        var maxRetries = configuration.GetValue<int?>(configKey) ?? 3;

        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
            .WaitAndRetryAsync(
                maxRetries,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    Console.WriteLine(
                        $"[{serviceName}] Retry {retryCount} after {timespan.TotalSeconds}s delay (Status: {outcome.Result?.StatusCode})");
                });
    }

    /// <summary>
    /// Get Polly circuit breaker policy for SmolDocling service
    /// </summary>
    /// <remarks>
    /// Circuit breaker protects against cascade failures when VLM service is down
    /// - Opens after 5 consecutive failures
    /// - Stays open for 60s before attempting half-open
    /// - Prevents resource exhaustion from failed GPU requests
    /// </remarks>
    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(IConfiguration configuration)
    {
        var failureThreshold = configuration.GetValue<int?>("PdfExtraction:SmolDocling:CircuitBreaker:FailureThreshold") ?? 5;
        var durationSeconds = configuration.GetValue<int?>("PdfExtraction:SmolDocling:CircuitBreaker:DurationSeconds") ?? 60;

        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: failureThreshold,
                durationOfBreak: TimeSpan.FromSeconds(durationSeconds),
                onBreak: (outcome, duration) =>
                {
                    Console.WriteLine(
                        $"[SmolDocling] Circuit breaker OPEN for {duration.TotalSeconds}s (Threshold: {failureThreshold} failures)");
                },
                onReset: () =>
                {
                    Console.WriteLine("[SmolDocling] Circuit breaker CLOSED - service recovered");
                },
                onHalfOpen: () =>
                {
                    Console.WriteLine("[SmolDocling] Circuit breaker HALF-OPEN - testing service");
                });
    }
}
