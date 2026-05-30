/**
 * ChipStrip (play-records) — Task 0.5 (Issue #1488 / Epic #1475 Phase D).
 *
 * Horizontal row of entity-tinted chips ("game · player · chat · event · session")
 * used by `RecordCard` footer and `PlayRecordHeroPodium` ConnectionBar.
 *
 * **Distinct from `features/sessions/ConnectionChipStripFooter`**:
 *   - sessions caps to 3 chips (game/player/chat only)
 *   - play-records permits arbitrary length and adds `event` + `session` tints
 *   - play-records adds `href` support (clickable chip) for entity nav
 *   - dashed border for empty state (`count===0` or `empty: true`) per mockup
 *
 * Pure presentational. No i18n: labels supplied by caller. No store reads.
 *
 * Color tokens come from `meeple-card` (entityHsl + entityHslText) so the
 * play-records strip stays color-consistent with MeepleCard chips elsewhere.
 *
 * @see plan `docs/superpowers/plans/2026-05-29-play-records-reskin.md` Task 0 Step 5
 * @see mockup `admin-mockups/design_files/sp4-play-records-detail.jsx` ConnectionBar
 */
import type { ReactElement } from 'react';

import clsx from 'clsx';

import { entityHsl, entityHslText, entityIcon } from '@/components/ui/data-display/meeple-card';
import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

/** Entities supported by this strip — subset of MeepleEntityType. */
export type ChipEntity = Extract<
  MeepleEntityType,
  'game' | 'player' | 'chat' | 'event' | 'session'
>;

export interface Chip {
  readonly entity: ChipEntity;
  /** Display text — wins over `count` when both supplied. */
  readonly label?: string;
  /** Numeric counter — rendered as the chip text when `label` is absent.
   *  `count === 0` triggers empty styling (dashed border, dim icon). */
  readonly count?: number;
  /** Explicit empty flag — overrides count-based detection (e.g. "No chat"
   *  with a label but still empty). */
  readonly empty?: boolean;
  /** Optional nav target — renders the chip as `<a>` instead of `<span>`. */
  readonly href?: string;
}

export interface ChipStripProps {
  readonly chips: ReadonlyArray<Chip>;
  readonly className?: string;
}

const CHIP_BASE =
  'inline-flex items-center gap-1 rounded-full px-2 py-0.5 font-mono text-[10px] font-extrabold uppercase tracking-wider whitespace-nowrap';

function isEmptyChip(chip: Chip): boolean {
  if (chip.empty === true) return true;
  if (chip.count !== undefined && chip.count === 0) return true;
  return false;
}

function ChipItem({ chip }: { readonly chip: Chip }): ReactElement {
  const empty = isEmptyChip(chip);
  const icon = entityIcon[chip.entity];
  const textColor = entityHslText(chip.entity);
  const fill = entityHsl(chip.entity, 0.1);
  const border = entityHsl(chip.entity, 0.22);
  const dashedBorder = entityHsl(chip.entity, 0.4);

  const style: React.CSSProperties = {
    background: empty ? 'transparent' : fill,
    border: `1px ${empty ? 'dashed' : 'solid'} ${empty ? dashedBorder : border}`,
    color: textColor,
  };

  const content = (
    <>
      <span aria-hidden="true" style={empty ? { opacity: 0.55 } : undefined}>
        {icon}
      </span>
      {chip.label !== undefined ? (
        <span>{chip.label}</span>
      ) : chip.count !== undefined ? (
        <span>{chip.count}</span>
      ) : null}
    </>
  );

  const commonProps = {
    'data-slot': 'chip',
    'data-entity': chip.entity,
    'data-empty': empty ? 'true' : 'false',
    style,
    className: CHIP_BASE,
  } as const;

  return chip.href !== undefined ? (
    <a {...commonProps} href={chip.href}>
      {content}
    </a>
  ) : (
    <span {...commonProps}>{content}</span>
  );
}

export function ChipStrip({ chips, className }: ChipStripProps): ReactElement {
  return (
    <div data-slot="chip-strip" className={clsx('flex flex-wrap items-center gap-1', className)}>
      {chips.map((chip, idx) => (
        <ChipItem key={`${chip.entity}-${idx}`} chip={chip} />
      ))}
    </div>
  );
}
