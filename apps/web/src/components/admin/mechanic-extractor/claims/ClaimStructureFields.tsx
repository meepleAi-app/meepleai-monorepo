'use client';

import React from 'react';

import {
  MechanicClaimKindSchema,
  MechanicRulePrioritySchema,
} from '@/lib/api/schemas/mechanic-analyses.schemas';
import type {
  MechanicClaimKind,
  MechanicClaimStructureDto,
  MechanicRulePriority,
  MechanicTriggerDto,
} from '@/lib/api/schemas/mechanic-analyses.schemas';

export interface ClaimStructureSibling {
  id: string;
  text: string;
}

interface ClaimStructureFieldsProps {
  value: MechanicClaimStructureDto;
  onChange: (next: MechanicClaimStructureDto) => void;
  /** Other claims of the same analysis (the claim being edited is NOT included). */
  siblings: ClaimStructureSibling[];
}

const CONTROL_CLASS =
  'w-full rounded-md border border-border bg-card px-2 py-1 text-sm text-foreground focus:outline-none focus:ring-1 focus:ring-ring';
const LABEL_CLASS = 'block text-xs font-medium text-muted-foreground';

type TriggerField = keyof MechanicTriggerDto;

const TRIGGER_FIELDS: { field: TriggerField; label: string }[] = [
  { field: 'phase', label: 'Trigger phase' },
  { field: 'action', label: 'Trigger action' },
  { field: 'component', label: 'Trigger component' },
];

/**
 * Editable structure of a mechanic claim (kind, priority, overrides, trigger),
 * pre-filled with the values proposed by the extractor.
 */
export function ClaimStructureFields({
  value,
  onChange,
  siblings,
}: ClaimStructureFieldsProps): React.JSX.Element {
  const setTrigger = (field: TriggerField, raw: string): void => {
    const current: MechanicTriggerDto = value.trigger ?? {
      phase: null,
      action: null,
      component: null,
    };
    const next: MechanicTriggerDto = { ...current, [field]: raw === '' ? null : raw };
    const allEmpty = next.phase === null && next.action === null && next.component === null;
    onChange({ ...value, trigger: allEmpty ? null : next });
  };

  const toggleOverride = (id: string, checked: boolean): void => {
    const without = value.overrides.filter(o => o !== id);
    onChange({ ...value, overrides: checked ? [...without, id] : without });
  };

  return (
    <div className="space-y-3" data-testid="claim-structure-fields">
      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className={LABEL_CLASS} htmlFor="claim-structure-kind">
            Kind
          </label>
          <select
            id="claim-structure-kind"
            className={CONTROL_CLASS}
            value={value.kind}
            onChange={e => onChange({ ...value, kind: e.target.value as MechanicClaimKind })}
          >
            {MechanicClaimKindSchema.options.map(k => (
              <option key={k} value={k}>
                {k}
              </option>
            ))}
          </select>
        </div>
        <div>
          <label className={LABEL_CLASS} htmlFor="claim-structure-priority">
            Priority
          </label>
          <select
            id="claim-structure-priority"
            className={CONTROL_CLASS}
            value={value.priority}
            onChange={e => onChange({ ...value, priority: e.target.value as MechanicRulePriority })}
          >
            {MechanicRulePrioritySchema.options.map(p => (
              <option key={p} value={p}>
                {p}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="grid grid-cols-3 gap-2">
        {TRIGGER_FIELDS.map(({ field, label }) => (
          <div key={field}>
            <label className={LABEL_CLASS} htmlFor={`claim-structure-${field}`}>
              {label}
            </label>
            <input
              id={`claim-structure-${field}`}
              type="text"
              className={CONTROL_CLASS}
              value={value.trigger?.[field] ?? ''}
              onChange={e => setTrigger(field, e.target.value)}
            />
          </div>
        ))}
      </div>

      {siblings.length > 0 && (
        <fieldset className="space-y-1">
          <legend className={LABEL_CLASS}>Overrides (claim sovrascritti)</legend>
          <div className="max-h-32 space-y-1 overflow-y-auto">
            {siblings.map(s => (
              <label key={s.id} className="flex items-start gap-2 text-xs text-foreground">
                <input
                  type="checkbox"
                  aria-label={`Overrides: ${s.text}`}
                  checked={value.overrides.includes(s.id)}
                  onChange={e => toggleOverride(s.id, e.target.checked)}
                  className="mt-0.5"
                />
                <span className="break-words">{s.text}</span>
              </label>
            ))}
          </div>
        </fieldset>
      )}
    </div>
  );
}
