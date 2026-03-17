'use client';

import { useEffect } from 'react';

import FocusTrap from 'focus-trap-react';
import { motion, AnimatePresence } from 'framer-motion';
import Link from 'next/link';

import { ENTITY_NAV_ICONS } from '@/components/ui/data-display/meeple-card-features/navigation-icons';
import { entityColors } from '@/components/ui/data-display/meeple-card-styles';
import { useContextualEntity } from '@/hooks/useContextualEntity';
import { useContextualSheetActions, type SheetAction } from '@/hooks/useContextualSheetActions';
import { cn } from '@/lib/utils';
import { useCardHand } from '@/stores/use-card-hand';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface ContextualBottomSheetProps {
  isOpen: boolean;
  onClose: () => void;
}

// ---------------------------------------------------------------------------
// Sub-components
// ---------------------------------------------------------------------------

function DragHandle() {
  return (
    <div className="flex justify-center pt-3 pb-1">
      <div className="w-10 h-1 rounded-full bg-muted-foreground/30" />
    </div>
  );
}

interface EntityHeaderProps {
  title: string;
  entityType: string;
  color: string;
  IconComponent: React.ComponentType<{ className?: string }>;
}

function EntityHeader({ title, entityType, color, IconComponent }: EntityHeaderProps) {
  return (
    <div className="flex items-center gap-3 px-4 py-3">
      <div
        className="w-9 h-9 rounded-xl flex items-center justify-center flex-shrink-0"
        style={{ backgroundColor: `hsl(${color} / 0.15)`, color: `hsl(${color})` }}
      >
        <IconComponent className="w-5 h-5" />
      </div>
      <div className="min-w-0">
        <p className="font-semibold text-foreground truncate leading-tight">{title}</p>
        <p className="text-xs text-muted-foreground capitalize">{entityType}</p>
      </div>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Main component
// ---------------------------------------------------------------------------

export function ContextualBottomSheet({ isOpen, onClose }: ContextualBottomSheetProps) {
  const { cards, focusedIdx } = useCardHand();
  const entity = useContextualEntity();
  const actions = useContextualSheetActions();

  // Escape key handler
  useEffect(() => {
    if (!isOpen) return;

    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') {
        onClose();
      }
    }

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  // Prevent body scroll when open (mobile only — desktop scrolls via <main>)
  useEffect(() => {
    if (!isOpen) return;
    const isMobile = window.matchMedia('(max-width: 767px)').matches;
    if (!isMobile) return;
    document.body.style.overflow = 'hidden';
    return () => {
      document.body.style.overflow = '';
    };
  }, [isOpen]);

  const entityType = entity?.type ?? 'game';
  const entityTitle = entity?.title ?? (focusedIdx >= 0 ? cards[focusedIdx]?.title : null) ?? '';
  const entityColor = entity?.color ?? entityColors[entityType]?.hsl ?? '220 70% 50%';
  const IconComponent = ENTITY_NAV_ICONS[entityType] ?? ENTITY_NAV_ICONS.game;

  return (
    <AnimatePresence>
      {isOpen && (
        <>
          {/* Backdrop */}
          <motion.div
            data-testid="bottom-sheet-backdrop"
            className="fixed inset-0 z-40 bg-black/50"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.2 }}
            onClick={onClose}
          />

          {/* Sheet */}
          <FocusTrap
            focusTrapOptions={{
              allowOutsideClick: true,
              fallbackFocus: '[role="dialog"]',
            }}
          >
            <motion.div
              role="dialog"
              aria-modal="true"
              aria-label={entityTitle || 'Contextual actions'}
              tabIndex={-1}
              className={cn(
                'fixed bottom-0 left-0 right-0 z-50',
                'bg-card/95 backdrop-blur-xl rounded-t-2xl',
                'pb-[env(safe-area-inset-bottom)]',
                'max-h-[85vh] overflow-y-auto'
              )}
              initial={{ y: '100%' }}
              animate={{ y: 0 }}
              exit={{ y: '100%' }}
              transition={{ type: 'spring', damping: 30, stiffness: 300 }}
              drag="y"
              dragConstraints={{ top: 0 }}
              dragElastic={{ top: 0, bottom: 0.3 }}
              onDragEnd={(_event, info) => {
                if (info.offset.y > 80) {
                  onClose();
                }
              }}
            >
              {/* Drag handle */}
              <DragHandle />

              {/* Entity header */}
              {entityTitle && (
                <EntityHeader
                  title={entityTitle}
                  entityType={entityType}
                  color={entityColor}
                  IconComponent={IconComponent}
                />
              )}

              {/* Open cards carousel */}
              {cards.length > 0 && (
                <section aria-label="Open cards" className="px-4 py-2">
                  <p className="text-xs font-medium text-muted-foreground mb-2 uppercase tracking-wide">
                    Open Cards
                  </p>
                  <div className="flex gap-2 overflow-x-auto pb-1 scrollbar-none">
                    {cards.map((card, idx) => {
                      const cardColor = entityColors[card.entity]?.hsl ?? entityColors.game.hsl;
                      const CardIcon = ENTITY_NAV_ICONS[card.entity] ?? ENTITY_NAV_ICONS.game;
                      const isFocused = idx === focusedIdx;

                      return (
                        <Link
                          key={card.id}
                          href={card.href}
                          onClick={onClose}
                          className={cn(
                            'flex-shrink-0 flex items-center gap-2 px-3 py-2 rounded-xl',
                            'border transition-colors duration-150 min-w-[120px] max-w-[160px]',
                            isFocused
                              ? 'border-transparent'
                              : 'border-border/50 bg-muted/30 hover:bg-muted/60'
                          )}
                          style={
                            isFocused
                              ? {
                                  backgroundColor: `hsl(${cardColor} / 0.12)`,
                                  borderColor: `hsl(${cardColor} / 0.4)`,
                                }
                              : {}
                          }
                        >
                          <span className="flex-shrink-0" style={{ color: `hsl(${cardColor})` }}>
                            <CardIcon className="w-4 h-4" />
                          </span>
                          <span className="text-sm font-medium text-foreground truncate">
                            {card.title}
                          </span>
                        </Link>
                      );
                    })}
                  </div>
                </section>
              )}

              {/* Contextual actions grid */}
              {actions.length > 0 && (
                <section aria-label="Contextual actions" className="px-4 pt-2 pb-4">
                  <p className="text-xs font-medium text-muted-foreground mb-2 uppercase tracking-wide">
                    Actions
                  </p>
                  <div className="grid grid-cols-2 gap-2">
                    {actions.map((action: SheetAction) => {
                      const ActionIcon = action.icon;
                      const isPrimary = action.variant === 'primary';

                      return (
                        <button
                          key={action.id}
                          onClick={() => {
                            action.onClick();
                            onClose();
                          }}
                          className={cn(
                            'flex items-center gap-2 px-4 py-3 rounded-xl transition-colors duration-150',
                            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                            isPrimary
                              ? 'col-span-2 bg-primary text-primary-foreground hover:bg-primary/90 justify-center font-semibold'
                              : 'bg-muted/40 text-foreground hover:bg-muted/70 justify-start'
                          )}
                        >
                          <ActionIcon className="w-4 h-4 flex-shrink-0" />
                          <span className="text-sm font-medium">{action.label}</span>
                        </button>
                      );
                    })}
                  </div>
                </section>
              )}
            </motion.div>
          </FocusTrap>
        </>
      )}
    </AnimatePresence>
  );
}
