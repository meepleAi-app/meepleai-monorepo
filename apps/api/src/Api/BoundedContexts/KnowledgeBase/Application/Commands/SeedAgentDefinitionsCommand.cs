using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to seed a default AgentDefinition for the Playground POC.
/// Populates 1 RAG agent definition so the Agent Playground can function out of the box.
/// </summary>
public sealed record SeedAgentDefinitionsCommand : ICommand;
