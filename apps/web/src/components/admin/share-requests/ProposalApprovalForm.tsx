'use client';

/**
 * #5076: approves a private-game proposal with one of the three actions of approve-game-proposal. Variant and merge
 * land on an existing shared game, which the admin picks.
 */

import { useState, type JSX } from 'react';

import { Button } from '@/components/ui/primitives/button';
import { Label } from '@/components/ui/primitives/label';
import { RadioGroup, RadioGroupItem } from '@/components/ui/primitives/radio-group';
import { Textarea } from '@/components/ui/primitives/textarea';
import { useApproveGameProposal } from '@/hooks/queries/useAdminShareRequests';
import type { ProposalApprovalAction } from '@/lib/api/schemas/admin-share-requests.schemas';

import { TargetGamePicker, type TargetGame } from './TargetGamePicker';

type ProposalAction = Exclude<ProposalApprovalAction, 'UpdateCover'>;

const ACTIONS: { value: ProposalAction; label: string; description: string }[] = [
  {
    value: 'ApproveAsNew',
    label: 'Approva come nuovo gioco',
    description: 'Crea un gioco del catalogo dal gioco proposto.',
  },
  {
    value: 'ApproveAsVariant',
    label: 'Approva come variante',
    description: 'Crea un gioco «titolo (Variant)», accanto a un gioco del catalogo che scegli.',
  },
  {
    value: 'MergeKnowledgeBase',
    label: 'Unisci a un gioco esistente',
    description: 'Non crea giochi: i documenti allegati vanno al gioco del catalogo che scegli.',
  },
];

const NOTES_MAX = 1000;

export function ProposalApprovalForm({ shareRequestId }: { shareRequestId: string }): JSX.Element {
  const [action, setAction] = useState<ProposalAction>('ApproveAsNew');
  const [target, setTarget] = useState<TargetGame | null>(null);
  const [notes, setNotes] = useState('');
  const { mutate, isPending } = useApproveGameProposal();

  const needsTarget = action !== 'ApproveAsNew';
  const canSubmit = !isPending && (!needsTarget || target !== null) && notes.length <= NOTES_MAX;

  const submit = () => {
    mutate({
      shareRequestId,
      approvalAction: action,
      // The backend refuses a target for ApproveAsNew, and requires one for the other two.
      targetSharedGameId: needsTarget ? target?.id : null,
      adminNotes: notes.trim() || undefined,
    });
  };

  return (
    <div className="space-y-4">
      <RadioGroup
        value={action}
        onValueChange={value => setAction(value as ProposalAction)}
        aria-label="Come approvare la proposta"
        className="gap-3"
      >
        {ACTIONS.map(a => (
          <label
            key={a.value}
            htmlFor={`proposal-action-${a.value}`}
            className="flex cursor-pointer items-start gap-3 rounded-md border border-border p-3 text-sm"
          >
            <RadioGroupItem id={`proposal-action-${a.value}`} value={a.value} className="mt-0.5" />
            <span>
              <span className="block font-medium">{a.label}</span>
              <span className="block text-muted-foreground">{a.description}</span>
            </span>
          </label>
        ))}
      </RadioGroup>

      {needsTarget && (
        <div className="space-y-2">
          <Label>Gioco del catalogo</Label>
          <TargetGamePicker value={target} onChange={setTarget} />
        </div>
      )}

      <div className="space-y-2">
        <Label htmlFor="proposal-admin-notes">Note dell&apos;admin (facoltative)</Label>
        <Textarea
          id="proposal-admin-notes"
          value={notes}
          onChange={e => setNotes(e.target.value)}
          maxLength={NOTES_MAX}
          rows={3}
        />
      </div>

      <Button onClick={submit} disabled={!canSubmit}>
        {isPending ? 'Approvazione…' : 'Approva la proposta'}
      </Button>
    </div>
  );
}
