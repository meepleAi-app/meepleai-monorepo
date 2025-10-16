// UI-04: Timeline event details panel
import {
  TimelineEvent,
  Snippet,
  getEventTypeLabel,
  getEventTypeColor,
  getStatusIcon,
  formatDuration
} from "@/lib/timeline-types";

interface TimelineDetailsProps {
  event: TimelineEvent | null;
  isCollapsed: boolean;
  onToggleCollapse: () => void;
}

export function TimelineDetails({ event, isCollapsed, onToggleCollapse }: TimelineDetailsProps) {
  if (isCollapsed) {
    return (
      <div
        style={{
          width: 60,
          background: "#f8f9fa",
          borderLeft: "1px solid #dadce0",
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          padding: "16px 8px"
        }}
      >
        <button
          onClick={onToggleCollapse}
          style={{
            padding: 12,
            background: event ? "#1a73e8" : "#f1f3f4",
            color: event ? "white" : "#64748b",
            border: "none",
            borderRadius: 4,
            cursor: "pointer",
            fontSize: 20,
            width: "100%"
          }}
          title="Mostra dettagli"
          disabled={!event}
        >
          ☰
        </button>
        {event && (
          <div
            style={{
              marginTop: 16,
              fontSize: 11,
              color: "#64748b",
              textAlign: "center",
              writingMode: "vertical-rl",
              transform: "rotate(180deg)"
            }}
          >
            Dettagli
          </div>
        )}
      </div>
    );
  }

  return (
    <div
      style={{
        width: 360,
        background: "#f8f9fa",
        borderLeft: "1px solid #dadce0",
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
        <h3 style={{ margin: 0, fontSize: 16, fontWeight: 600 }}>Dettagli Evento</h3>
        <button
          onClick={onToggleCollapse}
          style={{
            padding: "6px 10px",
            background: "#f1f3f4",
            border: "none",
            borderRadius: 4,
            cursor: "pointer",
            fontSize: 16
          }}
          title="Nascondi dettagli"
        >
          ✕
        </button>
      </div>

      {/* Content */}
      {!event ? (
        <div
          style={{
            flex: 1,
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            justifyContent: "center",
            padding: 32,
            textAlign: "center",
            color: "#64748b"
          }}
        >
          <div style={{ fontSize: 48, marginBottom: 16 }}>👈</div>
          <div style={{ fontSize: 14 }}>Seleziona un evento dalla timeline per vedere i dettagli</div>
        </div>
      ) : (
        <div style={{ flex: 1, overflowY: "auto", padding: 16 }}>
          {/* Event Header */}
          <div
            style={{
              padding: 16,
              background: "white",
              border: `2px solid ${getEventTypeColor(event.type)}`,
              borderRadius: 8,
              marginBottom: 16
            }}
          >
            <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 12 }}>
              <div style={{ fontSize: 28 }}>{getStatusIcon(event.status)}</div>
              <div>
                <div
                  style={{
                    padding: "4px 10px",
                    background: getEventTypeColor(event.type),
                    color: "white",
                    borderRadius: 12,
                    fontSize: 11,
                    fontWeight: 600,
                    textTransform: "uppercase",
                    letterSpacing: "0.5px",
                    display: "inline-block"
                  }}
                >
                  {getEventTypeLabel(event.type)}
                </div>
                <div style={{ fontSize: 11, color: "#64748b", marginTop: 4 }}>
                  {event.timestamp.toLocaleString()}
                </div>
              </div>
            </div>

            {event.data.message && (
              <div style={{ fontSize: 14, color: "#202124", lineHeight: 1.5 }}>
                {event.data.message}
              </div>
            )}
          </div>

          {/* Role (for messages) */}
          {event.data.role && (
            <div style={{ marginBottom: 16 }}>
              <div
                style={{
                  fontSize: 12,
                  fontWeight: 600,
                  color: "#64748b",
                  marginBottom: 8,
                  textTransform: "uppercase",
                  letterSpacing: "0.5px"
                }}
              >
                Ruolo
              </div>
              <div
                style={{
                  padding: 12,
                  background: "white",
                  border: "1px solid #dadce0",
                  borderRadius: 8,
                  fontSize: 13
                }}
              >
                {event.data.role === "user" ? "Utente" : "Assistente"}
              </div>
            </div>
          )}

          {/* Metrics */}
          {event.data.metrics && (
            <div style={{ marginBottom: 16 }}>
              <div
                style={{
                  fontSize: 12,
                  fontWeight: 600,
                  color: "#64748b",
                  marginBottom: 8,
                  textTransform: "uppercase",
                  letterSpacing: "0.5px"
                }}
              >
                Metriche
              </div>
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                {event.data.metrics.latencyMs !== undefined && (
                  <div
                    style={{
                      padding: 12,
                      background: "white",
                      border: "1px solid #dadce0",
                      borderRadius: 8,
                      display: "flex",
                      justifyContent: "space-between",
                      alignItems: "center"
                    }}
                  >
                    <span style={{ fontSize: 13, color: "#64748b" }}>Latenza</span>
                    <span style={{ fontSize: 15, fontWeight: 600, color: "#1a73e8" }}>
                      {formatDuration(event.data.metrics.latencyMs)}
                    </span>
                  </div>
                )}
                {event.data.metrics.promptTokens !== undefined && (
                  <div
                    style={{
                      padding: 12,
                      background: "white",
                      border: "1px solid #dadce0",
                      borderRadius: 8,
                      display: "flex",
                      justifyContent: "space-between",
                      alignItems: "center"
                    }}
                  >
                    <span style={{ fontSize: 13, color: "#64748b" }}>Token Prompt</span>
                    <span style={{ fontSize: 15, fontWeight: 600, color: "#1a73e8" }}>
                      {event.data.metrics.promptTokens}
                    </span>
                  </div>
                )}
                {event.data.metrics.completionTokens !== undefined && (
                  <div
                    style={{
                      padding: 12,
                      background: "white",
                      border: "1px solid #dadce0",
                      borderRadius: 8,
                      display: "flex",
                      justifyContent: "space-between",
                      alignItems: "center"
                    }}
                  >
                    <span style={{ fontSize: 13, color: "#64748b" }}>Token Completamento</span>
                    <span style={{ fontSize: 15, fontWeight: 600, color: "#1a73e8" }}>
                      {event.data.metrics.completionTokens}
                    </span>
                  </div>
                )}
                {event.data.metrics.totalTokens !== undefined && (
                  <div
                    style={{
                      padding: 12,
                      background: "white",
                      border: "1px solid #dadce0",
                      borderRadius: 8,
                      display: "flex",
                      justifyContent: "space-between",
                      alignItems: "center"
                    }}
                  >
                    <span style={{ fontSize: 13, color: "#64748b" }}>Totale Token</span>
                    <span style={{ fontSize: 15, fontWeight: 600, color: "#1a73e8" }}>
                      {event.data.metrics.totalTokens}
                    </span>
                  </div>
                )}
                {event.data.metrics.confidence !== undefined && (
                  <div
                    style={{
                      padding: 12,
                      background: "white",
                      border: "1px solid #dadce0",
                      borderRadius: 8,
                      display: "flex",
                      justifyContent: "space-between",
                      alignItems: "center"
                    }}
                  >
                    <span style={{ fontSize: 13, color: "#64748b" }}>Confidenza</span>
                    <span style={{ fontSize: 15, fontWeight: 600, color: "#188038" }}>
                      {(event.data.metrics.confidence * 100).toFixed(1)}%
                    </span>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Citations */}
          {event.data.citations && event.data.citations.length > 0 && (
            <div style={{ marginBottom: 16 }}>
              <div
                style={{
                  fontSize: 12,
                  fontWeight: 600,
                  color: "#64748b",
                  marginBottom: 8,
                  textTransform: "uppercase",
                  letterSpacing: "0.5px"
                }}
              >
                Citazioni ({event.data.citations.length})
              </div>
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                {event.data.citations.map((citation: Snippet, idx: number) => (
                  <div
                    key={idx}
                    style={{
                      padding: 12,
                      background: "white",
                      border: "1px solid #dadce0",
                      borderRadius: 8
                    }}
                  >
                    <div style={{ fontWeight: 600, fontSize: 13, marginBottom: 6, color: "#202124" }}>
                      {citation.source}
                      {citation.page !== null && citation.page !== undefined && (
                        <span style={{ color: "#64748b", fontWeight: 400 }}>
                          {" "}
                          • Pagina {citation.page}
                        </span>
                      )}
                    </div>
                    <div
                      style={{
                        fontSize: 12,
                        color: "#64748b",
                        lineHeight: 1.5,
                        whiteSpace: "pre-wrap"
                      }}
                    >
                      {citation.text}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Error */}
          {event.data.error && (
            <div style={{ marginBottom: 16 }}>
              <div
                style={{
                  fontSize: 12,
                  fontWeight: 600,
                  color: "#d93025",
                  marginBottom: 8,
                  textTransform: "uppercase",
                  letterSpacing: "0.5px"
                }}
              >
                Errore
              </div>
              <div
                style={{
                  padding: 12,
                  background: "#fce8e6",
                  border: "1px solid #d93025",
                  borderRadius: 8,
                  color: "#d93025",
                  fontSize: 13,
                  lineHeight: 1.5
                }}
              >
                {event.data.error}
              </div>
            </div>
          )}

          {/* Technical Info */}
          <details>
            <summary
              style={{
                fontSize: 12,
                fontWeight: 600,
                color: "#64748b",
                marginBottom: 8,
                textTransform: "uppercase",
                letterSpacing: "0.5px",
                cursor: "pointer",
                padding: "8px 0"
              }}
            >
              Informazioni Tecniche
            </summary>
            <div
              style={{
                padding: 12,
                background: "#f1f3f4",
                borderRadius: 8,
                fontFamily: "monospace",
                fontSize: 11,
                color: "#202124",
                lineHeight: 1.6
              }}
            >
              <div style={{ marginBottom: 4 }}>
                <strong>ID:</strong> {event.id}
              </div>
              <div style={{ marginBottom: 4 }}>
                <strong>Type:</strong> {event.type}
              </div>
              <div style={{ marginBottom: 4 }}>
                <strong>Status:</strong> {event.status}
              </div>
              <div style={{ marginBottom: 4 }}>
                <strong>Timestamp:</strong> {event.timestamp.toISOString()}
              </div>
              {event.relatedMessageId && (
                <div style={{ marginBottom: 4 }}>
                  <strong>Related Message:</strong> {event.relatedMessageId}
                </div>
              )}
              {event.data.endpoint && (
                <div style={{ marginBottom: 4 }}>
                  <strong>Endpoint:</strong> {event.data.endpoint}
                </div>
              )}
              {event.data.gameId && (
                <div style={{ marginBottom: 4 }}>
                  <strong>Game ID:</strong> {event.data.gameId}
                </div>
              )}
              {event.data.chatId && (
                <div style={{ marginBottom: 4 }}>
                  <strong>Chat ID:</strong> {event.data.chatId}
                </div>
              )}
            </div>
          </details>
        </div>
      )}
    </div>
  );
}
