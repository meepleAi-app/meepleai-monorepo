using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Handler for SaveAgentConfigCommand (Issue #3212)
/// Maps simplified frontend config to complete domain AgentConfiguration with typology defaults
/// </summary>
internal sealed class SaveAgentConfigCommandHandler(
    IUserLibraryRepository libraryRepository,
    IAgentTypologyRepository typologyRepository,
    IUnitOfWork unitOfWork,
    ILogger<SaveAgentConfigCommandHandler> logger)
    : ICommandHandler<SaveAgentConfigCommand, SaveAgentConfigResponse>
{
    public async Task<SaveAgentConfigResponse> Handle(
        SaveAgentConfigCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Validate typology exists and is approved
        var typology = await typologyRepository
            .GetByIdAsync(request.TypologyId, cancellationToken)
            .ConfigureAwait(false);

        if (typology == null)
        {
            throw new NotFoundException("AgentTypology", request.TypologyId.ToString());
        }

        if (typology.Status != KnowledgeBase.Domain.ValueObjects.TypologyStatus.Approved)
        {
            throw new ConflictException($"Agent typology {typology.Name} is not approved (status: {typology.Status})");
        }

        // 2. Get or create user library entry
        var entry = await libraryRepository
            .GetByUserAndGameAsync(request.UserId, request.GameId, cancellationToken)
            .ConfigureAwait(false);

        if (entry == null)
        {
            // Create new entry if user hasn't added game to library yet
            entry = new UserLibraryEntry(Guid.NewGuid(), request.UserId, request.GameId);
            await libraryRepository.AddAsync(entry, cancellationToken).ConfigureAwait(false);
        }

        // 3. Map simplified config to complete AgentConfiguration with defaults
        // Use typology defaults or sensible fallbacks
        var agentConfig = AgentConfiguration.Create(
            llmModel: request.ModelName,
            temperature: 0.7, // Default temperature (typology could provide this in future)
            maxTokens: 2000,  // Default max tokens (typology could provide this in future)
            personality: typology.Name, // Use typology name as personality indicator
            detailLevel: "Normale", // Default detail level
            personalNotes: $"Typology: {typology.Name}, Cost: ${request.CostEstimate:F4}/query"
        );

        // 4. Configure agent via domain method
        entry.ConfigureAgent(agentConfig);

        // 5. Save via UnitOfWork
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Agent config saved for user {UserId}, game {GameId}, typology {TypologyId}, model {Model}",
            request.UserId,
            request.GameId,
            request.TypologyId,
            request.ModelName
        );

        // 6. Return response
        return new SaveAgentConfigResponse(
            Success: true,
            ConfigId: entry.Id, // Use library entry ID as config identifier
            Message: "Agent configuration saved successfully"
        );
    }
}
