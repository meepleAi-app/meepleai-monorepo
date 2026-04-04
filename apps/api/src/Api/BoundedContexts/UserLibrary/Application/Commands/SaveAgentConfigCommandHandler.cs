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
/// Maps simplified frontend config to complete domain AgentConfiguration
/// </summary>
internal sealed class SaveAgentConfigCommandHandler(
    IUserLibraryRepository libraryRepository,
    IAgentDefinitionRepository definitionRepository,
    IUnitOfWork unitOfWork,
    ILogger<SaveAgentConfigCommandHandler> logger)
    : ICommandHandler<SaveAgentConfigCommand, SaveAgentConfigResponse>
{
    public async Task<SaveAgentConfigResponse> Handle(
        SaveAgentConfigCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Validate definition exists and is active
        var definition = await definitionRepository
            .GetByIdAsync(request.AgentDefinitionId, cancellationToken)
            .ConfigureAwait(false);

        if (definition == null)
        {
            throw new NotFoundException("AgentDefinition", request.AgentDefinitionId.ToString());
        }

        if (!definition.IsActive)
        {
            throw new ConflictException($"Agent definition {definition.Name} is not active");
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

        // 3. Map simplified config to complete AgentConfiguration
        var agentConfig = AgentConfiguration.Create(
            llmModel: request.ModelName,
            temperature: 0.7,
            maxTokens: 2000,
            personality: definition.Name,
            detailLevel: "Normale",
            personalNotes: $"Definition: {definition.Name}, Cost: ${request.CostEstimate:F4}/query"
        );

        // 4. Configure agent via domain method
        entry.ConfigureAgent(agentConfig);

        // 5. Save via UnitOfWork
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Agent config saved for user {UserId}, game {GameId}, definition {AgentDefinitionId}, model {Model}",
            request.UserId,
            request.GameId,
            request.AgentDefinitionId,
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
