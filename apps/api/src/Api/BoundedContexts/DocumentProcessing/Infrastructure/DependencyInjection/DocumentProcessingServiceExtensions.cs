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

        // Infrastructure Adapters (scoped - may use file I/O)
        services.AddScoped<IPdfTableExtractor, ITextPdfTableExtractor>();

        return services;
    }
}
