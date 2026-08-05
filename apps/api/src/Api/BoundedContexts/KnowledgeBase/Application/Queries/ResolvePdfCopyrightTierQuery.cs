using System;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// #3435 (§5quinquies): resolves one PDF's <see cref="CopyrightTier"/> for a user, so non-citation
/// surfaces can Full-gate consistently with grounded citations. Copyright-tier resolution is owned by
/// KnowledgeBase; other bounded contexts (e.g. the DocumentProcessing image-region viewer overlay)
/// consume it via <c>IMediator</c> rather than injecting the resolver (ADR-090 boundary).
/// </summary>
internal sealed record ResolvePdfCopyrightTierQuery(string DocumentId, Guid UserId)
    : IQuery<CopyrightTier>;
