// UI-04: Timeline event list component
import { TimelineEvent, TimelineFilters, Snippet } from "@/lib/timeline-types";
import { TimelineEventItem } from "./TimelineEventItem";
import { useState } from "react";

interface TimelineEventListProps {
  events: TimelineEvent[];
  filters: TimelineFilters;
  selectedEventId: string | null;
  onSelectEvent: (eventId: string) => void;
}

export function TimelineEventList({
  events,
  filters,
  selectedEventId,
  onSelectEvent
}: TimelineEventListProps) {
  const [expandedEventIds, setExpandedEventIds] = useState<Set<string>>(new Set());

  const toggleExpand = (eventId: string) => {
    setExpandedEventIds((prev) => {
      const next = new Set(prev);
      if (next.has(eventId)) {
        next.delete(eventId);
      } else {
        next.add(eventId);
      }
      return next;
    });
  };

  // Apply filters
  const filteredEvents = events.filter((event) => {
    // Filter by event type
    if (!filters.eventTypes.has(event.type)) {
      return false;
    }

    // Filter by status
    if (!filters.statuses.has(event.status)) {
      return false;
    }

    // Filter by date range
    if (filters.startDate && event.timestamp < filters.startDate) {
      return false;
    }
    if (filters.endDate && event.timestamp > filters.endDate) {
      return false;
    }

    // Filter by search text
    if (filters.searchText) {
      const searchLower = filters.searchText.toLowerCase();
      const messageMatch = event.data.message?.toLowerCase().includes(searchLower);
      const errorMatch = event.data.error?.toLowerCase().includes(searchLower);
      const citationMatch = event.data.citations?.some((c: Snippet) =>
        c.text.toLowerCase().includes(searchLower) || c.source.toLowerCase().includes(searchLower)
      );

      if (!messageMatch && !errorMatch && !citationMatch) {
        return false;
      }
    }

    return true;
  });

  // Sort by timestamp (most recent first)
  const sortedEvents = [...filteredEvents].sort((a, b) => b.timestamp.getTime() - a.timestamp.getTime());

  if (sortedEvents.length === 0) {
    return (
      <div className="p-12 text-center text-slate-500">
        <div className="text-5xl mb-4">🔍</div>
        <div className="text-base font-medium mb-2">Nessun evento trovato</div>
        <div className="text-[13px]">
          Prova a modificare i filtri o inizia una conversazione per vedere gli eventi.
        </div>
      </div>
    );
  }

  return (
    <div className="p-4">
      {/* Event Count */}
      <div className="mb-4 p-3 bg-blue-50 rounded-lg text-[13px] text-blue-700 font-medium">
        {sortedEvents.length} {sortedEvents.length === 1 ? "evento trovato" : "eventi trovati"}
        {filteredEvents.length !== events.length && ` (${events.length} totali)`}
      </div>

      {/* Events */}
      {sortedEvents.map((event) => (
        <TimelineEventItem
          key={event.id}
          event={event}
          isSelected={selectedEventId === event.id}
          onSelect={onSelectEvent}
          isExpanded={expandedEventIds.has(event.id)}
          onToggleExpand={toggleExpand}
        />
      ))}
    </div>
  );
}
