using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using AgentDefinitionEntity = Api.BoundedContexts.KnowledgeBase.Domain.Entities.AgentDefinition;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for SeedAgentDefinitionsCommand.
/// Populates a default AgentDefinition for the Agent Playground POC.
/// Idempotent: Only executes if no agent definitions exist.
/// </summary>
internal sealed class SeedAgentDefinitionsCommandHandler : ICommandHandler<SeedAgentDefinitionsCommand>
{
    private readonly IAgentDefinitionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SeedAgentDefinitionsCommandHandler> _logger;

    public SeedAgentDefinitionsCommandHandler(
        IAgentDefinitionRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<SeedAgentDefinitionsCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SeedAgentDefinitionsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var all = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var existingNames = new HashSet<string>(all.Select(a => a.Name), StringComparer.Ordinal);
        int seeded = 0;

        // Agent 1: OpenRouter (cloud) - free Llama 3.3
        if (!existingNames.Contains("MeepleAI Board Game Assistant"))
        {
            var agent = AgentDefinitionEntity.Create(
                name: "MeepleAI Board Game Assistant",
                description: "A helpful AI assistant specialized in board games. Explains rules, suggests strategies, and answers questions about any board game.",
                type: AgentType.RagAgent,
                config: AgentDefinitionConfig.Create(
                    model: "meta-llama/llama-3.3-70b-instruct:free",
                    maxTokens: 4096,
                    temperature: 0.7f),
                strategy: AgentStrategy.SingleModel(),
                prompts: new List<AgentPromptTemplate>
                {
                    AgentPromptTemplate.Create(
                        role: "system",
                        content: "You are MeepleAI, a friendly and knowledgeable board game assistant. You help users understand rules, suggest strategies, recommend games, and answer any board game-related questions. Be concise, accurate, and enthusiastic about board games. When citing rules, reference the specific rulebook section when possible.")
                });

            await _repository.AddAsync(agent, cancellationToken).ConfigureAwait(false);
            seeded++;
        }

        // Agent 2: Ollama (local) - for provider switching demo
        if (!existingNames.Contains("MeepleAI Local Assistant"))
        {
            var ollamaAgent = AgentDefinitionEntity.Create(
                name: "MeepleAI Local Assistant",
                description: "Local Ollama-powered board game AI assistant using Llama 3.3. Runs on local hardware for privacy and low-latency responses.",
                type: AgentType.RagAgent,
                config: AgentDefinitionConfig.Create(
                    model: "llama3.3:70b",
                    maxTokens: 4096,
                    temperature: 0.7f),
                strategy: AgentStrategy.SingleModel(),
                prompts: new List<AgentPromptTemplate>
                {
                    AgentPromptTemplate.Create(
                        role: "system",
                        content: "You are MeepleAI, a friendly and knowledgeable board game assistant. You help users understand rules, suggest strategies, recommend games, and answer any board game-related questions. Be concise, accurate, and enthusiastic about board games. When citing rules, reference the specific rulebook section when possible.")
                });

            await _repository.AddAsync(ollamaAgent, cancellationToken).ConfigureAwait(false);
            seeded++;
        }

        if (seeded > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Agent definitions seeded successfully: {Count} agent(s) added", seeded);
        }
        else
        {
            _logger.LogInformation("Agent definitions already seeded ({Count} found). Skipping seed.", all.Count);
        }
    }
}
