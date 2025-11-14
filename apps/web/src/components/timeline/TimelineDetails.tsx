// UI-04: Timeline event details panel
import {
  TimelineEvent,
  Snippet,
  getEventTypeLabel,
  getEventTypeColor,
  getStatusIcon,
  formatDuration
} from "@/lib/timeline-types";
import { cn } from "@/lib/utils";

interface TimelineDetailsProps {
  event: TimelineEvent | null;
  isCollapsed: boolean;
  onToggleCollapse: () => void;
}

export function TimelineDetails({ event, isCollapsed, onToggleCollapse }: TimelineDetailsProps) {
  if (isCollapsed) {
    return (
      <div className="w-[60px] bg-gray-50 border-l border-gray-300 flex flex-col items-center p-4">
        <button
          onClick={onToggleCollapse}
          className={cn(
            "p-3 rounded border-none cursor-pointer text-xl w-full",
            event ? "bg-blue-600 text-white" : "bg-gray-100 text-slate-500"
          )}
          title="Mostra dettagli"
          disabled={!event}
        >
          ☰
        </button>
        {event && (
          <div className="mt-4 text-[11px] text-slate-500 text-center [writing-mode:vertical-rl] rotate-180">
            Dettagli
          </div>
        )}
      </div>
    );
  }

  return (
    <div className="w-[360px] bg-gray-50 border-l border-gray-300 flex flex-col overflow-y-auto">
      {/* Header */}
      <div className="p-4 border-b border-gray-300 flex justify-between items-center">
        <h3 className="m-0 text-base font-semibold">Dettagli Evento</h3>
        <button
          onClick={onToggleCollapse}
          className="px-2.5 py-1.5 bg-gray-100 border-none rounded cursor-pointer text-base"
          title="Nascondi dettagli"
        >
          ✕
        </button>
      </div>

      {/* Content */}
      {!event ? (
        <div className="flex-1 flex flex-col items-center justify-center p-8 text-center text-slate-500">
          <div className="text-5xl mb-4">👈</div>
          <div className="text-sm">Seleziona un evento dalla timeline per vedere i dettagli</div>
        </div>
      ) : (
        <div className="flex-1 overflow-y-auto p-4">
          {/* Event Header */}
          <div
            className="p-4 bg-white border-2 rounded-lg mb-4"
            style={{ borderColor: getEventTypeColor(event.type) }}
          >
            <div className="flex items-center gap-3 mb-3">
              <div className="text-3xl">{getStatusIcon(event.status)}</div>
              <div>
                <div
                  className="py-1 px-2.5 text-white rounded-xl text-[11px] font-semibold uppercase tracking-wider inline-block"
                  style={{ background: getEventTypeColor(event.type) }}
                >
                  {getEventTypeLabel(event.type)}
                </div>
                <div className="text-[11px] text-slate-500 mt-1">
                  {event.timestamp.toLocaleString()}
                </div>
              </div>
            </div>

            {event.data.message && (
              <div className="text-sm text-gray-900 leading-normal">
                {event.data.message}
              </div>
            )}
          </div>

          {/* Role (for messages) */}
          {event.data.role && (
            <div className="mb-4">
              <div className="text-xs font-semibold text-slate-500 mb-2 uppercase tracking-wider">
                Ruolo
              </div>
              <div className="p-3 bg-white border border-gray-300 rounded-lg text-[13px]">
                {event.data.role === "user" ? "Utente" : "Assistente"}
              </div>
            </div>
          )}

          {/* Metrics */}
          {event.data.metrics && (
            <div className="mb-4">
              <div className="text-xs font-semibold text-slate-500 mb-2 uppercase tracking-wider">
                Metriche
              </div>
              <div className="flex flex-col gap-2">
                {event.data.metrics.latencyMs !== undefined && (
                  <div className="p-3 bg-white border border-gray-300 rounded-lg flex justify-between items-center">
                    <span className="text-[13px] text-slate-500">Latenza</span>
                    <span className="text-[15px] font-semibold text-blue-600">
                      {formatDuration(event.data.metrics.latencyMs)}
                    </span>
                  </div>
                )}
                {event.data.metrics.promptTokens !== undefined && (
                  <div className="p-3 bg-white border border-gray-300 rounded-lg flex justify-between items-center">
                    <span className="text-[13px] text-slate-500">Token Prompt</span>
                    <span className="text-[15px] font-semibold text-blue-600">
                      {event.data.metrics.promptTokens}
                    </span>
                  </div>
                )}
                {event.data.metrics.completionTokens !== undefined && (
                  <div className="p-3 bg-white border border-gray-300 rounded-lg flex justify-between items-center">
                    <span className="text-[13px] text-slate-500">Token Completamento</span>
                    <span className="text-[15px] font-semibold text-blue-600">
                      {event.data.metrics.completionTokens}
                    </span>
                  </div>
                )}
                {event.data.metrics.totalTokens !== undefined && (
                  <div className="p-3 bg-white border border-gray-300 rounded-lg flex justify-between items-center">
                    <span className="text-[13px] text-slate-500">Totale Token</span>
                    <span className="text-[15px] font-semibold text-blue-600">
                      {event.data.metrics.totalTokens}
                    </span>
                  </div>
                )}
                {event.data.metrics.confidence !== undefined && (
                  <div className="p-3 bg-white border border-gray-300 rounded-lg flex justify-between items-center">
                    <span className="text-[13px] text-slate-500">Confidenza</span>
                    <span className="text-[15px] font-semibold text-green-700">
                      {(event.data.metrics.confidence * 100).toFixed(1)}%
                    </span>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Citations */}
          {event.data.citations && event.data.citations.length > 0 && (
            <div className="mb-4">
              <div className="text-xs font-semibold text-slate-500 mb-2 uppercase tracking-wider">
                Citazioni ({event.data.citations.length})
              </div>
              <div className="flex flex-col gap-2">
                {event.data.citations.map((citation: Snippet, idx: number) => (
                  <div
                    key={idx}
                    className="p-3 bg-white border border-gray-300 rounded-lg"
                  >
                    <div className="font-semibold text-[13px] mb-1.5 text-gray-900">
                      {citation.source}
                      {citation.page !== null && citation.page !== undefined && (
                        <span className="text-slate-500 font-normal">
                          {" "}
                          • Pagina {citation.page}
                        </span>
                      )}
                    </div>
                    <div className="text-xs text-slate-500 leading-normal whitespace-pre-wrap">
                      {citation.text}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Error */}
          {event.data.error && (
            <div className="mb-4">
              <div className="text-xs font-semibold text-red-600 mb-2 uppercase tracking-wider">
                Errore
              </div>
              <div className="p-3 bg-red-50 border border-red-600 rounded-lg text-red-600 text-[13px] leading-normal">
                {event.data.error}
              </div>
            </div>
          )}

          {/* Technical Info */}
          <details>
            <summary className="text-xs font-semibold text-slate-500 mb-2 uppercase tracking-wider cursor-pointer py-2">
              Informazioni Tecniche
            </summary>
            <div className="p-3 bg-gray-100 rounded-lg font-mono text-[11px] text-gray-900 leading-relaxed">
              <div className="mb-1">
                <strong>ID:</strong> {event.id}
              </div>
              <div className="mb-1">
                <strong>Type:</strong> {event.type}
              </div>
              <div className="mb-1">
                <strong>Status:</strong> {event.status}
              </div>
              <div className="mb-1">
                <strong>Timestamp:</strong> {event.timestamp.toISOString()}
              </div>
              {event.relatedMessageId && (
                <div className="mb-1">
                  <strong>Related Message:</strong> {event.relatedMessageId}
                </div>
              )}
              {event.data.endpoint && (
                <div className="mb-1">
                  <strong>Endpoint:</strong> {event.data.endpoint}
                </div>
              )}
              {event.data.gameId && (
                <div className="mb-1">
                  <strong>Game ID:</strong> {event.data.gameId}
                </div>
              )}
              {event.data.chatId && (
                <div className="mb-1">
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
