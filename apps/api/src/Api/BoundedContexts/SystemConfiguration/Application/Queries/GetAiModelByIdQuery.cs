using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

public sealed record GetAiModelByIdQuery(Guid Id) : IQuery<AiModelConfigDto>;
