using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.DependencyInjection;

public static class DocumentProcessingServiceExtensions
{
    public static IServiceCollection AddDocumentProcessingContext(this IServiceCollection services)
    {
        // Domain Layer
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Domain Services (stateless, can be singleton for performance)
        services.AddSingleton<TableToAtomicRuleConverter>();
        services.AddSingleton<PdfValidationDomainService>(); // PDF-09: Business validation rules
        services.AddScoped<PdfTextProcessingDomainService>(); // DDD-PHASE4: Text processing business rules

        // Infrastructure Adapters (scoped - may use file I/O)
        services.AddScoped<IPdfTableExtractor, ITextPdfTableExtractor>();
        services.AddScoped<IPdfValidator, DocnetPdfValidator>(); // PDF-09: DDD validation adapter
        services.AddScoped<IPdfTextExtractor, DocnetPdfTextExtractor>(); // DDD-PHASE4: Text extraction adapter

        return services;
    }
}
