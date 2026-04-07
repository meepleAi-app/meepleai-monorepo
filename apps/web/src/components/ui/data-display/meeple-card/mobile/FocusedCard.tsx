'use client';

import { MetaChips } from '../parts/MetaChips';
import { entityHsl, entityLabel } from '../tokens';

import type { MeepleCardProps } from '../types';

export function FocusedCard(props: MeepleCardProps) {
  const {
    entity,
    title,
    subtitle,
    imageUrl,
    rating,
    ratingMax,
    metadata = [],
    actions = [],
    onClick,
  } = props;
  const color = entityHsl(entity);

  return (
    <div
      className="flex flex-1 flex-col overflow-hidden rounded-[14px] border border-[var(--mc-border)] bg-[var(--mc-bg-card)] shadow-[var(--mc-shadow-xl)] outline-2 outline-offset-2 outline-transparent backdrop-blur-[12px]"
      onClick={onClick}
    >
      <div className="h-[3px] w-full flex-shrink-0" style={{ background: color }} />
      {imageUrl && (
        <div className="relative flex-shrink-0 overflow-hidden">
          <img src={imageUrl} alt={title} className="h-40 w-full object-cover" />
          <div
            className="absolute bottom-0 left-0 right-0 h-1/2"
            style={{ background: 'linear-gradient(to top, var(--mc-bg-card), transparent)' }}
          />
          <span
            className="absolute left-2 top-2 rounded-2xl px-2 py-0.5 font-[var(--font-quicksand)] text-[8px] font-bold uppercase tracking-wide text-white backdrop-blur-lg"
            style={{ background: entityHsl(entity, 0.8) }}
          >
            {entityLabel[entity]}
          </span>
          {rating !== undefined && (
            <span className="absolute right-2 top-2 flex items-center gap-[3px] rounded-[10px] bg-black/50 px-2 py-[3px] text-[10px] font-bold text-amber-300 backdrop-blur-lg">
              ★ {rating.toFixed(1)}
              {ratingMax && <span className="text-white/60">/{ratingMax}</span>}
            </span>
          )}
        </div>
      )}
      <div className="flex flex-1 flex-col gap-[5px] overflow-y-auto p-2.5 px-3">
        <h3 className="font-[var(--font-quicksand)] text-base font-bold leading-tight">{title}</h3>
        {subtitle && (
          <p className="text-[0.72rem] leading-tight text-[var(--mc-text-secondary)]">{subtitle}</p>
        )}
        {metadata.length > 0 && <MetaChips metadata={metadata} />}
      </div>
      {actions.length > 0 && (
        <div className="mt-auto flex gap-1.5 border-t border-[var(--mc-border-light)] p-2">
          {actions.map((action, i) => (
            <button
              key={i}
              onClick={e => {
                e.stopPropagation();
                action.onClick();
              }}
              disabled={action.disabled}
              className={`flex h-8 w-8 items-center justify-center rounded-full text-[13px] shadow-[var(--mc-shadow-sm)] transition-transform hover:scale-110 ${action.variant === 'primary' ? 'border-none text-white' : 'border border-white/60 bg-white/85 backdrop-blur-lg'}`}
              style={action.variant === 'primary' ? { background: color } : undefined}
              title={action.label}
            >
              {action.icon}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
