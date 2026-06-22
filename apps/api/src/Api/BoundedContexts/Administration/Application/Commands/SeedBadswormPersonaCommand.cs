using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to seed the badsworm@gmail.com persona dogfood data:
/// 10 library entries (incl. Nanolith libro game) with mixed KB processing
/// states for UI dashboard validation. Idempotent. Runs after the Catalog
/// seed layer (Dev + Staging profiles only) so the SharedGames it references
/// are guaranteed to exist.
///
/// Reference: docs/superpowers/specs/2026-05-07-libro-game-nanolith-demo-design.md
/// (Aaron persona dogfood) + acceptance spec defined in chat 2026-05-10.
/// </summary>
public sealed record SeedBadswormPersonaCommand : ICommand;
