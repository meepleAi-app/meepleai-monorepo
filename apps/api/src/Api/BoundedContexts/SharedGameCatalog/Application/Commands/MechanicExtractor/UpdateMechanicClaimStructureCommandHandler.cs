using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Handler for <see cref="UpdateMechanicClaimStructureCommand"/>.
/// </summary>
/// <remarks>
/// 404: missing analysis or claim. 400: domain <see cref="ArgumentException"/> (override graph invariants).
/// 409: domain <see cref="InvalidOperationException"/> (e.g. rejected claim) or optimistic concurrency.
/// </remarks>
internal sealed class UpdateMechanicClaimStructureCommandHandler
    : ICommandHandler<UpdateMechanicClaimStructureCommand, MechanicClaimDto>
{
    private readonly IMechanicAnalysisRepository _analysisRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateMechanicClaimStructureCommandHandler> _logger;

    public UpdateMechanicClaimStructureCommandHandler(
        IMechanicAnalysisRepository analysisRepository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateMechanicClaimStructureCommandHandler> logger)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MechanicClaimDto> Handle(
        UpdateMechanicClaimStructureCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var analysis = await _analysisRepository
            .GetByIdWithClaimsIgnoringFiltersAsync(request.AnalysisId, cancellationToken)
            .ConfigureAwait(false);

        if (analysis is null)
        {
            throw new NotFoundException(
                resourceType: "MechanicAnalysis",
                resourceId: request.AnalysisId.ToString());
        }

        if (analysis.Claims.All(c => c.Id != request.ClaimId))
        {
            throw new NotFoundException(
                resourceType: "MechanicClaim",
                resourceId: request.ClaimId.ToString());
        }

        try
        {
            analysis.SetClaimStructure(request.ClaimId, request.Structure.ToDomain());
        }
        catch (ArgumentException ex)
        {
            throw new BadRequestException(ex.Message, ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message, ex);
        }

        _analysisRepository.Update(analysis);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(
                ex,
                "Optimistic concurrency failure updating structure of MechanicClaim {ClaimId} on MechanicAnalysis {AnalysisId}.",
                request.ClaimId,
                request.AnalysisId);

            throw new ConflictException(
                "Mechanic analysis was modified by another operation. Please retry.",
                ex);
        }

        var claim = analysis.Claims.First(c => c.Id == request.ClaimId);

        _logger.LogInformation(
            "MechanicClaim {ClaimId} on MechanicAnalysis {AnalysisId} structure updated by admin {ReviewerId}.",
            claim.Id,
            analysis.Id,
            request.ReviewerId);

        return MechanicClaimDtoMapper.FromDomain(claim, analysis.Id);
    }
}
