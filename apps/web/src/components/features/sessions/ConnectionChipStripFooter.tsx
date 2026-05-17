/**
 * ConnectionChipStripFooter — Wave D.1 v2 component (Issue #735).
 *
 * Mapped from `admin-mockups/design_files/sp4-sessions-index.jsx` (ChipStrip).
 *
 * Footer row of entity chips — max 3 (game / player / chat).
 *
 * Per chip:
 *   - Empty chip (count=0 or empty=true): dashed border, 55% opacity.
 *   - Non-empty chip: entity-colored background (10% alpha), solid border.
 * Colors via inline style (entityHsl token from MeepleCard — avoids Tailwind
 * arbitrary value purging for dynamically derived HSL strings).
 */

import type { ReactElement } from 'react';

import clsx from 'clsx';

import { entityHsl, entityHslText, entityIcon } from '@/components/ui/data-display/meeple-card';
import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

export interface ConnectionChip {
  readonly entity: 'game' | 'player' | 'chat';
  readonly label?: string;
  readonly count?: number;
  readonly empty?: boolean;
}

export interface ConnectionChipStripFooterProps {
  /** Maximum 3 chips displayed. Additional chips are silently dropped. */
  readonly chips: ReadonlyArray<ConnectionChip>;
  readonly className?: string;
}

export function ConnectionChipStripFooter({
  chips,
  className,
}: ConnectionChipStripFooterProps): ReactElement {
  const visibleChips = chips.slice(0, 3);

  return (
    <div
      data-slot="connection-chip-strip-footer"
      className={clsx('flex flex-wrap items-center gap-1', className)}
    >
      {visibleChips.map((chip, idx) => {
        const isEmpty = chip.empty === true || (chip.count !== undefined && chip.count === 0);
        const entityType = chip.entity as MeepleEntityType;
        const icon = entityIcon[entityType];
        const solid = entityHsl(entityType);
        // textColor uses darker variant for AA on light fill bg (#1094 Real-C-B):
        // solid orange (l=38%) on bg-game/0.1 yields 4.03:1 (fail AA 4.5).
        // textColor (l=32%) yields 6.14:1 ✅. See tokens.ts:entityTextOverrides.
        const textColor = entityHslText(entityType);
        const fill = entityHsl(entityType, 0.1);
        const border = entityHsl(entityType, 0.2);
        const dashedBorder = entityHsl(entityType, 0.4);

        return (
          <span
            key={idx}
            data-slot="connection-chip"
            data-entity={chip.entity}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 3,
              paddingInline: 7,
              paddingBlock: 2,
              borderRadius: '9999px',
              background: isEmpty ? 'transparent' : fill,
              border: `1px ${isEmpty ? 'dashed' : 'solid'} ${isEmpty ? dashedBorder : border}`,
              color: isEmpty ? solid : textColor,
              opacity: isEmpty ? 0.55 : 1,
            }}
            className="font-mono text-[9.5px] font-extrabold uppercase tracking-wider"
          >
            <span aria-hidden="true">{icon}</span>
            {chip.label && <span>{chip.label}</span>}
            {chip.count !== undefined && !chip.label && <span>{chip.count}</span>}
          </span>
        );
      })}
    </div>
  );
}
