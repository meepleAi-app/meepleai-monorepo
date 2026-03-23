'use client';

export function DashboardClient() {
  return (
    <div className="flex flex-col lg:flex-row gap-6 w-full max-w-7xl mx-auto px-4 py-6">
      {/* Tavolo — main column */}
      <main className="flex-1 min-w-0 flex flex-col gap-6">
        <div data-testid="hero-zone-placeholder">Hero</div>
        <div data-testid="quick-stats-placeholder">Stats</div>
        <div data-testid="active-sessions-placeholder">Sessions</div>
        <div data-testid="recent-games-placeholder">Games</div>
        <div data-testid="your-agents-placeholder">Agents</div>
      </main>

      {/* Sidebar */}
      <aside className="flex flex-col gap-6 w-full lg:w-[280px] lg:flex-shrink-0">
        <div data-testid="sidebar-agents-placeholder">Sidebar Agents</div>
        <div data-testid="sidebar-chats-placeholder">Sidebar Chats</div>
        <div data-testid="sidebar-activity-placeholder">Sidebar Activity</div>
      </aside>
    </div>
  );
}
