using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.AgentProfileAdmin;

/// <summary>Il profilo dell'agente per la pagina admin «Agente» (ADR-095 D8, #4169 6c).</summary>
internal sealed record GetAgentProfileAdminQuery : IQuery<AgentProfileAdminDto>;
