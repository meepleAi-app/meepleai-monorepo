using System;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

internal sealed class ResolvePdfCopyrightTierQueryHandler
    : IQueryHandler<ResolvePdfCopyrightTierQuery, CopyrightTier>
{
    private readonly ICopyrightTierResolver _resolver;

    public ResolvePdfCopyrightTierQueryHandler(ICopyrightTierResolver resolver)
        => _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    public Task<CopyrightTier> Handle(ResolvePdfCopyrightTierQuery query, CancellationToken cancellationToken)
        => _resolver.ResolveTierAsync(query.DocumentId, query.UserId, cancellationToken);
}
