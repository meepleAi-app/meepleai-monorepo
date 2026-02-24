'use client';

/**
 * AddEntityLinkModal — C6
 *
 * Modal to create a new EntityLink from the current entity.
 * - Select a link type (chips)
 * - Enter the target entity ID
 * - Optionally select the target entity type (auto-filtered by link type)
 * - Submit creates the link via POST /api/v1/library/entity-links
 *
 * Issue #5162 — Epic C6
 */

import React, { useState, useCallback } from 'react';

import { Loader2, Link as LinkIcon } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/overlays/dialog';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';

import { EntityLinkChip } from './entity-link-chip';
import {
  LINK_TYPE_CONFIG,
  LINK_TYPE_TARGET_ENTITIES,
  LINK_ENTITY_CONFIG,
  type EntityLinkType,
  type LinkEntityType,
} from './entity-link-types';

// ============================================================================
// Types
// ============================================================================

export interface AddEntityLinkModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** The entity creating the link */
  sourceEntityType: LinkEntityType;
  sourceEntityId: string;
  /** Pre-selected link type (e.g. from "Add" button in a group section) */
  defaultLinkType?: EntityLinkType;
  /** Called after a link is successfully created */
  onLinkCreated?: () => void;
}

// All link types available for selection
const ALL_LINK_TYPES = Object.keys(LINK_TYPE_CONFIG) as EntityLinkType[];

// ============================================================================
// Component
// ============================================================================

