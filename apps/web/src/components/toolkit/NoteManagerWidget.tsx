'use client';

import React, { useState, useCallback } from 'react';

import { FileText, Lock, Globe } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Textarea } from '@/components/ui/primitives/textarea';
import { useWidgetSync } from '@/lib/hooks/useWidgetSync';

import { WidgetCard } from './WidgetCard';

interface NoteManagerWidgetProps {
  isEnabled: boolean;
  sessionId?: string;
  toolkitId?: string;
  onToggle?: (enabled: boolean) => void;
  onStateChange?: (stateJson: string) => void;
  'data-testid'?: string;
}

/**
 * NoteManagerWidget — private and public notes per player.
 * Issue #5153 — Epic B10.
 */
export function NoteManagerWidget({
  isEnabled,
  sessionId,
  toolkitId,
  onToggle,
  onStateChange,
  'data-testid': testId,
}: NoteManagerWidgetProps) {
  const [activeTab, setActiveTab] = useState<'private' | 'public'>('private');
  const [privateNote, setPrivateNote] = useState('');
  const [publicNote, setPublicNote] = useState('');
  const [saved, setSaved] = useState(false);

  const { broadcastState } = useWidgetSync({
    sessionId,
    toolkitId,
    widgetType: 'NoteManager',
    enabled: !!sessionId && !!toolkitId,
    onRemoteUpdate: (stateJson: string) => {
      try {
        // Only sync public notes — private notes stay local
        const remote: { publicNote: string } = JSON.parse(stateJson);
        setPublicNote(remote.publicNote);
      } catch {
        // Ignore malformed remote state
      }
    },
  });

  const persist = useCallback(
    (priv: string, pub: string) => {
      onStateChange?.(JSON.stringify({ privateNote: priv, publicNote: pub }));
      // Only broadcast public note content
      broadcastState(JSON.stringify({ publicNote: pub }));
      setSaved(true);
      setTimeout(() => setSaved(false), 1500);
    },
    [onStateChange, broadcastState]
  );

  const save = useCallback(
    () => persist(privateNote, publicNote),
    [persist, privateNote, publicNote]
  );

  return (
    <WidgetCard
      title="Note Manager"
      icon={<FileText className="h-4 w-4 text-purple-500" />}
      isEnabled={isEnabled}
      onToggle={onToggle}
      data-testid={testId ?? 'note-manager-widget'}
    >
      {/* Tab list */}
      <div role="tablist" className="grid h-8 grid-cols-2 rounded-md bg-muted p-0.5">
        <button
          role="tab"
          aria-selected={activeTab === 'private'}
          onClick={() => setActiveTab('private')}
          className={`flex items-center justify-center gap-1 rounded text-xs transition-colors ${
            activeTab === 'private'
              ? 'bg-background text-foreground shadow-sm'
              : 'text-muted-foreground hover:text-foreground'
          }`}
        >
          <Lock className="h-3 w-3" />
          Private
        </button>
        <button
          role="tab"
          aria-selected={activeTab === 'public'}
          onClick={() => setActiveTab('public')}
          className={`flex items-center justify-center gap-1 rounded text-xs transition-colors ${
            activeTab === 'public'
              ? 'bg-background text-foreground shadow-sm'
              : 'text-muted-foreground hover:text-foreground'
          }`}
        >
          <Globe className="h-3 w-3" />
          Public
        </button>
      </div>

      {/* Tab content */}
      <div className="mt-2">
        {activeTab === 'private' && (
          <Textarea
            value={privateNote}
            onChange={e => setPrivateNote(e.target.value)}
            placeholder="Your private notes (only you can see these)…"
            className="min-h-[80px] resize-none text-xs"
            aria-label="Private notes"
          />
        )}
        {activeTab === 'public' && (
          <Textarea
            value={publicNote}
            onChange={e => setPublicNote(e.target.value)}
            placeholder="Shared notes visible to all players…"
            className="min-h-[80px] resize-none text-xs"
            aria-label="Public notes"
          />
        )}
      </div>

      <Button size="sm" className="mt-2 w-full" onClick={save} disabled={saved}>
        {saved ? 'Saved ✓' : 'Save Notes'}
      </Button>
    </WidgetCard>
  );
}
