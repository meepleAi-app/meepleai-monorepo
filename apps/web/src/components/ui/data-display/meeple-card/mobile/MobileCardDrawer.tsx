'use client';

import { useEffect, useState } from 'react';

import { entityHsl, entityLabel } from '../tokens';
import { getDrawerTabs } from './drawerTabs';

import type { MeepleCardProps } from '../types';

interface MobileCardDrawerProps {
  /** Card currently shown in the drawer. When null, drawer is closed. */
  card: MeepleCardProps | null;
  /** Called when user dismisses the drawer (backdrop click, close button). */
  onClose: () => void;
  /**
   * Optional bounds that constrain the drawer overlay.
   * When the drawer is mounted inside a phone-frame preview, these bounds
   * keep the drawer inside the phone instead of filling the full viewport.
   */
  containerClassName?: string;
}

/**
 * Full-screen drawer overlay used in the mobile card layout.
 *
 * Opens when the focused card is clicked. Slides in from the top and shows
 * entity-specific tabs (game, agent, session, kb, chat, event, player,
 * toolkit, tool). Tab content is a placeholder that previews what the real
 * ExtraMeepleCardDrawer would render in production.
 */
export function MobileCardDrawer({
  card,
  onClose,
  containerClassName = '',
}: MobileCardDrawerProps) {
  const [activeTabId, setActiveTabId] = useState<string | null>(null);

  // Reset active tab whenever the card changes, so drawer re-opens on a
  // freshly-clicked card always start from the first tab.
  useEffect(() => {
    if (card) {
      const tabs = getDrawerTabs(card.entity);
      setActiveTabId(tabs[0]?.id ?? null);
    } else {
      setActiveTabId(null);
    }
  }, [card]);

  if (!card) return null;

  const tabs = getDrawerTabs(card.entity);
  const activeTab = tabs.find(t => t.id === activeTabId) ?? tabs[0];
  const accent = entityHsl(card.entity);

  return (
    <div
      className={`absolute inset-0 z-30 flex flex-col ${containerClassName}`}
      role="dialog"
      aria-modal="true"
      aria-label={`Dettagli ${card.title}`}
    >
      {/* Backdrop — tap to dismiss */}
      <button
        type="button"
        aria-label="Chiudi drawer"
        className="absolute inset-0 h-full w-full cursor-default bg-black/40 backdrop-blur-sm"
        onClick={onClose}
      />

      {/* Drawer content — slides from top */}
      <div
        className="relative mx-auto mt-10 flex w-[calc(100%-16px)] flex-1 flex-col overflow-hidden rounded-t-[20px] border border-[var(--mc-border)] bg-[var(--mc-bg-card)] shadow-[0_-8px_32px_rgba(0,0,0,0.15)] backdrop-blur-[16px]"
        style={{ animation: 'meeplecard-drawer-slide 0.25s cubic-bezier(0.4,0,0.2,1)' }}
      >
        {/* Pull handle */}
        <div className="flex justify-center py-2">
          <div className="h-1 w-10 rounded-full bg-[var(--mc-border)]" />
        </div>

        {/* Header: entity ribbon + title + close */}
        <div className="flex items-start gap-2 border-b border-[var(--mc-border)] px-4 pb-3">
          <div
            className="mt-0.5 h-9 w-9 flex-shrink-0 rounded-lg text-xs font-bold text-white flex items-center justify-center uppercase tracking-wide"
            style={{ background: accent }}
            aria-hidden
          >
            {entityLabel[card.entity].slice(0, 2)}
          </div>
          <div className="min-w-0 flex-1">
            <h3 className="font-[var(--font-quicksand)] text-base font-bold leading-tight text-[var(--mc-text-primary)]">
              {card.title}
            </h3>
            {card.subtitle && (
              <p className="truncate text-xs text-[var(--mc-text-secondary)]">{card.subtitle}</p>
            )}
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Chiudi"
            className="flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-full bg-[var(--mc-bg-muted)] text-sm text-[var(--mc-text-secondary)] transition-colors hover:bg-black/10 dark:hover:bg-white/15"
          >
            ✕
          </button>
        </div>

        {/* Tab bar — horizontal scroll */}
        <div
          role="tablist"
          aria-label={`Tab di ${entityLabel[card.entity]}`}
          className="flex gap-1 overflow-x-auto border-b border-[var(--mc-border)] px-2 py-1.5"
          style={{ scrollbarWidth: 'none' }}
        >
          {tabs.map(tab => {
            const isActive = tab.id === activeTab?.id;
            return (
              <button
                key={tab.id}
                type="button"
                role="tab"
                aria-selected={isActive}
                aria-controls={`tab-panel-${tab.id}`}
                id={`tab-${tab.id}`}
                onClick={() => setActiveTabId(tab.id)}
                className={`flex flex-shrink-0 items-center gap-1 rounded-full px-3 py-1.5 text-xs font-semibold transition-colors ${
                  isActive
                    ? 'text-white'
                    : 'bg-[var(--mc-bg-muted)] text-[var(--mc-text-secondary)] hover:bg-black/10 dark:hover:bg-white/15'
                }`}
                style={isActive ? { background: accent } : undefined}
              >
                <span aria-hidden>{tab.icon}</span>
                <span>{tab.label}</span>
              </button>
            );
          })}
        </div>

        {/* Tab content — placeholder with card data preview */}
        <div
          role="tabpanel"
          id={activeTab ? `tab-panel-${activeTab.id}` : undefined}
          aria-labelledby={activeTab ? `tab-${activeTab.id}` : undefined}
          className="flex-1 overflow-y-auto px-4 py-3"
        >
          {activeTab ? (
            <DrawerTabPlaceholder card={card} tabLabel={activeTab.label} tabIcon={activeTab.icon} />
          ) : null}
        </div>
      </div>

      <style jsx>{`
        @keyframes meeplecard-drawer-slide {
          from {
            transform: translateY(-16px);
            opacity: 0;
          }
          to {
            transform: translateY(0);
            opacity: 1;
          }
        }
      `}</style>
    </div>
  );
}

