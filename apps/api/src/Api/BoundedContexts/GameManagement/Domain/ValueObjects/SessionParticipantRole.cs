namespace Api.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Defines the role of a <see cref="SessionParticipant"/> in a live game session
/// (invite/join flow in the GameManagement bounded context).
///
/// <para>Renamed from the former <c>ParticipantRole</c> (issue #3392) to eliminate the
/// name collision with <see cref="Api.BoundedContexts.SessionTracking.Domain.Enums.ParticipantRole"/>,
/// which has the SAME member names but the OPPOSITE numeric ordering
/// (SessionTracking: Spectator=0, Player=1, Host=2). The colliding name was a real vector
/// for an incorrect <c>using</c> import mixing the two contracts.</para>
///
/// <para>These values are persisted BY NAME (string), so the rename does not affect stored
/// data. The numeric ordering here is NOT used for ordinal comparisons — this enum is only
/// ever compared with equality.</para>
/// </summary>
public enum SessionParticipantRole { Host, Player, Spectator }
