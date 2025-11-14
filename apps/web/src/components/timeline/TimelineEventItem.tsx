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
import { cn } from "@/lib/utils";

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
      className={cn(
        "mb-3 transition-all border",
        isSelected ? "border-2 bg-muted" : ""
      )}
      style={isSelected ? { borderColor: typeColor } : {}}
    >
      <CardHeader
        className={cn(
          "p-3 cursor-pointer flex-row items-center gap-3 space-y-0",
          isExpanded && "border-b"
        )}
        onClick={() => onSelect(event.id)}
      >
        {/* Status Icon */}
        <div className="text-xl flex-shrink-0">{statusIcon}</div>

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
            <div className="mb-3">
              <div className="font-semibold mb-1.5 text-slate-500">Messaggio:</div>
              <div className="p-2.5 bg-white border border-gray-300 rounded whitespace-pre-wrap">
                {event.data.message}
              </div>
            </div>
          )}

          {/* Citations */}
          {event.data.citations && event.data.citations.length > 0 && (
            <div className="mb-3">
              <div className="font-semibold mb-1.5 text-slate-500">
                Citazioni ({event.data.citations.length}):
              </div>
              {event.data.citations.map((citation: Snippet, idx: number) => (
                <div
                  key={idx}
                  className="p-2 bg-white border border-gray-300 rounded mb-1.5"
                >
                  <div className="font-medium mb-1">
                    {citation.source}
                    {citation.page !== null && citation.page !== undefined && ` (Pagina ${citation.page})`}
                  </div>
                  <div className="text-slate-500 text-[11px]">{citation.text}</div>
                </div>
              ))}
            </div>
          )}

          {/* Metrics */}
          {event.data.metrics && (
            <div className="mb-3">
              <div className="font-semibold mb-1.5 text-slate-500">Metriche:</div>
              <div className="grid grid-cols-2 gap-2">
                {event.data.metrics.latencyMs !== undefined && (
                  <div className="p-2 bg-white border border-gray-300 rounded">
                    <div className="text-[10px] text-slate-500 mb-0.5">Latenza</div>
                    <div className="font-semibold text-blue-600">
                      {formatDuration(event.data.metrics.latencyMs)}
                    </div>
                  </div>
                )}
                {event.data.metrics.promptTokens !== undefined && (
                  <div className="p-2 bg-white border border-gray-300 rounded">
                    <div className="text-[10px] text-slate-500 mb-0.5">
                      Token Prompt
                    </div>
                    <div className="font-semibold text-blue-600">
                      {event.data.metrics.promptTokens}
                    </div>
                  </div>
                )}
                {event.data.metrics.completionTokens !== undefined && (
                  <div className="p-2 bg-white border border-gray-300 rounded">
                    <div className="text-[10px] text-slate-500 mb-0.5">
                      Token Completamento
                    </div>
                    <div className="font-semibold text-blue-600">
                      {event.data.metrics.completionTokens}
                    </div>
                  </div>
                )}
                {event.data.metrics.totalTokens !== undefined && (
                  <div className="p-2 bg-white border border-gray-300 rounded">
                    <div className="text-[10px] text-slate-500 mb-0.5">
                      Totale Token
                    </div>
                    <div className="font-semibold text-blue-600">
                      {event.data.metrics.totalTokens}
                    </div>
                  </div>
                )}
                {event.data.metrics.confidence !== undefined && (
                  <div className="p-2 bg-white border border-gray-300 rounded">
                    <div className="text-[10px] text-slate-500 mb-0.5">Confidenza</div>
                    <div className="font-semibold text-green-700">
                      {(event.data.metrics.confidence * 100).toFixed(1)}%
                    </div>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Error */}
          {event.data.error && (
            <div className="mb-3">
              <div className="font-semibold mb-1.5 text-red-600">Errore:</div>
              <div className="p-2.5 bg-red-50 border border-red-600 rounded text-red-600">
                {event.data.error}
              </div>
            </div>
          )}

          {/* Technical Details */}
          <details className="mt-2">
            <summary className="cursor-pointer text-slate-500 text-[11px] font-medium py-1">
              Dettagli Tecnici
            </summary>
            <div className="mt-2 p-2 bg-gray-100 rounded font-mono text-[11px]">
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
