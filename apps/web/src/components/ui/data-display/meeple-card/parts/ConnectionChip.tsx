'use client';

import { useState, type CSSProperties } from 'react';

import { Plus } from 'lucide-react';
import Link from 'next/link';

import { entityLabel } from '../tokens';
import { entityTokens } from '../tokens';
import { ConnectionChipPopover } from './ConnectionChipPopover';
import { ENTITY_ICON_SIZE, ENTITY_ICON_STROKE, entityIcons } from './entity-icons';

import type { ConnectionChipProps } from '../types';

const CHIP_PX = { sm: 22, md: 28 } as const;
const BADGE_PX = { sm: 14, md: 16 } as const;
const PLUS_PX = { sm: 12, md: 14 } as const;

function formatCount(count: number): string {
  return count > 99 ? '99+' : String(count);
}

function formatCountForLabel(count: number, label: string): string {
  if (count > 99) return `99 or more ${label}`;
  return `${count} ${label}`;
}

export function ConnectionChip({
  entityType,
  count = 0,
  items,
  size = 'md',
  showLabel,
  label,
  onCreate,
  createLabel,
  onClick,
  href,
  colorOverride,
  disabled = false,
  loading = false,
  iconOverride,
}: ConnectionChipProps) {
  const [popoverOpen, setPopoverOpen] = useState(false);
  const tokens = entityTokens(entityType);
  const color = colorOverride ?? tokens.solid;
  const Icon = entityIcons[entityType];
  const chipPx = CHIP_PX[size];
  const iconPx = ENTITY_ICON_SIZE[size];
  const badgePx = BADGE_PX[size];
  const plusPx = PLUS_PX[size];
  const labelEffective = label ?? entityLabel[entityType];
  const showLabelEffective = showLabel ?? size === 'md';

  const hasCount = count > 0;
  const isEmpty = count === 0;
  const hasCreate = onCreate !== undefined;
  const hasItems = (items?.length ?? 0) > 0;
  const hasOnClick = onClick !== undefined;
  const isInteractive = !disabled && !loading && (hasItems || hasCreate || hasOnClick || !!href);

  if (loading) {
    return (
      <span
        data-testid="connection-chip-loading"
        className="inline-flex animate-pulse rounded-full"
        style={{
          width: chipPx,
          height: chipPx,
          background: tokens.muted,
        }}
      />
    );
  }

  const chipFaceStyle: CSSProperties = {
    width: chipPx,
    height: chipPx,
    background: isEmpty ? tokens.muted : tokens.fill,
    border: `1px ${isEmpty ? 'dashed' : 'solid'} ${isEmpty ? tokens.dashed : tokens.border}`,
    color,
    opacity: isEmpty ? (hasCreate ? 0.85 : 0.6) : 1,
    ['--mc-chip-color' as string]: color,
    ['--mc-chip-glow' as string]: tokens.glow,
    ['--mc-chip-shadow' as string]: tokens.shadow,
    ['--mc-chip-hover-bg' as string]: tokens.hover,
  };

  const chipInner = (
    <span
      className="relative inline-flex items-center justify-center rounded-full transition-all duration-200 group-hover/chip:scale-[1.08] group-hover/chip:bg-[var(--mc-chip-hover-bg)] group-hover/chip:shadow-[0_0_0_4px_var(--mc-chip-glow),0_4px_12px_var(--mc-chip-shadow)] motion-reduce:group-hover/chip:scale-100"
      style={chipFaceStyle}
    >
      {iconOverride ?? (
        <Icon
          size={iconPx}
          strokeWidth={ENTITY_ICON_STROKE}
          aria-hidden="true"
          style={{ opacity: isEmpty ? (hasCreate ? 0.7 : 0.45) : 1 }}
        />
      )}

      {hasCount && (
        <span
          data-testid="connection-chip-badge"
          className="absolute flex items-center justify-center rounded-full font-semibold text-white shadow-sm [font-feature-settings:'tnum']"
          style={{
            top: -4,
            right: -4,
            height: badgePx,
            minWidth: badgePx,
            padding: '0 3px',
            fontSize: size === 'sm' ? 9 : 10,
            lineHeight: 1,
            background: color,
            boxShadow: '0 0 0 2px var(--mc-bg-card, #fff)',
          }}
        >
          {formatCount(count)}
        </span>
      )}

      {isEmpty && hasCreate && (
        <span
          data-testid="connection-chip-plus"
          aria-hidden="true"
          className="absolute flex items-center justify-center rounded-full text-white shadow-sm"
          style={{
            bottom: -3,
            right: -3,
            height: plusPx,
            width: plusPx,
            background: color,
          }}
        >
          <Plus size={plusPx - 4} strokeWidth={3} />
        </span>
      )}
    </span>
  );

  const labelEl = showLabelEffective ? (
    <span className="mt-0.5 text-[9px] font-semibold uppercase tracking-wide text-[var(--mc-text-muted)] group-hover/chip:text-[var(--mc-text-secondary)]">
      {labelEffective}
    </span>
  ) : null;

  const handleActivate = () => {
    if (!isInteractive) return;
    if (hasItems) {
      setPopoverOpen(true);
    } else if (hasCreate) {
      onCreate?.();
    } else if (hasOnClick) {
      onClick?.();
    }
    // href without items/create/onClick is rendered as <Link>, no click handler needed here.
  };

  const ariaLabel = hasCount
    ? formatCountForLabel(count, labelEffective)
    : hasCreate
      ? (createLabel ?? `Aggiungi ${labelEffective}`)
      : labelEffective;

  const rootClassName = `group/chip relative inline-flex flex-col items-center outline-none focus-visible:ring-2 focus-visible:ring-offset-1 ${
    disabled ? 'cursor-not-allowed opacity-40' : isInteractive ? 'cursor-pointer' : 'cursor-default'
  }`;
  const rootStyle = { ['--tw-ring-color' as string]: tokens.solid } as CSSProperties;

  // Render as <Link> when href is provided, no popover, no create handler, and not disabled.
  // onCreate has precedence over href. onClick combined with href renders as <Link> with
  // e.preventDefault() so left-click invokes onClick while middle-click/⌘-click preserve
  // the browser's "open in new tab" semantics.
  if (href && !hasItems && !hasCreate && !disabled) {
    if (hasOnClick) {
      return (
        <Link
          href={href}
          aria-label={ariaLabel}
          className={rootClassName}
          style={rootStyle}
          onClick={e => {
            e.preventDefault();
            onClick?.();
          }}
        >
          {chipInner}
          {labelEl}
        </Link>
      );
    }
    return (
      <Link href={href} aria-label={ariaLabel} className={rootClassName} style={rootStyle}>
        {chipInner}
        {labelEl}
      </Link>
    );
  }

  const buttonEl = (
    <button
      type="button"
      aria-label={ariaLabel}
      disabled={disabled}
      onClick={handleActivate}
      className={rootClassName}
      style={rootStyle}
    >
      {chipInner}
      {labelEl}
    </button>
  );

  if (hasItems && !disabled) {
    return (
      <ConnectionChipPopover
        open={popoverOpen}
        onOpenChange={setPopoverOpen}
        items={items ?? []}
        onCreate={onCreate}
        createLabel={createLabel}
        entityType={entityType}
        iconOverride={iconOverride}
      >
        {buttonEl}
      </ConnectionChipPopover>
    );
  }

  return buttonEl;
}
