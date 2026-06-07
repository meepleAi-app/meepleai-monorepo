'use client';
import { useState } from 'react';

import { Button } from '@/components/ui/primitives/button';

export interface AssignBggIdFormValues {
  sharedGameId: string;
  bggId: number;
}

interface AssignBggIdFormProps {
  onSubmit: (values: AssignBggIdFormValues) => void;
  onCancel: () => void;
}

const UUID_REGEX = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function AssignBggIdForm({ onSubmit, onCancel }: AssignBggIdFormProps) {
  const [sharedGameId, setSharedGameId] = useState('');
  const [bggIdStr, setBggIdStr] = useState('');

  const sharedGameIdInvalid = sharedGameId.length > 0 && !UUID_REGEX.test(sharedGameId);
  const isValid = UUID_REGEX.test(sharedGameId) && /^\d+$/.test(bggIdStr);

  return (
    <form
      onSubmit={e => {
        e.preventDefault();
        if (!isValid) return;
        onSubmit({ sharedGameId, bggId: Number.parseInt(bggIdStr, 10) });
      }}
      className="space-y-3"
    >
      <label className="block">
        <span className="text-xs font-mono uppercase text-muted-foreground">Shared Game ID</span>
        <input
          aria-label="Shared Game ID"
          aria-invalid={sharedGameIdInvalid}
          aria-describedby={sharedGameIdInvalid ? 'shared-game-id-error' : undefined}
          value={sharedGameId}
          onChange={e => setSharedGameId(e.target.value)}
          className="mt-1 w-full rounded border border-border bg-background px-2 py-1.5 text-sm aria-[invalid=true]:border-event"
        />
        {sharedGameIdInvalid && (
          <span id="shared-game-id-error" className="mt-1 block text-xs text-event">
            ID must be a valid UUID (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx)
          </span>
        )}
      </label>
      <label className="block">
        <span className="text-xs font-mono uppercase text-muted-foreground">BGG ID</span>
        <input
          aria-label="BGG ID"
          value={bggIdStr}
          onChange={e => setBggIdStr(e.target.value)}
          inputMode="numeric"
          className="mt-1 w-full rounded border border-border bg-background px-2 py-1.5 text-sm"
        />
      </label>
      <div className="flex gap-2">
        <Button type="submit" disabled={!isValid}>
          Assign
        </Button>
        <Button type="button" variant="outline" onClick={onCancel}>
          Cancel
        </Button>
      </div>
    </form>
  );
}
