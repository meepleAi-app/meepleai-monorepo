// UI-04: Timeline event item component
import {
  TimelineEvent,
  Snippet,
  getEventTypeLabel,
  getEventTypeColor,
  getStatusIcon,
  formatDuration
} from "@/lib/timeline-types";

interface TimelineEventItemProps {
  event: TimelineEvent;
  isSelected: boolean;
  onSelect: (eventId: string) => void;
  isExpanded: boolean;
  onToggleExpand: (eventId: string) => void;
}

export function TimelineEventItem({
  event,
  isSelected,
  onSelect,
  isExpanded,
  onToggleExpand
}: TimelineEventItemProps) {
  const typeColor = getEventTypeColor(event.type);
  const statusIcon = getStatusIcon(event.status);

  return (
    <div
      style={{
        marginBottom: 12,
        border: `2px solid ${isSelected ? typeColor : "#dadce0"}`,
        borderRadius: 8,
        background: isSelected ? "#f8f9fa" : "white",
        overflow: "hidden",
        transition: "all 0.2s ease"
      }}
    >
      {/* Event Header */}
      <div
        style={{
          padding: 12,
          display: "flex",
          alignItems: "center",
          gap: 12,
          cursor: "pointer",
          borderBottom: isExpanded ? "1px solid #dadce0" : "none"
        }}
        onClick={() => onSelect(event.id)}
      >
        {/* Status Icon */}
        <div style={{ fontSize: 20, flexShrink: 0 }}>{statusIcon}</div>

        {/* Event Type Badge */}
        <div
          style={{
            padding: "4px 10px",
            background: typeColor,
            color: "white",
            borderRadius: 12,
            fontSize: 11,
            fontWeight: 600,
            textTransform: "uppercase",
            letterSpacing: "0.5px",
            flexShrink: 0
          }}
        >
          {getEventTypeLabel(event.type)}
        </div>

        {/* Event Summary */}
        <div style={{ flex: 1, minWidth: 0 }}>
          <div
            style={{
              fontSize: 13,
              fontWeight: 500,
              color: "#202124",
              overflow: "hidden",
              textOverflow: "ellipsis",
              whiteSpace: "nowrap"
            }}
          >
            {event.data.message || getEventTypeLabel(event.type)}
          </div>
          <div style={{ fontSize: 11, color: "#5f6368", marginTop: 2 }}>
            {event.timestamp.toLocaleTimeString()}
          </div>
        </div>

        {/* Metrics Badge */}
        {event.data.metrics && (
          <div
            style={{
              padding: "4px 8px",
              background: "#e8f0fe",
              color: "#1967d2",
              borderRadius: 4,
              fontSize: 11,
              fontWeight: 500,
              flexShrink: 0
            }}
          >
            {event.data.metrics.latencyMs !== undefined &&
              formatDuration(event.data.metrics.latencyMs)}
            {event.data.metrics.totalTokens !== undefined &&
              ` • ${event.data.metrics.totalTokens} tokens`}
          </div>
        )}

        {/* Expand/Collapse Button */}
        <button
          onClick={(e) => {
            e.stopPropagation();
            onToggleExpand(event.id);
          }}
          style={{
            padding: "4px 8px",
            background: "transparent",
            border: "none",
            cursor: "pointer",
            fontSize: 16,
            color: "#5f6368",
            flexShrink: 0
          }}
          title={isExpanded ? "Comprimi" : "Espandi"}
        >
          {isExpanded ? "▲" : "▼"}
        </button>
      </div>

      {/* Expanded Details */}
      {isExpanded && (
        <div
          style={{
            padding: 12,
            background: "#fafafa",
            fontSize: 12,
            color: "#202124"
          }}
        >
          {/* Message Content */}
          {event.data.message && event.type === "message" && (
            <div style={{ marginBottom: 12 }}>
              <div style={{ fontWeight: 600, marginBottom: 6, color: "#5f6368" }}>Messaggio:</div>
              <div
                style={{
                  padding: 10,
                  background: "white",
                  border: "1px solid #dadce0",
                  borderRadius: 4,
                  whiteSpace: "pre-wrap"
                }}
              >
                {event.data.message}
              </div>
            </div>
          )}

          {/* Citations */}
          {event.data.citations && event.data.citations.length > 0 && (
            <div style={{ marginBottom: 12 }}>
              <div style={{ fontWeight: 600, marginBottom: 6, color: "#5f6368" }}>
                Citazioni ({event.data.citations.length}):
              </div>
              {event.data.citations.map((citation: Snippet, idx: number) => (
                <div
                  key={idx}
                  style={{
                    padding: 8,
                    background: "white",
                    border: "1px solid #dadce0",
                    borderRadius: 4,
                    marginBottom: 6
                  }}
                >
                  <div style={{ fontWeight: 500, marginBottom: 4 }}>
                    {citation.source}
                    {citation.page !== null && citation.page !== undefined && ` (Pagina ${citation.page})`}
                  </div>
                  <div style={{ color: "#5f6368", fontSize: 11 }}>{citation.text}</div>
                </div>
              ))}
            </div>
          )}

          {/* Metrics */}
          {event.data.metrics && (
            <div style={{ marginBottom: 12 }}>
              <div style={{ fontWeight: 600, marginBottom: 6, color: "#5f6368" }}>Metriche:</div>
              <div
                style={{
                  display: "grid",
                  gridTemplateColumns: "repeat(2, 1fr)",
                  gap: 8
                }}
              >
                {event.data.metrics.latencyMs !== undefined && (
                  <div
                    style={{
                      padding: 8,
                      background: "white",
                      border: "1px solid #dadce0",
                      borderRadius: 4
                    }}
                  >
                    <div style={{ fontSize: 10, color: "#5f6368", marginBottom: 2 }}>Latenza</div>
                    <div style={{ fontWeight: 600, color: "#1a73e8" }}>
                      {formatDuration(event.data.metrics.latencyMs)}
                    </div>
                  </div>
                )}
                {event.data.metrics.promptTokens !== undefined && (
                  <div
                    style={{
                      padding: 8,
                      background: "white",
                      border: "1px solid #dadce0",
                      borderRadius: 4
                    }}
                  >
                    <div style={{ fontSize: 10, color: "#5f6368", marginBottom: 2 }}>
                      Token Prompt
                    </div>
                    <div style={{ fontWeight: 600, color: "#1a73e8" }}>
                      {event.data.metrics.promptTokens}
                    </div>
                  </div>
                )}
                {event.data.metrics.completionTokens !== undefined && (
                  <div
                    style={{
                      padding: 8,
                      background: "white",
                      border: "1px solid #dadce0",
                      borderRadius: 4
                    }}
                  >
                    <div style={{ fontSize: 10, color: "#5f6368", marginBottom: 2 }}>
                      Token Completamento
                    </div>
                    <div style={{ fontWeight: 600, color: "#1a73e8" }}>
                      {event.data.metrics.completionTokens}
                    </div>
                  </div>
                )}
                {event.data.metrics.totalTokens !== undefined && (
                  <div
                    style={{
                      padding: 8,
                      background: "white",
                      border: "1px solid #dadce0",
                      borderRadius: 4
                    }}
                  >
                    <div style={{ fontSize: 10, color: "#5f6368", marginBottom: 2 }}>
                      Totale Token
                    </div>
                    <div style={{ fontWeight: 600, color: "#1a73e8" }}>
                      {event.data.metrics.totalTokens}
                    </div>
                  </div>
                )}
                {event.data.metrics.confidence !== undefined && (
                  <div
                    style={{
                      padding: 8,
                      background: "white",
                      border: "1px solid #dadce0",
                      borderRadius: 4
                    }}
                  >
                    <div style={{ fontSize: 10, color: "#5f6368", marginBottom: 2 }}>Confidenza</div>
                    <div style={{ fontWeight: 600, color: "#188038" }}>
                      {(event.data.metrics.confidence * 100).toFixed(1)}%
                    </div>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Error */}
          {event.data.error && (
            <div style={{ marginBottom: 12 }}>
              <div style={{ fontWeight: 600, marginBottom: 6, color: "#d93025" }}>Errore:</div>
              <div
                style={{
                  padding: 10,
                  background: "#fce8e6",
                  border: "1px solid #d93025",
                  borderRadius: 4,
                  color: "#d93025"
                }}
              >
                {event.data.error}
              </div>
            </div>
          )}

          {/* Technical Details */}
          <details style={{ marginTop: 8 }}>
            <summary
              style={{
                cursor: "pointer",
                color: "#5f6368",
                fontSize: 11,
                fontWeight: 500,
                padding: "4px 0"
              }}
            >
              Dettagli Tecnici
            </summary>
            <div
              style={{
                marginTop: 8,
                padding: 8,
                background: "#f1f3f4",
                borderRadius: 4,
                fontFamily: "monospace",
                fontSize: 11
              }}
            >
              <div>ID: {event.id}</div>
              <div>Type: {event.type}</div>
              <div>Status: {event.status}</div>
              <div>Timestamp: {event.timestamp.toISOString()}</div>
              {event.relatedMessageId && <div>Related Message: {event.relatedMessageId}</div>}
              {event.data.endpoint && <div>Endpoint: {event.data.endpoint}</div>}
              {event.data.gameId && <div>Game ID: {event.data.gameId}</div>}
              {event.data.chatId && <div>Chat ID: {event.data.chatId}</div>}
            </div>
          </details>
        </div>
      )}
    </div>
  );
}
