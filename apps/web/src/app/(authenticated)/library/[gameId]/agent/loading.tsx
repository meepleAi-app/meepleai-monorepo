/**
 * Loading State for Agent Page
 * Issue #3237 (FRONT-001)
 */

export default function AgentLoading() {
  return (
    <div className="flex h-screen items-center justify-center bg-slate-950">
      <div className="flex flex-col items-center gap-4">
        <div className="h-12 w-12 animate-spin rounded-full border-4 border-cyan-400 border-t-transparent" />
        <p className="text-slate-400">Loading AI Assistant...</p>
      </div>
    </div>
  );
}
