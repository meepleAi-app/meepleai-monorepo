/**
 * SessionActionButtons - Context-Sensitive Session Actions
 * Issue #4751 - MeepleCard Session Front
 *
 * Renders action buttons that change based on session status:
 * - Setup: Aggiungi Player + Inizia Partita
 * - InProgress: Pausa + Salva
 * - Paused: Riprendi + Salva
 * - Completed: Vedi PlayRecord + Rivincita
 */

'use client';

import React from 'react';

import {
  Pause,
  Play,
  Plus,
  RefreshCw,
  Save,
  ScrollText,
} from 'lucide-react';

import { cn } from '@/lib/utils';

import type { SessionActionHandlers, SessionStatus } from './session-types';

// ============================================================================
// Types
// ============================================================================

export interface SessionActionButtonsProps {
  status: SessionStatus;
  actions: SessionActionHandlers;
  className?: string;
}

interface ActionConfig {
  icon: typeof Play;
  label: string;
  primary?: boolean;
  handler?: () => void;
}

// ============================================================================
// Configuration
// ============================================================================

function getActions(
  status: SessionStatus,
  handlers: SessionActionHandlers,
): ActionConfig[] {
  switch (status) {
    case 'setup':
      return [
        { icon: Plus, label: 'Aggiungi Player', handler: handlers.onAddPlayer },
        { icon: Play, label: 'Inizia Partita', primary: true, handler: handlers.onStart },
      ];
    case 'inProgress':
      return [
        { icon: Pause, label: 'Pausa', handler: handlers.onPause },
        { icon: Save, label: 'Salva', primary: true, handler: handlers.onSave },
      ];
    case 'paused':
      return [
        { icon: Play, label: 'Riprendi', primary: true, handler: handlers.onResume },
        { icon: Save, label: 'Salva', handler: handlers.onSave },
      ];
    case 'completed':
      return [
        { icon: ScrollText, label: 'PlayRecord', handler: handlers.onViewPlayRecord },
        { icon: RefreshCw, label: 'Rivincita', primary: true, handler: handlers.onRematch },
      ];
  }
}

// ============================================================================
// Component
// ============================================================================

export const SessionActionButtons = React.memo(function SessionActionButtons({
  status,
  actions,
  className,
}: SessionActionButtonsProps) {
  const configs = getActions(status, actions);

  return (
    <div
      className={cn('flex items-center gap-1.5', className)}
      data-testid="session-action-buttons"
    >
      {configs.map((config) => {
        const Icon = config.icon;
        return (
          <button
            key={config.label}
            onClick={(e) => {
              e.stopPropagation();
              config.handler?.();
            }}
            disabled={!config.handler}
            className={cn(
              'flex items-center gap-1 px-2.5 py-1.5 rounded-lg',
              'text-[10px] font-semibold',
              'transition-all duration-200',
              'disabled:opacity-40 disabled:cursor-not-allowed',
              config.primary
                ? 'bg-indigo-500 text-white hover:bg-indigo-600 shadow-sm'
                : 'bg-muted text-foreground hover:bg-muted/80',
            )}
            data-testid={`session-action-${config.label.toLowerCase().replace(/\s+/g, '-')}`}
          >
            <Icon className="w-3 h-3" />
            {config.label}
          </button>
        );
      })}
    </div>
  );
});
