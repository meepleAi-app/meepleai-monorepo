'use client';

import { type ReactElement, useEffect, useRef, useState } from 'react';

import type { ParsedWidget, WidgetConfigByType } from '@/lib/session-live/widget-state';

import { WidgetShell } from '../internals/WidgetShell';

import type { ToolkitRendererLabels } from '../labels';

interface Props {
  readonly widget: Extract<ParsedWidget, { type: 'NoteManager' }>;
  readonly collapsed: boolean;
  readonly onHeaderClick: () => void;
  readonly onConfigChange: (next: WidgetConfigByType['NoteManager']) => void;
  readonly labels: ToolkitRendererLabels;
}

const AUTOSAVE_DEBOUNCE_MS = 800;

export function NoteManagerWidget({
  widget,
  collapsed,
  onHeaderClick,
  onConfigChange,
  labels,
}: Props): ReactElement {
  const { config } = widget;
  const [localText, setLocalText] = useState(config.text);
  const [savedAt, setSavedAt] = useState<number | null>(null);
  const timeoutRef = useRef<number | null>(null);

  // External update sync
  useEffect(() => {
    setLocalText(config.text);
  }, [config.text]);

  // Debounced autosave
  useEffect(() => {
    if (localText === config.text) return;
    if (timeoutRef.current != null) {
      window.clearTimeout(timeoutRef.current);
    }
    timeoutRef.current = window.setTimeout(() => {
      onConfigChange({ ...config, text: localText });
      setSavedAt(Date.now());
    }, AUTOSAVE_DEBOUNCE_MS);
    return () => {
      if (timeoutRef.current != null) window.clearTimeout(timeoutRef.current);
    };
  }, [localText, config, onConfigChange]);

  const headerAria = (collapsed ? labels.expandAriaTemplate : labels.collapseAriaTemplate).replace(
    '{name}',
    labels.noteManager.heading
  );

  const isDirty = localText !== config.text;

  return (
    <WidgetShell
      icon="📝"
      title={labels.noteManager.heading}
      typeLabel="NoteManager"
      collapsed={collapsed}
      onHeaderClick={onHeaderClick}
      headerAriaLabel={headerAria}
      entityAccent="kb"
    >
      <div data-slot="widget-note-manager" className="flex flex-col gap-2">
        <textarea
          value={localText}
          onChange={e => setLocalText(e.target.value)}
          aria-label={labels.noteManager.inputAriaLabel}
          rows={3}
          className="w-full resize-y rounded border border-border bg-card px-2 py-1.5 text-sm text-foreground placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        />
        <p
          aria-live="polite"
          className="text-xs text-muted-foreground"
          data-slot="note-manager-status"
        >
          {isDirty
            ? labels.noteManager.savingLabel
            : savedAt != null
              ? labels.noteManager.savedLabel
              : ''}
        </p>
      </div>
    </WidgetShell>
  );
}
