using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Batch command (#534 ME-M3.2): aggregate mechanic-card feedback and auto-suppress cards breaching
/// admin-tunable thresholds. Invoked by <c>MechanicCardAutoSuppressionJob</c> (not an HTTP endpoint).
/// </summary>
internal sealed record RunMechanicCardAutoSuppressionCommand : ICommand<AutoSuppressionResult>;

/// <summary>Outcome of one auto-suppression run.</summary>
internal sealed record AutoSuppressionResult(int Evaluated, int Suppressed);
