// Issue #3390 (epic #3397) Slice 1: structured grounding observability for the in-session agent.
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    // === Issue #3390 (epic #3397) Slice 1 — In-session agent grounding observability ===
    //
    // Makes the grounding state of every in-session agent response measurable BEFORE the
    // structural unification (Slices 2-5) touches the hot live path. The audit
    // (docs/for-developers/audits/2026-07-29-in-session-agent-grounding-audit.md) flagged the
    // grounding signal as invisible to ops ("painted gauge"): no metric distinguished a grounded
    // (cited) answer from a silently ungrounded one, and none carried the input path.
    //
    // Two producers, one contract (mirrors the #3388 GroundingStatus wire contract):
    //   - text  -> ChatWithSessionAgentCommandHandler (KnowledgeBase, RAG, retrieval_profile=live_session)
    //   - image -> AskSessionAgentCommandHandler       (SessionTracking, multimodal, retrieval_profile=none)
    //
    // Cardinality rule (see MeepleAiMetrics.Rag.cs): tags are bounded sets ONLY — no
    // session/user/game ids. citation_count is NOT a tag (unbounded) -> separate histogram.

    /// <summary>
    /// Counter incremented once per completed in-session agent response.
    /// Tags: path (<see cref="AgentResponsePaths"/>), grounding_status
    /// (<see cref="AgentGroundingStatuses"/>), retrieval_profile
    /// (<see cref="AgentRetrievalProfiles"/>). Drives the ungrounded-rate alert
    /// (infra/prometheus/alerts/agent-grounding.yml).
    /// </summary>
    public static readonly Counter<long> AgentResponseGrounding = Meter.CreateCounter<long>(
        name: "meepleai.agent.response.grounding",
        unit: "responses",
        description: "In-session agent responses by input path, grounding status, and retrieval profile");

    /// <summary>
    /// Histogram of citation counts per in-session agent response, split by input path.
    /// The <c>le=0</c> bucket captures ungrounded (uncited) responses. Tag: path only (bounded).
    /// </summary>
    public static readonly Histogram<int> AgentResponseCitationCount = Meter.CreateHistogram<int>(
        name: "meepleai.agent.response.citation_count",
        unit: "citations",
        description: "Number of citations per in-session agent response, by input path");

    /// <summary>
    /// Bounded set of in-session agent input paths. The seam #3390 unifies:
    /// <c>text</c> already flows through retrieval (grounded-capable); <c>image</c> does not (yet).
    /// </summary>
    public static class AgentResponsePaths
    {
        public const string Text = "text";
        public const string Image = "image";
    }

    /// <summary>
    /// Bounded set of grounding-status tag values (lowercase of the #3388 wire enum).
    /// </summary>
    public static class AgentGroundingStatuses
    {
        public const string Grounded = "grounded";
        public const string Partial = "partial";
        public const string Ungrounded = "ungrounded";

        /// <summary>
        /// Maps the wire grounding string ("Grounded"/"Partial"/"Ungrounded", produced by both
        /// in-session handlers) to the bounded lowercase tag value. Unknown -> <see cref="Ungrounded"/>
        /// (fail-safe: never fabricate a grounded label from an unexpected value).
        /// </summary>
        public static string FromWire(string? wire) => wire switch
        {
            "Grounded" => Grounded,
            "Partial" => Partial,
            _ => Ungrounded,
        };
    }

    /// <summary>
    /// Bounded set of retrieval-profile tag values. <c>none</c> marks a response produced with
    /// no retrieval at all (today's image/text multimodal path). Slices 2-4 will emit
    /// <c>live_session</c> from the image path once retrieval is wired.
    /// </summary>
    public static class AgentRetrievalProfiles
    {
        public const string LiveSession = "live_session";
        public const string Default = "default";
        public const string None = "none";
    }

    /// <summary>
    /// Records one completed in-session agent response.
    /// Call once, at the point where the final grounding status and citation count are known.
    /// </summary>
    /// <param name="path">Input path (<see cref="AgentResponsePaths"/>).</param>
    /// <param name="groundingStatusWire">Wire grounding string ("Grounded"/"Partial"/"Ungrounded").</param>
    /// <param name="retrievalProfile">Retrieval profile (<see cref="AgentRetrievalProfiles"/>).</param>
    /// <param name="citationCount">Number of citations in the response (0 when ungrounded/uncited).</param>
    public static void RecordAgentResponseGrounding(
        string path,
        string groundingStatusWire,
        string retrievalProfile,
        int citationCount)
    {
        var tags = new TagList
        {
            { "path", path },
            { "grounding_status", AgentGroundingStatuses.FromWire(groundingStatusWire) },
            { "retrieval_profile", retrievalProfile }
        };
        AgentResponseGrounding.Add(1, tags);

        AgentResponseCitationCount.Record(citationCount, new TagList { { "path", path } });
    }
}
