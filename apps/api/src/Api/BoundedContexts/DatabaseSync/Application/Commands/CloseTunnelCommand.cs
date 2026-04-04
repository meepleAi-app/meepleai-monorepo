using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DatabaseSync.Application.Commands;

internal record CloseTunnelCommand() : ICommand<TunnelStatusResult>;
