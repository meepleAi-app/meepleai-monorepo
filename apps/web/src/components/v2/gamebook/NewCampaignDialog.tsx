'use client';

import { useState } from 'react';
import type { ReactElement, ReactNode } from 'react';

import { useMutation } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/overlays/dialog';
import { createCampaign } from '@/lib/api/gamebook-campaigns';

export interface NewCampaignDialogProps {
  gameId: string;
  trigger: ReactNode;
}

export function NewCampaignDialog({ gameId, trigger }: NewCampaignDialogProps): ReactElement {
  const [open, setOpen] = useState(false);
  const [title, setTitle] = useState('');
  const router = useRouter();

  const mutation = useMutation({
    mutationFn: () => createCampaign({ gameId, title: title.trim() }),
    onSuccess: campaign => {
      setOpen(false);
      setTitle('');
      router.push(`/library/games/${gameId}/play/${campaign.id}`);
    },
  });

  const canSubmit = title.trim().length > 0 && !mutation.isPending;

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Nuova campagna libro game</DialogTitle>
          <DialogDescription>
            Crea una nuova campagna persistente. Potrai riprenderla nelle sere successive.
          </DialogDescription>
        </DialogHeader>
        <form
          onSubmit={e => {
            e.preventDefault();
            if (canSubmit) mutation.mutate();
          }}
          className="grid gap-3 py-2"
        >
          <label className="grid gap-1 text-sm">
            <span className="text-muted-foreground">Titolo</span>
            <input
              type="text"
              value={title}
              onChange={e => setTitle(e.target.value)}
              placeholder="Es. Campagna Nanolith #1"
              maxLength={200}
              required
              autoFocus
              className="rounded-md border border-input bg-background px-3 py-1.5"
              data-testid="new-campaign-title-input"
            />
          </label>
          {mutation.isError && (
            <p className="text-sm text-destructive">
              {mutation.error instanceof Error
                ? mutation.error.message
                : 'Errore creazione campagna'}
            </p>
          )}
          <DialogFooter>
            <button
              type="button"
              onClick={() => setOpen(false)}
              className="rounded-md border border-input bg-background px-4 py-1.5 text-sm"
              disabled={mutation.isPending}
            >
              Annulla
            </button>
            <button
              type="submit"
              disabled={!canSubmit}
              className="rounded-md bg-[var(--c-game)] px-4 py-1.5 text-sm font-medium text-white disabled:opacity-50"
              data-testid="new-campaign-submit"
            >
              {mutation.isPending ? 'Creazione…' : 'Crea'}
            </button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