export function AddEntityLinkModal({
  open,
  onOpenChange,
  sourceEntityType,
  sourceEntityId,
  defaultLinkType,
  onLinkCreated,
}: AddEntityLinkModalProps) {
  const [selectedLinkType, setSelectedLinkType] = useState<EntityLinkType | null>(
    defaultLinkType ?? null
  );
  const [targetEntityType, setTargetEntityType] = useState<LinkEntityType | null>(null);
  const [targetEntityId, setTargetEntityId] = useState('');
  const [note, setNote] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Reset form when modal opens/closes
  const handleOpenChange = useCallback(
    (next: boolean) => {
      if (!next) {
        setSelectedLinkType(defaultLinkType ?? null);
        setTargetEntityType(null);
        setTargetEntityId('');
        setNote('');
        setError(null);
        setSubmitting(false);
      }
      onOpenChange(next);
    },
    [defaultLinkType, onOpenChange]
  );

  // When link type changes, reset target entity type if it's no longer allowed
  const handleLinkTypeSelect = (lt: EntityLinkType) => {
    setSelectedLinkType(lt);
    const allowed = LINK_TYPE_TARGET_ENTITIES[lt];
    if (targetEntityType && !allowed.includes(targetEntityType)) {
      setTargetEntityType(null);
    }
  };

  const allowedTargetTypes = selectedLinkType ? LINK_TYPE_TARGET_ENTITIES[selectedLinkType] : [];

  const canSubmit =
    selectedLinkType !== null &&
    targetEntityType !== null &&
    targetEntityId.trim().length > 0 &&
    !submitting;

  const handleSubmit = async () => {
    if (!canSubmit || !selectedLinkType || !targetEntityType) return;

    setSubmitting(true);
    setError(null);

    try {
      await api.entityLinks.createEntityLink({
        sourceEntityType,
        sourceEntityId,
        targetEntityType,
        targetEntityId: targetEntityId.trim(),
        linkType: selectedLinkType,
        ...(note.trim() ? { metadata: JSON.stringify({ note: note.trim() }) } : {}),
      });

      onLinkCreated?.();
      handleOpenChange(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create link');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2 font-quicksand text-base font-bold">
            <LinkIcon className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
            Add Connection
          </DialogTitle>
        </DialogHeader>

        <div className="flex flex-col gap-5 py-2">
          {/* Step 1: Link type */}
          <section>
            <p className="mb-2 text-[11px] font-bold uppercase tracking-widest text-muted-foreground/60">
              Link type
            </p>
            <div className="flex flex-wrap gap-1.5">
              {ALL_LINK_TYPES.map(lt => (
                <button
                  key={lt}
                  type="button"
                  onClick={() => handleLinkTypeSelect(lt)}
                  className={cn(
                    'rounded-full transition-all duration-150',
                    'focus:outline-none focus:ring-2 focus:ring-slate-300',
                    selectedLinkType === lt
                      ? 'ring-2 ring-offset-1 ring-slate-400'
                      : 'opacity-70 hover:opacity-100'
                  )}
                  aria-pressed={selectedLinkType === lt}
                >
                  <EntityLinkChip linkType={lt} size="md" />
                </button>
              ))}
            </div>
          </section>

          {/* Step 2: Target entity type (shown after link type selected) */}
          {selectedLinkType && (
            <section>
              <p className="mb-2 text-[11px] font-bold uppercase tracking-widest text-muted-foreground/60">
                Target type
              </p>
              <div className="flex flex-wrap gap-1.5">
                {allowedTargetTypes.map(et => {
                  const cfg = LINK_ENTITY_CONFIG[et];
                  return (
                    <button
                      key={et}
                      type="button"
                      onClick={() => setTargetEntityType(et)}
                      className={cn(
                        'inline-flex items-center gap-1 rounded-full px-2.5 py-1',
                        'text-xs font-medium font-nunito transition-all duration-150',
                        'focus:outline-none focus:ring-2 focus:ring-slate-300',
                        targetEntityType === et
                          ? 'ring-2 ring-offset-1 ring-slate-400'
                          : 'opacity-70 hover:opacity-100'
                      )}
                      style={{
                        background: `hsl(${cfg.color} / 0.12)`,
                        border: `1px solid hsl(${cfg.color} / 0.4)`,
                        color: `hsl(${cfg.color})`,
                      }}
                      aria-pressed={targetEntityType === et}
                    >
                      {cfg.label}
                    </button>
                  );
                })}
              </div>
            </section>
          )}

          {/* Step 3: Target ID */}
          {targetEntityType && (
            <section>
              <label
                htmlFor="target-entity-id"
                className="mb-1.5 block text-[11px] font-bold uppercase tracking-widest text-muted-foreground/60"
              >
                {LINK_ENTITY_CONFIG[targetEntityType].label} ID
              </label>
              <input
                id="target-entity-id"
                type="text"
                value={targetEntityId}
                onChange={e => setTargetEntityId(e.target.value)}
                placeholder="Paste entity UUID…"
                className={cn(
                  'w-full rounded-lg border border-border/60 bg-background',
                  'px-3 py-2 text-sm font-mono text-foreground',
                  'placeholder:text-muted-foreground/40',
                  'focus:border-border focus:outline-none focus:ring-2 focus:ring-slate-300',
                  'transition-colors duration-150'
                )}
              />
            </section>
          )}

          {/* Optional note */}
          {targetEntityId.trim().length > 0 && (
            <section>
              <label
                htmlFor="link-note"
                className="mb-1.5 block text-[11px] font-bold uppercase tracking-widest text-muted-foreground/60"
              >
                Note (optional)
              </label>
              <input
                id="link-note"
                type="text"
                value={note}
                onChange={e => setNote(e.target.value)}
                placeholder="Short description…"
                className={cn(
                  'w-full rounded-lg border border-border/60 bg-background',
                  'px-3 py-2 text-sm text-foreground',
                  'placeholder:text-muted-foreground/40',
                  'focus:border-border focus:outline-none focus:ring-2 focus:ring-slate-300',
                  'transition-colors duration-150'
                )}
              />
            </section>
          )}

          {/* Error */}
          {error && (
            <p className="rounded-lg bg-red-50 px-3 py-2 text-xs text-red-600" role="alert">
              {error}
            </p>
          )}
        </div>

        <DialogFooter className="gap-2">
          <button
            type="button"
            onClick={() => handleOpenChange(false)}
            className={cn(
              'rounded-lg border border-border/60 px-4 py-2',
              'text-sm font-medium text-muted-foreground',
              'hover:bg-muted transition-colors duration-150',
              'focus:outline-none focus:ring-2 focus:ring-slate-300'
            )}
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={() => void handleSubmit()}
            disabled={!canSubmit}
            className={cn(
              'flex items-center gap-2 rounded-lg px-4 py-2',
              'bg-primary text-primary-foreground text-sm font-medium',
              'hover:bg-primary/90 transition-colors duration-150',
              'focus:outline-none focus:ring-2 focus:ring-primary/40',
              'disabled:pointer-events-none disabled:opacity-40'
            )}
          >
            {submitting && <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden="true" />}
            Add connection
          </button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default AddEntityLinkModal;
