using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;

/// <summary>
/// Handler for <see cref="GetMechanicPromptQuery"/> (#539 follow-up). Assembles the system prompt
/// and every per-section prompt from <see cref="IMechanicPromptProvider"/> for admin inspection.
/// </summary>
internal sealed class GetMechanicPromptQueryHandler
    : IQueryHandler<GetMechanicPromptQuery, MechanicPromptDto>
{
    private readonly IMechanicPromptProvider _promptProvider;

    public GetMechanicPromptQueryHandler(IMechanicPromptProvider promptProvider)
    {
        _promptProvider = promptProvider ?? throw new ArgumentNullException(nameof(promptProvider));
    }

    public Task<MechanicPromptDto> Handle(GetMechanicPromptQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sections = Enum.GetValues<MechanicSection>()
            .OrderBy(s => (int)s)
            .Select(s => new MechanicPromptSectionDto(
                Section: (int)s,
                SectionName: s.ToString(),
                Prompt: _promptProvider.GetSectionPrompt(s)))
            .ToList();

        var dto = new MechanicPromptDto(
            PromptVersion: _promptProvider.PromptVersion,
            SystemPrompt: _promptProvider.GetSystemPrompt(),
            Sections: sections);

        return Task.FromResult(dto);
    }
}
