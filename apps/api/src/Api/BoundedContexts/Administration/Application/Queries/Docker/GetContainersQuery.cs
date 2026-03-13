using Api.BoundedContexts.Administration.Domain.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.Docker;

public record GetContainersQuery : IRequest<IReadOnlyList<ContainerInfoDto>>;
