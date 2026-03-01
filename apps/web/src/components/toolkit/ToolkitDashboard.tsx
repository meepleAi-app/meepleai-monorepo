'use client';

import React, { useCallback } from 'react';

import { Wrench, Loader2 } from 'lucide-react';

import type { ToolkitDashboardDto, WidgetType } from '@/lib/api/schemas/toolkit.schemas';

import { NoteManagerWidget } from './NoteManagerWidget';
import { RandomGeneratorWidget } from './RandomGeneratorWidget';
import { ResourceManagerWidget } from './ResourceManagerWidget';
import { ScoreTrackerWidget } from './ScoreTrackerWidget';
import { TurnManagerWidget } from './TurnManagerWidget';
import { WhiteboardWidget } from './WhiteboardWidget';

interface ToolkitDashboardProps {
  toolkit: ToolkitDashboardDto | null;
  sessionId?: string;
  players?: Array<{ id: string; name: string }>;
  isLoading?: boolean;
  onWidgetToggle?: (widgetType: WidgetType, enabled: boolean) => Promise<void>;
  onWidgetStateChange?: (widgetType: WidgetType, stateJson: string) => Promise<void>;
  'data-testid'?: string;
}

/**
 * ToolkitDashboard — renders all enabled toolkit widgets in a responsive grid.
 * Orchestrates the 6 widget components (B6–B11) from the backend toolkit configuration.
 * Issue #5128 — Epic B.
 */
export function ToolkitDashboard({
  toolkit,
  players,
  isLoading = false,
  onWidgetToggle,
  onWidgetStateChange,
  'data-testid': testId,
}: ToolkitDashboardProps) {
  const getWidget = useCallback(
    (type: WidgetType) => toolkit?.widgets.find(w => w.type === type),
    [toolkit]
  );

  const isEnabled = useCallback(
    (type: WidgetType) => getWidget(type)?.isEnabled ?? false,
    [getWidget]
  );

  const handleToggle = useCallback(
    (type: WidgetType) => (enabled: boolean) => {
      onWidgetToggle?.(type, enabled);
    },
    [onWidgetToggle]
  );

  const handleStateChange = useCallback(
    (type: WidgetType) => (stateJson: string) => {
      onWidgetStateChange?.(type, stateJson);
    },
    [onWidgetStateChange]
  );

  if (isLoading) {
    return (
      <div
        className="flex min-h-48 items-center justify-center"
        data-testid={testId ?? 'toolkit-dashboard-loading'}
      >
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!toolkit) {
    return (
      <div
        className="flex min-h-48 flex-col items-center justify-center gap-2 text-muted-foreground"
        data-testid={testId ?? 'toolkit-dashboard-empty'}
      >
        <Wrench className="h-10 w-10 opacity-30" />
        <p className="text-sm">No toolkit configured for this game.</p>
      </div>
    );
  }

  const enabledWidgets = toolkit.widgets
    .slice()
    .sort((a, b) => a.displayOrder - b.displayOrder)
    .filter(w => w.isEnabled);

  return (
    <div
      className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3"
      data-testid={testId ?? 'toolkit-dashboard'}
      aria-label="Game Toolkit Dashboard"
    >
      {/* Always render all 6 in displayOrder; visibility controlled by isEnabled prop */}
      {toolkit.widgets
        .slice()
        .sort((a, b) => a.displayOrder - b.displayOrder)
        .map(widget => {
          switch (widget.type) {
            case 'RandomGenerator':
              return (
                <RandomGeneratorWidget
                  key={widget.id}
                  isEnabled={isEnabled('RandomGenerator')}
                  onToggle={handleToggle('RandomGenerator')}
                  onStateChange={handleStateChange('RandomGenerator')}
                  data-testid="toolkit-random-generator"
                />
              );
            case 'TurnManager':
              return (
                <TurnManagerWidget
                  key={widget.id}
                  isEnabled={isEnabled('TurnManager')}
                  players={players}
                  onToggle={handleToggle('TurnManager')}
                  onStateChange={handleStateChange('TurnManager')}
                  data-testid="toolkit-turn-manager"
                />
              );
            case 'ScoreTracker':
              return (
                <ScoreTrackerWidget
                  key={widget.id}
                  isEnabled={isEnabled('ScoreTracker')}
                  players={players}
                  onToggle={handleToggle('ScoreTracker')}
                  onStateChange={handleStateChange('ScoreTracker')}
                  data-testid="toolkit-score-tracker"
                />
              );
            case 'ResourceManager':
              return (
                <ResourceManagerWidget
                  key={widget.id}
                  isEnabled={isEnabled('ResourceManager')}
                  onToggle={handleToggle('ResourceManager')}
                  onStateChange={handleStateChange('ResourceManager')}
                  data-testid="toolkit-resource-manager"
                />
              );
            case 'NoteManager':
              return (
                <NoteManagerWidget
                  key={widget.id}
                  isEnabled={isEnabled('NoteManager')}
                  onToggle={handleToggle('NoteManager')}
                  onStateChange={handleStateChange('NoteManager')}
                  data-testid="toolkit-note-manager"
                />
              );
            case 'Whiteboard':
              return (
                <WhiteboardWidget
                  key={widget.id}
                  isEnabled={isEnabled('Whiteboard')}
                  onToggle={handleToggle('Whiteboard')}
                  onStateChange={handleStateChange('Whiteboard')}
                  data-testid="toolkit-whiteboard"
                />
              );
            default:
              return null;
          }
        })}

      {enabledWidgets.length === 0 && (
        <div
          className="col-span-full flex min-h-24 items-center justify-center text-sm text-muted-foreground"
          data-testid="toolkit-no-widgets"
        >
          All widgets are disabled. Enable widgets to start using the toolkit.
        </div>
      )}
    </div>
  );
}
