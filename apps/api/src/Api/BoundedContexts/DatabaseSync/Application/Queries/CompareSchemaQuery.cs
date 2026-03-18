using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DatabaseSync.Application.Queries;

internal record CompareSchemaQuery() : IQuery<SchemaDiffResult>;
