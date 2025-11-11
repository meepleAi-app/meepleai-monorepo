using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Query to retrieve all distinct configuration categories.
/// </summary>
public record GetConfigCategoriesQuery : IQuery<IReadOnlyList<string>>;
