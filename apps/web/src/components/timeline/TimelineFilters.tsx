// UI-04: Timeline filters sidebar component
import {
  TimelineFilters,
  TimelineEventType,
  TimelineEventStatus,
  getEventTypeLabel,
  getEventTypeColor
} from "@/lib/timeline-types";

interface TimelineFiltersProps {
  filters: TimelineFilters;
  onFiltersChange: (filters: TimelineFilters) => void;
  isCollapsed: boolean;
  onToggleCollapse: () => void;
}

const ALL_EVENT_TYPES: TimelineEventType[] = [
  "message",
  "rag_search",
  "rag_retrieval",
  "rag_generation",
  "rag_complete",
  "error"
];

const ALL_STATUSES: TimelineEventStatus[] = ["pending", "in_progress", "success", "error"];

export function TimelineFilters({
  filters,
  onFiltersChange,
  isCollapsed,
  onToggleCollapse
}: TimelineFiltersProps) {
  const toggleEventType = (type: TimelineEventType) => {
    const newTypes = new Set(filters.eventTypes);
    if (newTypes.has(type)) {
      newTypes.delete(type);
    } else {
      newTypes.add(type);
    }
    onFiltersChange({ ...filters, eventTypes: newTypes });
  };

  const toggleStatus = (status: TimelineEventStatus) => {
    const newStatuses = new Set(filters.statuses);
    if (newStatuses.has(status)) {
      newStatuses.delete(status);
    } else {
      newStatuses.add(status);
    }
    onFiltersChange({ ...filters, statuses: newStatuses });
  };

  const selectAllEventTypes = () => {
    onFiltersChange({ ...filters, eventTypes: new Set(ALL_EVENT_TYPES) });
  };

  const deselectAllEventTypes = () => {
    onFiltersChange({ ...filters, eventTypes: new Set() });
  };

  const selectAllStatuses = () => {
    onFiltersChange({ ...filters, statuses: new Set(ALL_STATUSES) });
  };

  const deselectAllStatuses = () => {
    onFiltersChange({ ...filters, statuses: new Set() });
  };

  const handleSearchChange = (text: string) => {
    onFiltersChange({ ...filters, searchText: text || undefined });
  };

  if (isCollapsed) {
    return (
      <aside
        aria-label="Timeline filters (collapsed)"
        style={{
          width: 60,
          background: "#f8f9fa",
          borderRight: "1px solid #dadce0",
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          padding: "16px 8px"
        }}
      >
        <button
          onClick={onToggleCollapse}
          aria-label="Show filters"
          style={{
            padding: 12,
            background: "#1a73e8",
            color: "white",
            border: "none",
            borderRadius: 4,
            cursor: "pointer",
            fontSize: 20,
            width: "100%"
          }}
          title="Mostra filtri"
        >
          ☰
        </button>
        <div
          aria-hidden="true"
          style={{
            marginTop: 16,
            fontSize: 11,
            color: "#64748b",
            textAlign: "center",
            writingMode: "vertical-rl",
            transform: "rotate(180deg)"
          }}
        >
          Filtri
        </div>
      </aside>
    );
  }

  return (
    <aside
      aria-label="Timeline filters"
      style={{
        width: 280,
        background: "#f8f9fa",
        borderRight: "1px solid #dadce0",
        display: "flex",
        flexDirection: "column",
        overflowY: "auto"
      }}
    >
      {/* Header */}
      <div
        style={{
          padding: 16,
          borderBottom: "1px solid #dadce0",
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center"
        }}
      >
        <h3 style={{ margin: 0, fontSize: 16, fontWeight: 600 }}>Filtri Timeline</h3>
        <button
          onClick={onToggleCollapse}
          aria-label="Hide filters"
          style={{
            padding: "6px 10px",
            background: "#f1f3f4",
            border: "none",
            borderRadius: 4,
            cursor: "pointer",
            fontSize: 16
          }}
          title="Nascondi filtri"
        >
          ✕
        </button>
      </div>

      <div style={{ flex: 1, overflowY: "auto", padding: 16 }}>
        {/* Search */}
        <div style={{ marginBottom: 24 }}>
          <label
            htmlFor="timeline-search"
            style={{ display: "block", marginBottom: 8, fontSize: 13, fontWeight: 600 }}
          >
            Cerca
          </label>
          <input
            id="timeline-search"
            type="text"
            value={filters.searchText || ""}
            onChange={(e) => handleSearchChange(e.target.value)}
            placeholder="Cerca nei messaggi, citazioni..."
            style={{
              width: "100%",
              padding: 8,
              fontSize: 13,
              border: "1px solid #dadce0",
              borderRadius: 4,
              background: "white"
            }}
          />
        </div>

        {/* Event Types */}
        <div style={{ marginBottom: 24 }}>
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              marginBottom: 8
            }}
          >
            <label style={{ fontSize: 13, fontWeight: 600 }}>Tipo Evento</label>
            <div role="group" aria-label="Event type filter controls" style={{ display: "flex", gap: 4 }}>
              <button
                onClick={selectAllEventTypes}
                aria-label="Select all event types"
                style={{
                  padding: "2px 6px",
                  fontSize: 10,
                  background: "#e8f0fe",
                  color: "#1967d2",
                  border: "none",
                  borderRadius: 3,
                  cursor: "pointer"
                }}
              >
                Tutti
              </button>
              <button
                onClick={deselectAllEventTypes}
                aria-label="Deselect all event types"
                style={{
                  padding: "2px 6px",
                  fontSize: 10,
                  background: "#f1f3f4",
                  color: "#64748b",
                  border: "none",
                  borderRadius: 3,
                  cursor: "pointer"
                }}
              >
                Nessuno
              </button>
            </div>
          </div>
          {ALL_EVENT_TYPES.map((type) => {
            const isChecked = filters.eventTypes.has(type);
            const color = getEventTypeColor(type);
            return (
              <label
                key={type}
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: 8,
                  padding: 8,
                  marginBottom: 4,
                  background: isChecked ? "#e8f0fe" : "white",
                  border: `1px solid ${isChecked ? color : "#dadce0"}`,
                  borderRadius: 4,
                  cursor: "pointer",
                  fontSize: 12,
                  transition: "all 0.2s ease"
                }}
              >
                <input
                  type="checkbox"
                  checked={isChecked}
                  onChange={() => toggleEventType(type)}
                  style={{ cursor: "pointer" }}
                />
                <div
                  style={{
                    width: 12,
                    height: 12,
                    background: color,
                    borderRadius: "50%"
                  }}
                />
                <span style={{ flex: 1 }}>{getEventTypeLabel(type)}</span>
              </label>
            );
          })}
        </div>

        {/* Statuses */}
        <div style={{ marginBottom: 24 }}>
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              marginBottom: 8
            }}
          >
            <label style={{ fontSize: 13, fontWeight: 600 }}>Stato</label>
            <div role="group" aria-label="Status filter controls" style={{ display: "flex", gap: 4 }}>
              <button
                onClick={selectAllStatuses}
                aria-label="Select all statuses"
                style={{
                  padding: "2px 6px",
                  fontSize: 10,
                  background: "#e8f0fe",
                  color: "#1967d2",
                  border: "none",
                  borderRadius: 3,
                  cursor: "pointer"
                }}
              >
                Tutti
              </button>
              <button
                onClick={deselectAllStatuses}
                aria-label="Deselect all statuses"
                style={{
                  padding: "2px 6px",
                  fontSize: 10,
                  background: "#f1f3f4",
                  color: "#64748b",
                  border: "none",
                  borderRadius: 3,
                  cursor: "pointer"
                }}
              >
                Nessuno
              </button>
            </div>
          </div>
          {ALL_STATUSES.map((status) => {
            const isChecked = filters.statuses.has(status);
            const statusLabels: Record<TimelineEventStatus, string> = {
              pending: "In Attesa",
              in_progress: "In Corso",
              success: "Completato",
              error: "Errore"
            };
            return (
              <label
                key={status}
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: 8,
                  padding: 8,
                  marginBottom: 4,
                  background: isChecked ? "#e8f0fe" : "white",
                  border: `1px solid ${isChecked ? "#1a73e8" : "#dadce0"}`,
                  borderRadius: 4,
                  cursor: "pointer",
                  fontSize: 12,
                  transition: "all 0.2s ease"
                }}
              >
                <input
                  type="checkbox"
                  checked={isChecked}
                  onChange={() => toggleStatus(status)}
                  style={{ cursor: "pointer" }}
                />
                <span style={{ flex: 1 }}>{statusLabels[status]}</span>
              </label>
            );
          })}
        </div>

        {/* Reset Button */}
        <button
          onClick={() =>
            onFiltersChange({
              eventTypes: new Set(ALL_EVENT_TYPES),
              statuses: new Set(ALL_STATUSES),
              searchText: undefined
            })
          }
          aria-label="Reset all filters"
          style={{
            width: "100%",
            padding: 10,
            background: "#f1f3f4",
            color: "#64748b",
            border: "none",
            borderRadius: 4,
            fontSize: 13,
            fontWeight: 500,
            cursor: "pointer"
          }}
        >
          Ripristina Filtri
        </button>
      </div>
    </aside>
  );
}
