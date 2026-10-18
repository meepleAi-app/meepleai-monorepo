using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Services;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Handler for <see cref="GetUploadLimitsQuery"/>. The plan's PDF size is a commercial cap on top of
/// the technical limit, so the effective limit is the smaller of the two (#4449): admins and editors
/// get the technical limit, a free plan below it gets its own.
/// </summary>
internal sealed class GetUploadLimitsQueryHandler : IQueryHandler<GetUploadLimitsQuery, UploadLimitsDto>
{
    private readonly ITierEnforcementService _tierEnforcementService;
    private readonly long _maxFileSizeBytes;

    public GetUploadLimitsQueryHandler(
        ITierEnforcementService tierEnforcementService,
        IOptions<PdfProcessingOptions> pdfOptions)
    {
        _tierEnforcementService = tierEnforcementService ?? throw new ArgumentNullException(nameof(tierEnforcementService));
        ArgumentNullException.ThrowIfNull(pdfOptions);
        _maxFileSizeBytes = pdfOptions.Value.MaxFileSizeBytes;
    }

    public async Task<UploadLimitsDto> Handle(GetUploadLimitsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tierLimits = await _tierEnforcementService
            .GetLimitsAsync(request.UserId, cancellationToken)
            .ConfigureAwait(false);

        return new UploadLimitsDto(
            MaxFileSizeBytes: Math.Min(_maxFileSizeBytes, tierLimits.MaxPdfSizeBytes),
            MaxConcurrentChunkedUploads: InitChunkedUploadCommandHandler.MaxActiveSessionsPerUser);
    }
}
