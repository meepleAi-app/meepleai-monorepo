using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;

/// <summary>
/// #5079: withdraws the open proposal of a private game to the catalog. With a proposal open, the game cannot lose its
/// description nor be deleted (decision of 2026-11-19); withdrawing frees it.
/// </summary>
internal record WithdrawPrivateGameProposalCommand(Guid UserId, Guid PrivateGameId) : ICommand;
