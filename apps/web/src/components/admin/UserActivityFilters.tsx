/* eslint-disable security/detect-object-injection -- Safe typed Record access with event type string keys */
/**
 * UserActivityFilters Component - Issue #911
 *
 * Filter panel for user activity timeline.
 * Features:
 * - Multiselect for event types
 * - Severity level filters
 * - Active filter chips
 * - Reset all filters button
 */

import { XIcon } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Label } from '@/components/ui/primitives/label';

export interface UserActivityFiltersState {
  eventTypes: Set<string>;
  severities: Set<string>;
}

export interface UserActivityFiltersProps {
  filters: UserActivityFiltersState;
  onFiltersChange: (filters: UserActivityFiltersState) => void;
  availableEventTypes: string[];
  className?: string;
}

const SEVERITY_LEVELS = ['Info', 'Warning', 'Error', 'Critical'] as const;

const eventTypeLabels: Record<string, string> = {
  UserRegistered: 'Registrazione Utente',
  UserLogin: 'Login Utente',
  PdfUploaded: 'PDF Caricato',
  PdfProcessed: 'PDF Processato',
  AlertCreated: 'Allarme Creato',
  AlertResolved: 'Allarme Risolto',
  GameAdded: 'Gioco Aggiunto',
  ConfigurationChanged: 'Configurazione Modificata',
  ErrorOccurred: 'Errore Avvenuto',
  SystemEvent: 'Evento di Sistema',
};

export function UserActivityFilters({
  filters,
  onFiltersChange,
  availableEventTypes,
  className,
}: UserActivityFiltersProps) {
  const toggleEventType = (eventType: string) => {
    const newTypes = new Set(filters.eventTypes);
    if (newTypes.has(eventType)) {
      newTypes.delete(eventType);
    } else {
      newTypes.add(eventType);
    }
    onFiltersChange({ ...filters, eventTypes: newTypes });
  };

  const toggleSeverity = (severity: string) => {
    const newSeverities = new Set(filters.severities);
    if (newSeverities.has(severity)) {
      newSeverities.delete(severity);
    } else {
      newSeverities.add(severity);
    }
    onFiltersChange({ ...filters, severities: newSeverities });
  };

  const selectAllEventTypes = () => {
    onFiltersChange({
      ...filters,
      eventTypes: new Set(availableEventTypes),
    });
  };

  const deselectAllEventTypes = () => {
    onFiltersChange({ ...filters, eventTypes: new Set() });
  };

  const selectAllSeverities = () => {
    onFiltersChange({
      ...filters,
      severities: new Set(SEVERITY_LEVELS),
    });
  };

  const deselectAllSeverities = () => {
    onFiltersChange({ ...filters, severities: new Set() });
  };

  const resetAllFilters = () => {
    onFiltersChange({
      eventTypes: new Set(availableEventTypes),
      severities: new Set(SEVERITY_LEVELS),
    });
  };

  const hasActiveFilters =
    filters.eventTypes.size < availableEventTypes.length ||
    filters.severities.size < SEVERITY_LEVELS.length;

  const activeFilterCount =
    availableEventTypes.length -
    filters.eventTypes.size +
    (SEVERITY_LEVELS.length - filters.severities.size);

  return (
    <div className={className}>
      {/* Active Filters Chips */}
      {hasActiveFilters && (
        <div className="mb-4 flex flex-wrap items-center gap-2">
          <span className="text-sm font-medium text-foreground">Filtri attivi:</span>
          {Array.from(filters.eventTypes).length < availableEventTypes.length &&
            availableEventTypes
              .filter(type => !filters.eventTypes.has(type))
              .map(type => (
                <Badge key={type} variant="secondary" className="flex items-center gap-1">
                  <span className="text-xs">Escluso: {eventTypeLabels[type] || type}</span>
                  <button
                    onClick={() => toggleEventType(type)}
                    className="ml-1 hover:bg-muted dark:hover:bg-muted/70 rounded-full p-0.5"
                    aria-label={`Includi ${eventTypeLabels[type] || type}`}
                  >
                    <XIcon className="h-3 w-3" aria-hidden="true" />
                  </button>
                </Badge>
              ))}
          {Array.from(filters.severities).length < SEVERITY_LEVELS.length &&
            SEVERITY_LEVELS.filter(sev => !filters.severities.has(sev)).map(sev => (
              <Badge key={sev} variant="secondary" className="flex items-center gap-1">
                <span className="text-xs">Escluso: {sev}</span>
                <button
                  onClick={() => toggleSeverity(sev)}
                  className="ml-1 hover:bg-gray-300 rounded-full p-0.5"
                  aria-label={`Includi severity ${sev}`}
                >
                  <XIcon className="h-3 w-3" aria-hidden="true" />
                </button>
              </Badge>
            ))}
          <Button variant="ghost" size="sm" onClick={resetAllFilters} className="text-xs">
            Ripristina tutti
          </Button>
        </div>
      )}

      <div className="grid gap-6 md:grid-cols-2">
        {/* Event Types */}
        <div>
          <div className="flex items-center justify-between mb-3">
            <Label className="text-sm font-semibold">Tipo Evento</Label>
            <div className="flex gap-2">
              <Button
                variant="ghost"
                size="sm"
                onClick={selectAllEventTypes}
                className="text-xs h-7"
              >
                Tutti
              </Button>
              <Button
                variant="ghost"
                size="sm"
                onClick={deselectAllEventTypes}
                className="text-xs h-7"
              >
                Nessuno
              </Button>
            </div>
          </div>
          <div className="space-y-2">
            {availableEventTypes.map(eventType => (
              <div key={eventType} className="flex items-center gap-2">
                <Checkbox
                  id={`event-${eventType}`}
                  checked={filters.eventTypes.has(eventType)}
                  onCheckedChange={() => toggleEventType(eventType)}
                />
                <Label
                  htmlFor={`event-${eventType}`}
                  className="text-sm font-normal cursor-pointer"
                >
                  {eventTypeLabels[eventType] || eventType}
                </Label>
              </div>
            ))}
          </div>
        </div>

        {/* Severity Levels */}
        <div>
          <div className="flex items-center justify-between mb-3">
            <Label className="text-sm font-semibold">Livello Severità</Label>
            <div className="flex gap-2">
              <Button
                variant="ghost"
                size="sm"
                onClick={selectAllSeverities}
                className="text-xs h-7"
              >
                Tutti
              </Button>
              <Button
                variant="ghost"
                size="sm"
                onClick={deselectAllSeverities}
                className="text-xs h-7"
              >
                Nessuno
              </Button>
            </div>
          </div>
          <div className="space-y-2">
            {SEVERITY_LEVELS.map(severity => (
              <div key={severity} className="flex items-center gap-2">
                <Checkbox
                  id={`severity-${severity}`}
                  checked={filters.severities.has(severity)}
                  onCheckedChange={() => toggleSeverity(severity)}
                />
                <Label
                  htmlFor={`severity-${severity}`}
                  className="text-sm font-normal cursor-pointer"
                >
                  {severity}
                </Label>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Summary */}
      {hasActiveFilters && (
        <div className="mt-4 pt-4 border-t border-border/50 dark:border-border/30">
          <p className="text-xs text-muted-foreground">
            {activeFilterCount} {activeFilterCount === 1 ? 'filtro attivo' : 'filtri attivi'}
          </p>
        </div>
      )}
    </div>
  );
}
