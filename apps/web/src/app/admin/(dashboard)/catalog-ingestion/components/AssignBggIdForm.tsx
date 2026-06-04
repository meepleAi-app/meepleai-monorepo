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

export function AssignBggIdForm({ onSubmit, onCancel }: AssignBggIdFormProps) {
  const [sharedGameId, setSharedGameId] = useState('');
  const [bggIdStr, setBggIdStr] = useState('');

  const isValid = sharedGameId.length > 0 && /^\d+$/.test(bggIdStr);

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
          value={sharedGameId}
          onChange={e => setSharedGameId(e.target.value)}
          className="mt-1 w-full rounded border border-border bg-background px-2 py-1.5 text-sm"
        />
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
