/**
 * AgentSwitchDialog - Agent change confirmation banner
 *
 * Shown when user requests to switch the agent type on an active thread.
 * Displays confirmation with agent name and confirm/cancel buttons.
 */

'use client';

import React from 'react';

// ============================================================================
// Types
// ============================================================================

export interface AgentSwitchDialogProps {
  pendingAgentName: string;
  onConfirm: () => void;
  onCancel: () => void;
}

// ============================================================================
// Component
// ============================================================================

export function AgentSwitchDialog({
  pendingAgentName,
  onConfirm,
  onCancel,
}: AgentSwitchDialogProps) {
  return (
    <div
      className="mx-4 mt-2 p-3 bg-amber-50 dark:bg-amber-500/10 rounded-lg border border-amber-200 dark:border-amber-500/20 flex items-center justify-between gap-3"
      role="alertdialog"
      data-testid="agent-switch-confirm"
    >
      <p className="text-sm font-nunito text-amber-900 dark:text-amber-200">
        Vuoi cambiare agente a <strong>{pendingAgentName}</strong>? La cronologia viene mantenuta.
      </p>
      <div className="flex gap-2 flex-shrink-0">
        <button
          onClick={onConfirm}
          className="px-3 py-1.5 text-xs font-medium bg-amber-500 text-white rounded-md hover:bg-amber-600 transition-colors"
          data-testid="agent-switch-confirm-btn"
        >
          Conferma
        </button>
        <button
          onClick={onCancel}
          className="px-3 py-1.5 text-xs font-medium bg-white dark:bg-card text-muted-foreground rounded-md border hover:bg-muted/50 transition-colors"
          data-testid="agent-switch-cancel-btn"
        >
          Annulla
        </button>
      </div>
    </div>
  );
}
