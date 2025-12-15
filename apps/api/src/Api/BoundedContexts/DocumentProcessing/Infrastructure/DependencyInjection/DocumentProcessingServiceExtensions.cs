using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.DependencyInjection;

internal static class DocumentProcessingServiceExtensions
{
    /// <summary>
    /// Keyed service keys for PDF text extractors (ISSUE-1174: Post-merge enhancement)
    /// </summary>
    internal static class PdfExtractorKeys
    {
        public const string Unstructured = "unstructured";
        public const string SmolDocling = "smoldocling";
        public const string Docnet = "docnet";
    }

    public static IServiceCollection AddDocumentProcessingContext(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // BGAI-086: Register and validate PDF processing configuration on startup
        services.AddOptions<PdfProcessingOptions>()
            .Bind(configuration.GetSection("PdfProcessing"))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<PdfProcessingOptions>, PdfProcessingConfigurationValidator>();

        // Domain Layer
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();
        services.AddScoped<IChunkedUploadSessionRepository, ChunkedUploadSessionRepository>();
        services.AddScoped<IDocumentCollectionRepository, DocumentCollectionRepository>(); // ISSUE-2051: Document collections
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Domain Services (stateless, can be singleton for performance)
        services.AddSingleton<TableToAtomicRuleConverter>();
        services.AddSingleton<PdfValidationDomainService>(); // PDF-09: Business validation rules
        services.AddScoped<PdfTextProcessingDomainService>(); // DDD-PHASE4: Text processing business rules
        services.AddScoped<PdfQualityValidationDomainService>(); // BGAI-012: Quality threshold enforcement
        services.AddScoped<IPdfUploadQuotaService, PdfUploadQuotaService>(); // User tier-based upload quotas
        services.AddScoped<CitationPriorityService>(); // ISSUE-2051: Citation priority and deduplication

        // Infrastructure Adapters (scoped - may use file I/O)
        services.AddScoped<IPdfTableExtractor, ITextPdfTableExtractor>();
        services.AddScoped<IPdfValidator, DocnetPdfValidator>(); // PDF-09: DDD validation adapter

        // BGAI-086/087: Configure PDF text extractor based on provider setting
        var extractorProvider = configuration["PdfProcessing:Extractor:Provider"] ?? "Orchestrator";

        if (extractorProvider.Equals("Orchestrator", StringComparison.OrdinalIgnoreCase))
        {
            // BGAI-087 + ISSUE-1174: Register all extractors for orchestrator using keyed services
            // This prevents circular dependency: OrchestratedPdfTextExtractor → EnhancedPdfProcessingOrchestrator → IPdfTextExtractor[]
            RegisterUnstructuredExtractor(services, configuration);
            RegisterSmolDoclingExtractor(services, configuration);
            services.AddScoped<DocnetPdfTextExtractor>();

            // ISSUE-1174: Register stage extractors as keyed services (avoids circular DI dependency)
            // The orchestrator constructor uses [FromKeyedServices] to resolve specific extractors
            services.AddKeyedScoped<IPdfTextExtractor, UnstructuredPdfTextExtractor>(PdfExtractorKeys.Unstructured);
            services.AddKeyedScoped<IPdfTextExtractor, SmolDoclingPdfTextExtractor>(PdfExtractorKeys.SmolDocling);
            services.AddKeyedScoped<IPdfTextExtractor, DocnetPdfTextExtractor>(PdfExtractorKeys.Docnet);

            // Register orchestrator application service
            services.AddScoped<EnhancedPdfProcessingOrchestrator>();

            // Register orchestrator adapter as primary extractor interface
            services.AddScoped<IPdfTextExtractor, OrchestratedPdfTextExtractor>();
        }
        else if (extractorProvider.Equals("Unstructured", StringComparison.OrdinalIgnoreCase))
        {
            RegisterUnstructuredExtractor(services, configuration);
            services.AddScoped<IPdfTextExtractor, UnstructuredPdfTextExtractor>();
        }
        else if (extractorProvider.Equals("SmolDocling", StringComparison.OrdinalIgnoreCase))
        {
            RegisterSmolDoclingExtractor(services, configuration);
            services.AddScoped<IPdfTextExtractor, SmolDoclingPdfTextExtractor>();
        }
        else
        {
            // Fallback: Docnet extractor
            services.AddScoped<IPdfTextExtractor, DocnetPdfTextExtractor>();
        }

        return services;
    }

    /// <summary>
    /// BGAI-086: Register Unstructured extractor with updated config paths
    /// </summary>
    private static void RegisterUnstructuredExtractor(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient("UnstructuredService", client =>
            {
                var apiUrl = configuration["PdfProcessing:Extractor:Unstructured:ApiUrl"]
                             ?? "http://unstructured-service:8001";
                client.BaseAddress = new Uri(apiUrl);

                var timeoutSeconds = configuration.GetValue<int?>("PdfProcessing:Extractor:Unstructured:TimeoutSeconds") ?? 35;
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

                client.DefaultRequestHeaders.Add("User-Agent", "MeepleAI-Backend/1.0");
            })
            .AddPolicyHandler(GetRetryPolicy(
                configuration.GetValue<int?>("PdfProcessing:Extractor:Unstructured:MaxRetries") ?? 3));

        services.AddScoped<UnstructuredPdfTextExtractor>();
    }

    /// <summary>
    /// BGAI-087: Register SmolDocling extractor with new config
    /// </summary>
    private static void RegisterSmolDoclingExtractor(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient("SmolDoclingService", client =>
            {
                var apiUrl = configuration["PdfProcessing:Extractor:SmolDocling:ApiUrl"]
                             ?? "http://smoldocling-service:8002";
                client.BaseAddress = new Uri(apiUrl);

                var timeoutSeconds = configuration.GetValue<int?>("PdfProcessing:Extractor:SmolDocling:TimeoutSeconds") ?? 30;
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

                client.DefaultRequestHeaders.Add("User-Agent", "MeepleAI-Backend/1.0");
            })
            .AddPolicyHandler(GetRetryPolicy(
                configuration.GetValue<int?>("PdfProcessing:Extractor:SmolDocling:MaxRetries") ?? 3));

        services.AddScoped<SmolDoclingPdfTextExtractor>();
    }

    /// <summary>
    /// Get Polly retry policy with configurable max retries
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(int maxRetries)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
            .WaitAndRetryAsync(
                maxRetries,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }
}
