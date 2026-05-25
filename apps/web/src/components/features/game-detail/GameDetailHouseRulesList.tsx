/**
 * GameDetailHouseRulesList - Wave C follow-up (Issue #1464)
 *
 * Interactive presentational CRUD list of house rules for a game's memory. Receives
 * rules + i18n labels + mutation callbacks. State for UI affordances (add form,
 * inline edit, delete confirm) lives inside; data fetching/mutations are the caller's
 * job. Mockup: `admin-mockups/design_files/sp4-game-detail.jsx:423-475`.
 *
 * Note on scope (per spec-panel decision): the BE HouseRule was extended in this issue
 * to carry a stable Guid Id; PATCH/DELETE endpoints were added (#1464). The mockup's
 * enable/disable toggle is NOT supported by the domain — see PILOT_GAP_REPORT § 3.5 #4.
 */

'use client';

import { useState, type ReactElement } from 'react';

import clsx from 'clsx';

import { ConfirmModal } from '@/components/ui/dialogs/confirm-modal';
import type { HouseRuleDto } from '@/lib/api/clients/agentMemoryClient';

export interface GameDetailHouseRulesListLabels {
  readonly title: string;
  readonly addCta: string;
  readonly addPlaceholder: string;
  readonly addSubmit: string;
  readonly editLabel: string;
  readonly editSubmit: string;
  readonly cancel: string;
  readonly deleteLabel: string;
  readonly deleteConfirmTitle: string;
  readonly deleteConfirmMessage: string;
  readonly deleteConfirm: string;
  readonly empty: string;
}

export interface GameDetailHouseRulesListProps {
  readonly rules: ReadonlyArray<HouseRuleDto>;
  readonly labels: GameDetailHouseRulesListLabels;
  readonly onAdd: (description: string) => void;
  readonly onUpdate: (ruleId: string, description: string) => void;
  readonly onDelete: (ruleId: string) => void;
  readonly className?: string;
}

