using Api.BoundedContexts.Administration.Domain.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.Docker;

public record GetContainerLogsQuery(
    string ContainerId,
    int TailLines = 100) : IRequest<ContainerLogsDto>;