interface DrawerTabPlaceholderProps {
  card: MeepleCardProps;
  tabLabel: string;
  tabIcon: string;
}

function DrawerTabPlaceholder({ card, tabLabel, tabIcon }: DrawerTabPlaceholderProps) {
  const accent = entityHsl(card.entity);

  return (
    <div className="space-y-3">
      <div
        className="flex items-center gap-2 rounded-lg border px-3 py-2 text-xs"
        style={{
          background: entityHsl(card.entity, 0.08),
          borderColor: entityHsl(card.entity, 0.25),
          color: accent,
        }}
      >
        <span aria-hidden className="text-base">
          {tabIcon}
        </span>
        <span className="font-semibold uppercase tracking-wide">Tab: {tabLabel}</span>
      </div>
      <p className="text-xs text-[var(--mc-text-secondary)]">
        Questa è una preview del tab <strong>{tabLabel}</strong> per entity type{' '}
        <strong>{entityLabel[card.entity]}</strong>. In produzione, qui verrebbe caricato il
        contenuto reale (es. ExtraMeepleCardDrawer tabs come Overview/AI/History/Media).
      </p>
      <dl className="grid grid-cols-[auto,1fr] gap-x-3 gap-y-1.5 text-xs">
        {card.badge && (
          <>
            <dt className="font-semibold text-[var(--mc-text-secondary)]">Badge</dt>
            <dd className="text-[var(--mc-text-primary)]">{card.badge}</dd>
          </>
        )}
        {card.status && (
          <>
            <dt className="font-semibold text-[var(--mc-text-secondary)]">Status</dt>
            <dd className="text-[var(--mc-text-primary)]">{card.status}</dd>
          </>
        )}
        {card.rating !== undefined && (
          <>
            <dt className="font-semibold text-[var(--mc-text-secondary)]">Rating</dt>
            <dd className="text-[var(--mc-text-primary)]">
              {card.rating.toFixed(1)}
              {card.ratingMax ? ` / ${card.ratingMax}` : ''}
            </dd>
          </>
        )}
        {card.metadata?.map((meta, i) => (
          <div key={i} className="contents">
            <dt className="font-semibold text-[var(--mc-text-secondary)]">
              {meta.label.split(/[:·]/)[0] || 'Meta'}
            </dt>
            <dd className="text-[var(--mc-text-primary)]">{meta.label}</dd>
          </div>
        ))}
      </dl>
    </div>
  );
}