export function GameDetailHouseRulesList(props: GameDetailHouseRulesListProps): ReactElement {
  const { rules, labels, onAdd, onUpdate, onDelete, className } = props;

  const [addOpen, setAddOpen] = useState(false);
  const [addValue, setAddValue] = useState('');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editValue, setEditValue] = useState('');
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);

  const trimmedAdd = addValue.trim();
  const canSubmitAdd = trimmedAdd.length > 0;
  const trimmedEdit = editValue.trim();
  const canSubmitEdit = trimmedEdit.length > 0;

  function handleSubmitAdd(): void {
    if (!canSubmitAdd) return;
    onAdd(trimmedAdd);
    setAddValue('');
    setAddOpen(false);
  }

  function handleStartEdit(rule: HouseRuleDto): void {
    setEditingId(rule.id);
    setEditValue(rule.description);
  }

  function handleSubmitEdit(): void {
    if (editingId == null || !canSubmitEdit) return;
    onUpdate(editingId, trimmedEdit);
    setEditingId(null);
    setEditValue('');
  }

  function handleCancelEdit(): void {
    setEditingId(null);
    setEditValue('');
  }

  function handleConfirmDelete(): void {
    if (confirmDeleteId == null) return;
    onDelete(confirmDeleteId);
    setConfirmDeleteId(null);
  }

  return (
    <section
      data-slot="game-detail-house-rules-list"
      className={clsx('rounded-2xl border border-border bg-card p-[18px] shadow-sm', className)}
    >
      <div className="mb-3.5 flex items-center justify-between gap-3">
        <h3 className="font-display text-[15px] font-extrabold text-foreground">{labels.title}</h3>
        {!addOpen && (
          <button
            type="button"
            onClick={() => setAddOpen(true)}
            data-slot="game-detail-house-rules-add-cta"
            className="rounded-md border border-border bg-transparent px-2.5 py-1 font-display text-[12px] font-extrabold text-foreground transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            {labels.addCta}
          </button>
        )}
      </div>

      {addOpen && (
        <div
          data-slot="game-detail-house-rules-add-form"
          className="mb-3 flex flex-col gap-2 rounded-xl border border-border bg-background p-3"
        >
          <textarea
            value={addValue}
            onChange={e => setAddValue(e.target.value)}
            placeholder={labels.addPlaceholder}
            // BE caps description at 2000 chars (AddHouseRuleCommandValidator).
            maxLength={2000}
            autoFocus
            data-slot="game-detail-house-rules-add-input"
            className="min-h-[64px] resize-y rounded-md border border-border bg-background p-2 font-mono text-[12px] text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          />
          <div className="flex items-center justify-end gap-2">
            <button
              type="button"
              onClick={() => {
                setAddOpen(false);
                setAddValue('');
              }}
              className="rounded-md border border-border bg-transparent px-2.5 py-1 font-display text-[12px] font-bold text-muted-foreground transition-colors hover:bg-muted"
            >
              {labels.cancel}
            </button>
            <button
              type="button"
              onClick={handleSubmitAdd}
              disabled={!canSubmitAdd}
              data-slot="game-detail-house-rules-add-submit"
              className="rounded-md border-none bg-primary px-3 py-1 font-display text-[12px] font-extrabold text-primary-foreground shadow-sm transition-shadow hover:shadow-md disabled:cursor-not-allowed disabled:opacity-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              {labels.addSubmit}
            </button>
          </div>
        </div>
      )}

      {rules.length === 0 && !addOpen ? (
        <p
          data-slot="game-detail-house-rules-empty"
          className="py-6 text-center font-mono text-[11px] text-muted-foreground"
        >
          {labels.empty}
        </p>
      ) : (
        <ul role="list" aria-label={labels.title} className="flex flex-col">
          {rules.map((rule, i) => {
            const isEditing = editingId === rule.id;
            return (
              <li
                key={rule.id}
                role="listitem"
                data-slot="game-detail-house-rules-row"
                data-rule-id={rule.id}
                className={clsx(
                  'flex flex-col gap-2 py-2.5',
                  i < rules.length - 1 && 'border-b border-border'
                )}
              >
                {isEditing ? (
                  <>
                    <textarea
                      value={editValue}
                      onChange={e => setEditValue(e.target.value)}
                      maxLength={2000}
                      autoFocus
                      data-slot="game-detail-house-rules-edit-input"
                      className="min-h-[56px] resize-y rounded-md border border-border bg-background p-2 font-mono text-[12px] text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                    />
                    <div className="flex items-center justify-end gap-2">
                      <button
                        type="button"
                        onClick={handleCancelEdit}
                        className="rounded-md border border-border bg-transparent px-2.5 py-1 font-display text-[11px] font-bold text-muted-foreground transition-colors hover:bg-muted"
                      >
                        {labels.cancel}
                      </button>
                      <button
                        type="button"
                        onClick={handleSubmitEdit}
                        disabled={!canSubmitEdit}
                        data-slot="game-detail-house-rules-edit-submit"
                        className="rounded-md border-none bg-primary px-2.5 py-1 font-display text-[11px] font-extrabold text-primary-foreground transition-shadow hover:shadow-sm disabled:cursor-not-allowed disabled:opacity-50"
                      >
                        {labels.editSubmit}
                      </button>
                    </div>
                  </>
                ) : (
                  <div className="flex items-start justify-between gap-3">
                    <p className="flex-1 font-mono text-[12px] text-foreground">
                      {rule.description}
                    </p>
                    <div className="flex flex-shrink-0 items-center gap-1.5">
                      <button
                        type="button"
                        onClick={() => handleStartEdit(rule)}
                        aria-label={labels.editLabel}
                        data-slot="game-detail-house-rules-edit-cta"
                        className="rounded-md border-none bg-transparent px-2 py-1 font-mono text-[11px] font-extrabold text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
                      >
                        ✏️
                      </button>
                      <button
                        type="button"
                        onClick={() => setConfirmDeleteId(rule.id)}
                        aria-label={labels.deleteLabel}
                        data-slot="game-detail-house-rules-delete-cta"
                        className="rounded-md border-none bg-transparent px-2 py-1 font-mono text-[11px] font-extrabold text-muted-foreground transition-colors hover:bg-destructive/10 hover:text-destructive"
                      >
                        🗑️
                      </button>
                    </div>
                  </div>
                )}
              </li>
            );
          })}
        </ul>
      )}

      <ConfirmModal
        open={confirmDeleteId !== null}
        title={labels.deleteConfirmTitle}
        message={labels.deleteConfirmMessage}
        confirmLabel={labels.deleteConfirm}
        cancelLabel={labels.cancel}
        variant="destructive"
        onConfirm={handleConfirmDelete}
        onCancel={() => setConfirmDeleteId(null)}
      />
    </section>
  );
}
