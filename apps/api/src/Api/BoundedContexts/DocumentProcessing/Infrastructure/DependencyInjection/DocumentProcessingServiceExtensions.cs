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

        // BGAI-001-v2: Configure PDF text extractor based on provider setting
        var extractorProvider = configuration["PdfProcessing:Extractor:Provider"] ?? "Docnet";

        if (extractorProvider.Equals("Unstructured", StringComparison.OrdinalIgnoreCase))
        {
            // Register Unstructured HTTP client with Polly retry policy
            services.AddHttpClient("UnstructuredService", client =>
                {
                    var baseUrl = configuration["PdfProcessing:Extractor:UnstructuredService:BaseUrl"]
                                  ?? "http://unstructured-service:8001";
                    client.BaseAddress = new Uri(baseUrl);

                    var timeoutSeconds = configuration.GetValue<int?>("PdfProcessing:Extractor:UnstructuredService:TimeoutSeconds") ?? 35;
                    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

                    client.DefaultRequestHeaders.Add("User-Agent", "MeepleAI-Backend/1.0");
                })
                .AddPolicyHandler(GetRetryPolicy(configuration));

            services.AddScoped<IPdfTextExtractor, UnstructuredPdfTextExtractor>();
        }
        else
        {
            // Default: Docnet extractor (existing implementation)
            services.AddScoped<IPdfTextExtractor, DocnetPdfTextExtractor>();
        }

        return services;
    }

    /// <summary>
    /// Get Polly retry policy for Unstructured service HTTP calls
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(IConfiguration configuration)
    {
        var maxRetries = configuration.GetValue<int?>("PdfProcessing:Extractor:UnstructuredService:MaxRetries") ?? 3;

        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
            .WaitAndRetryAsync(
                maxRetries,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }
}
