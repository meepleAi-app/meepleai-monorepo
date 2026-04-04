'use client';

import React, { useState, useCallback } from 'react';

import { Boxes, Plus, X } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { useWidgetSync } from '@/lib/domain-hooks/useWidgetSync';

import { WidgetCard } from './WidgetCard';

interface Resource {
  id: string;
  name: string;
  count: number;
  max?: number;
}

interface ResourceManagerWidgetProps {
  isEnabled: boolean;
  sessionId?: string;
  toolkitId?: string;
  onToggle?: (enabled: boolean) => void;
  onStateChange?: (stateJson: string) => void;
  'data-testid'?: string;
}

/**
 * ResourceManagerWidget — counter grid for tokens, meeples, resources.
 * Issue #5152 — Epic B9.
 */
export function ResourceManagerWidget({
  isEnabled,
  sessionId,
  toolkitId,
  onToggle,
  onStateChange,
  'data-testid': testId,
}: ResourceManagerWidgetProps) {
  const [resources, setResources] = useState<Resource[]>([
    { id: '1', name: 'Meeples', count: 0 },
    { id: '2', name: 'Tokens', count: 0 },
  ]);
  const [newResourceName, setNewResourceName] = useState('');

  const { broadcastState } = useWidgetSync({
    sessionId,
    toolkitId,
    widgetType: 'ResourceManager',
    debounceMs: 500, // Higher debounce for rapid counter clicks
    enabled: !!sessionId && !!toolkitId,
    onRemoteUpdate: (stateJson: string) => {
      try {
        const remote: { resources: Resource[] } = JSON.parse(stateJson);
        setResources(remote.resources);
      } catch {
        // Ignore malformed remote state
      }
    },
  });

  const persist = useCallback(
    (r: Resource[]) => {
      const stateJson = JSON.stringify({ resources: r });
      onStateChange?.(stateJson);
      broadcastState(stateJson);
    },
    [onStateChange, broadcastState]
  );

  const updateCount = useCallback(
    (id: string, delta: number) => {
      setResources(prev => {
        const updated = prev.map(r =>
          r.id === id
            ? {
                ...r,
                count: Math.max(
                  0,
                  r.max !== undefined ? Math.min(r.max, r.count + delta) : r.count + delta
                ),
              }
            : r
        );
        persist(updated);
        return updated;
      });
    },
    [persist]
  );

  const addResource = useCallback(() => {
    const name = newResourceName.trim();
    if (!name) return;
    setResources(prev => {
      const updated = [...prev, { id: crypto.randomUUID(), name, count: 0 }];
      persist(updated);
      return updated;
    });
    setNewResourceName('');
  }, [newResourceName, persist]);

  const removeResource = useCallback(
    (id: string) => {
      setResources(prev => {
        const updated = prev.filter(r => r.id !== id);
        persist(updated);
        return updated;
      });
    },
    [persist]
  );

  const resetAll = useCallback(() => {
    setResources(prev => {
      const updated = prev.map(r => ({ ...r, count: 0 }));
      persist(updated);
      return updated;
    });
  }, [persist]);

  return (
    <WidgetCard
      title="Resource Manager"
      icon={<Boxes className="h-4 w-4 text-green-500" />}
      isEnabled={isEnabled}
      onToggle={onToggle}
      data-testid={testId ?? 'resource-manager-widget'}
    >
      <div className="space-y-3">
        <div className="space-y-2">
          {resources.map(resource => (
            <div
              key={resource.id}
              className="flex items-center justify-between gap-2"
              data-testid={`resource-row-${resource.id}`}
            >
              <span className="min-w-0 flex-1 truncate text-sm font-medium">{resource.name}</span>
              <div className="flex items-center gap-1">
                <Button
                  variant="outline"
                  size="icon"
                  className="h-7 w-7 text-xs"
                  onClick={() => updateCount(resource.id, -1)}
                  aria-label={`Decrease ${resource.name}`}
                >
                  −
                </Button>
                <span
                  className="w-10 text-center text-sm font-bold tabular-nums"
                  aria-label={`${resource.name} count`}
                >
                  {resource.count}
                </span>
                <Button
                  variant="outline"
                  size="icon"
                  className="h-7 w-7 text-xs"
                  onClick={() => updateCount(resource.id, 1)}
                  aria-label={`Increase ${resource.name}`}
                >
                  +
                </Button>
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-7 w-7 opacity-40 hover:opacity-100"
                  onClick={() => removeResource(resource.id)}
                  aria-label={`Remove ${resource.name}`}
                >
                  <X className="h-3 w-3" />
                </Button>
              </div>
            </div>
          ))}
        </div>

        <div className="flex gap-2 border-t pt-2">
          <Input
            value={newResourceName}
            onChange={e => setNewResourceName(e.target.value)}
            placeholder="New resource…"
            className="h-8 text-xs"
            onKeyDown={e => e.key === 'Enter' && addResource()}
            aria-label="New resource name"
          />
          <Button
            variant="outline"
            size="sm"
            onClick={addResource}
            disabled={!newResourceName.trim()}
            aria-label="Add resource"
          >
            <Plus className="h-3 w-3" />
          </Button>
        </div>

        {resources.some(r => r.count > 0) && (
          <Button
            variant="ghost"
            size="sm"
            className="w-full text-xs text-muted-foreground"
            onClick={resetAll}
            aria-label="Reset all resources to zero"
          >
            Reset all to 0
          </Button>
        )}
      </div>
    </WidgetCard>
  );
}
