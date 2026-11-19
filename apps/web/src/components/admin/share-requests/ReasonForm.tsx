'use client';

import { useState, type JSX } from 'react';

import { Button } from '@/components/ui/primitives/button';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';

const MIN = 10;
const MAX = 2000;

interface ReasonFormProps {
  id: string;
  label: string;
  submitLabel: string;
  isPending: boolean;
  onSubmit: (text: string) => void;
  destructive?: boolean;
}

/** A required text of 10 to 2000 characters, as the reject and request-changes validators want. */
export function ReasonForm({
  id,
  label,
  submitLabel,
  isPending,
  onSubmit,
  destructive = false,
}: ReasonFormProps): JSX.Element {
  const [text, setText] = useState('');
  const length = text.trim().length;
  const tooShort = length < MIN;

  return (
    <div className="space-y-2">
      <Label htmlFor={id}>{label}</Label>
      <Textarea
        id={id}
        value={text}
        onChange={e => setText(e.target.value)}
        maxLength={MAX}
        rows={3}
        aria-describedby={`${id}-hint`}
      />
      <p id={`${id}-hint`} className="text-xs text-muted-foreground">
        Almeno {MIN} caratteri.
      </p>
      <Button
        variant={destructive ? 'destructive' : 'outline'}
        onClick={() => onSubmit(text.trim())}
        disabled={isPending || tooShort}
      >
        {submitLabel}
      </Button>
    </div>
  );
}
