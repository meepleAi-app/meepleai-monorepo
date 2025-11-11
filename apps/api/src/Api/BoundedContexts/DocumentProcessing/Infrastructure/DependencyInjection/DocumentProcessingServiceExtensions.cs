using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.DependencyInjection;

public static class DocumentProcessingServiceExtensions
{
    public static IServiceCollection AddDocumentProcessingContext(this IServiceCollection services)
    {
        // Repository implementations pending
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        return services;
    }
}
