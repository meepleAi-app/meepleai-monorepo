// UI-04: Main Timeline component (multi-pane layout)
import { useState } from "react";
import { TimelineEvent, TimelineFilters, DEFAULT_FILTERS } from "@/lib/timeline-types";
import { TimelineFilters as TimelineFiltersComponent } from "./TimelineFilters";
import { TimelineEventList } from "./TimelineEventList";
import { TimelineDetails } from "./TimelineDetails";

interface TimelineProps {
  events: TimelineEvent[];
  isVisible: boolean;
  onToggleVisibility: () => void;
}

export function Timeline({ events, isVisible, onToggleVisibility }: TimelineProps) {
  const [filters, setFilters] = useState<TimelineFilters>(DEFAULT_FILTERS);
  const [selectedEventId, setSelectedEventId] = useState<string | null>(null);
  const [isFiltersCollapsed, setIsFiltersCollapsed] = useState(false);
  const [isDetailsCollapsed, setIsDetailsCollapsed] = useState(false);

  const selectedEvent = events.find((e) => e.id === selectedEventId) || null;

  if (!isVisible) {
    return (
      <button
        onClick={onToggleVisibility}
        aria-label="Show RAG Timeline"
        style={{
          position: "fixed",
          bottom: 24,
          right: 24,
          padding: "12px 20px",
          background: "#1a73e8",
          color: "white",
          border: "none",
          borderRadius: 24,
          fontSize: 14,
          fontWeight: 600,
          cursor: "pointer",
          boxShadow: "0 2px 8px rgba(0, 0, 0, 0.15)",
          display: "flex",
          alignItems: "center",
          gap: 8,
          zIndex: 1000
        }}
        title="Mostra Timeline RAG"
      >
        <span style={{ fontSize: 18 }}>📊</span>
        <span>Timeline RAG</span>
        {events.length > 0 && (
          <span
            style={{
              padding: "2px 8px",
              background: "white",
              color: "#1a73e8",
              borderRadius: 12,
              fontSize: 12,
              fontWeight: 700
            }}
          >
            {events.length}
          </span>
        )}
      </button>
    );
  }

  return (
    <aside
      role="region"
      aria-label="RAG Timeline with conversation events and metrics"
      style={{
        position: "fixed",
        top: 0,
        left: 0,
        right: 0,
        bottom: 0,
        background: "white",
        zIndex: 1000,
        display: "flex",
        flexDirection: "column",
        fontFamily: "sans-serif"
      }}
    >
      {/* Timeline Header */}
      <header
        style={{
          padding: 16,
          borderBottom: "1px solid #dadce0",
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          background: "white",
          zIndex: 10
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
          <span aria-hidden="true" style={{ fontSize: 28 }}>📊</span>
          <div>
            <h1 style={{ margin: 0, fontSize: 20, fontWeight: 600 }}>Timeline RAG</h1>
            <p style={{ margin: "4px 0 0 0", fontSize: 13, color: "#94a3b8" }}>
              Cronologia eventi e metriche conversazione
            </p>
          </div>
        </div>
        <button
          onClick={onToggleVisibility}
          aria-label="Close RAG Timeline"
          style={{
            padding: "10px 18px",
            background: "#f1f3f4",
            color: "#94a3b8",
            border: "none",
            borderRadius: 4,
            fontSize: 14,
            fontWeight: 500,
            cursor: "pointer",
            display: "flex",
            alignItems: "center",
            gap: 8
          }}
        >
          <span>Chiudi Timeline</span>
          <span aria-hidden="true" style={{ fontSize: 16 }}>✕</span>
        </button>
      </header>

      {/* Multi-pane Layout */}
      <div style={{ flex: 1, display: "flex", overflow: "hidden" }}>
        {/* Left: Filters Sidebar */}
        <TimelineFiltersComponent
          filters={filters}
          onFiltersChange={setFilters}
          isCollapsed={isFiltersCollapsed}
          onToggleCollapse={() => setIsFiltersCollapsed(!isFiltersCollapsed)}
        />

        {/* Center: Event List */}
        <div
          style={{
            flex: 1,
            overflowY: "auto",
            background: "#ffffff",
            borderRight: "1px solid #dadce0"
          }}
        >
          <TimelineEventList
            events={events}
            filters={filters}
            selectedEventId={selectedEventId}
            onSelectEvent={setSelectedEventId}
          />
        </div>

        {/* Right: Event Details */}
        <TimelineDetails
          event={selectedEvent}
          isCollapsed={isDetailsCollapsed}
          onToggleCollapse={() => setIsDetailsCollapsed(!isDetailsCollapsed)}
        />
      </div>

      {/* Stats Bar (optional) */}
      {events.length > 0 && (
        <footer
          role="status"
          aria-label="Timeline statistics"
          style={{
            padding: "8px 16px",
            borderTop: "1px solid #dadce0",
            background: "#f8f9fa",
            display: "flex",
            gap: 24,
            fontSize: 12,
            color: "#94a3b8"
          }}
        >
          <div>
            <strong style={{ color: "#202124" }}>{events.length}</strong> eventi totali
          </div>
          <div>
            <strong style={{ color: "#202124" }}>
              {events.filter((e) => e.status === "success").length}
            </strong>{" "}
            completati
          </div>
          <div>
            <strong style={{ color: "#d93025" }}>
              {events.filter((e) => e.status === "error").length}
            </strong>{" "}
            {events.filter((e) => e.status === "error").length === 1 ? "errore" : "errori"}
          </div>
          {events.some((e) => e.data.metrics?.totalTokens) && (
            <div>
              <strong style={{ color: "#202124" }}>
                {events
                  .reduce((sum, e) => sum + (e.data.metrics?.totalTokens || 0), 0)
                  .toLocaleString()}
              </strong>{" "}
              token totali
            </div>
          )}
        </footer>
      )}
    </aside>
  );
}
