// UI-04: Timeline event item component
import {
  TimelineEvent,
  Snippet,
  getEventTypeLabel,
  getEventTypeColor,
  getStatusIcon,
  formatDuration
} from "@/lib/timeline-types";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";

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
    <Card
      className={`mb-3 transition-all ${
        isSelected ? "border-2" : "border"
      } ${isSelected ? "bg-muted" : ""}`}
      style={isSelected ? { borderColor: typeColor } : {}}
    >
      <CardHeader
        className="p-3 cursor-pointer flex-row items-center gap-3 space-y-0"
        style={{ borderBottom: isExpanded ? "1px solid hsl(var(--border))" : "none" }}
        onClick={() => onSelect(event.id)}
      >
        {/* Status Icon */}
        <div style={{ fontSize: 20, flexShrink: 0 }}>{statusIcon}</div>

        {/* Event Type Badge */}
        <Badge
          className="uppercase tracking-wider shrink-0 text-white"
          style={{ background: typeColor }}
        >
          {getEventTypeLabel(event.type)}
        </Badge>

        {/* Event Summary */}
        <div className="flex-1 min-w-0">
          <div className="text-sm font-medium text-foreground overflow-hidden text-ellipsis whitespace-nowrap">
            {event.data.message || getEventTypeLabel(event.type)}
          </div>
          <div className="text-xs text-muted-foreground mt-0.5">
            {event.timestamp.toLocaleTimeString()}
          </div>
        </div>

        {/* Metrics Badge */}
        {event.data.metrics && (
          <Badge variant="secondary" className="shrink-0">
            {event.data.metrics.latencyMs !== undefined &&
              formatDuration(event.data.metrics.latencyMs)}
            {event.data.metrics.totalTokens !== undefined &&
              ` • ${event.data.metrics.totalTokens} tokens`}
          </Badge>
        )}

        {/* Expand/Collapse Button */}
        <Button
          onClick={(e) => {
            e.stopPropagation();
            onToggleExpand(event.id);
          }}
          variant="ghost"
          size="sm"
          className="shrink-0 text-base"
          title={isExpanded ? "Comprimi" : "Espandi"}
        >
          {isExpanded ? "▲" : "▼"}
        </Button>
      </CardHeader>

      {/* Expanded Details */}
      {isExpanded && (
        <CardContent className="p-3 bg-muted/50 text-xs text-foreground">
          {/* Message Content */}
          {event.data.message && event.type === "message" && (
            <div style={{ marginBottom: 12 }}>
              <div style={{ fontWeight: 600, marginBottom: 6, color: "#64748b" }}>Messaggio:</div>
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
              <div style={{ fontWeight: 600, marginBottom: 6, color: "#64748b" }}>
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
                  <div style={{ color: "#64748b", fontSize: 11 }}>{citation.text}</div>
                </div>
              ))}
            </div>
          )}

          {/* Metrics */}
          {event.data.metrics && (
            <div style={{ marginBottom: 12 }}>
              <div style={{ fontWeight: 600, marginBottom: 6, color: "#64748b" }}>Metriche:</div>
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
                    <div style={{ fontSize: 10, color: "#64748b", marginBottom: 2 }}>Latenza</div>
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
                    <div style={{ fontSize: 10, color: "#64748b", marginBottom: 2 }}>
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
                    <div style={{ fontSize: 10, color: "#64748b", marginBottom: 2 }}>
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
                    <div style={{ fontSize: 10, color: "#64748b", marginBottom: 2 }}>
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
                    <div style={{ fontSize: 10, color: "#64748b", marginBottom: 2 }}>Confidenza</div>
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
                color: "#64748b",
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
        </CardContent>
      )}
    </Card>
  );
}
