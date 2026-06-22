using System.Reflection;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// AC8 (BE-3 #1590 C3.1) — drift-prevention guard ensuring the two orthogonal
/// event-log tables remain decoupled at the handler level.
///
/// Architecture invariant:
///   session_events    → owned exclusively by SessionTracking BC
///   domain_event_logs → owned exclusively by the durable event log pipeline
///
/// The two tables are intentionally disjoint (spec §3.5 hardened).
/// <see cref="GetSessionDiaryQueryHandler"/> reads session_events only;
/// <see cref="GetLibraryActivityQueryHandler"/> reads domain_event_logs only.
///
/// NOTE on boundary style: both handlers share <c>MeepleAiDbContext</c>, so the
/// boundary is by-convention (each handler accesses only its own DbSet).
/// This test enforces that neither handler has been coupled to the OTHER table
/// via a dedicated repository interface (<c>ISessionEventRepository</c> or a
/// hypothetical <c>IDomainEventLogRepository</c>). It also documents the intent
/// so a future refactor introducing explicit repository types does not silently
/// cross the boundary.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
public sealed class EventLogBoundaryTests
{
    // -----------------------------------------------------------------------
    // AC8 — GetSessionDiaryQueryHandler must NOT cross into domain_event_logs
    // -----------------------------------------------------------------------

    /// <summary>
    /// The session-diary handler reads only <c>session_events</c>. It must never
    /// be given a repository or service whose name contains "DomainEventLog".
    /// </summary>
    [Fact]
    public void GetSessionDiaryQueryHandler_does_NOT_depend_on_domain_event_log_infrastructure()
    {
        var paramTypeNames = CtorParamTypeNames(typeof(GetSessionDiaryQueryHandler));

        paramTypeNames.Should().NotContain(
            n => n.Contains("DomainEventLog", StringComparison.Ordinal),
            "session diary reads session_events only — introducing a DomainEventLog dependency would cross the orthogonal table boundary (BE-3 #1590 AC8 / C3.1)");
    }

    // -----------------------------------------------------------------------
    // AC8 — GetLibraryActivityQueryHandler must NOT cross into session_events
    // -----------------------------------------------------------------------

    /// <summary>
    /// The library-activity handler reads only <c>domain_event_logs</c> and
    /// <c>user_library_entries</c>. It must never be given a repository or
    /// service whose name contains "SessionEvent".
    /// </summary>
    [Fact]
    public void GetLibraryActivityQueryHandler_does_NOT_depend_on_session_events_infrastructure()
    {
        var paramTypeNames = CtorParamTypeNames(typeof(GetLibraryActivityQueryHandler));

        paramTypeNames.Should().NotContain(
            n => n.Contains("SessionEvent", StringComparison.Ordinal),
            "library activity reads domain_event_logs only — introducing a SessionEvent dependency would cross the orthogonal table boundary (BE-3 #1590 AC8 / C3.1)");
    }

    // -----------------------------------------------------------------------
    // Helper
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the simple type name of every parameter in the first public
    /// instance constructor of <paramref name="handlerType"/>. Uses reflection
    /// so the assertions survive parameter reordering without maintenance.
    /// </summary>
    private static string[] CtorParamTypeNames(Type handlerType)
    {
        var ctor = handlerType
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .First();

        return ctor
            .GetParameters()
            .Select(p => p.ParameterType.Name)
            .ToArray();
    }
}
