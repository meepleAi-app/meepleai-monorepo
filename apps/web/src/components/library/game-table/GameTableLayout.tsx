'use client';

import { type ReactNode, useCallback, useEffect, useState } from 'react';

import { AnimatePresence, motion } from 'framer-motion';

import { TavoloSection } from '@/components/dashboard/tavolo';
import { useMediaQuery } from '@/hooks/useMediaQuery';
import { cn } from '@/lib/utils';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface GameTableLayoutProps {
  card: ReactNode;
  toolsZone: ReactNode;
  knowledgeZone: ReactNode;
  sessionsZone: ReactNode;
  drawer?: ReactNode;
  drawerOpen?: boolean;
  onDrawerClose?: () => void;
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

type ZoneId = 'tools' | 'knowledge' | 'sessions';

const ZONE_META: Record<ZoneId, { icon: string; title: string }> = {
  tools: { icon: '\uD83D\uDEE0\uFE0F', title: 'Strumenti' },
  knowledge: { icon: '\uD83D\uDCDA', title: 'Conoscenza' },
  sessions: { icon: '\uD83C\uDFB2', title: 'Sessioni' },
};

const ACCORDION_VARIANTS = {
  collapsed: { height: 0, opacity: 0, overflow: 'hidden' as const },
  expanded: { height: 'auto', opacity: 1, overflow: 'visible' as const },
};

const DRAWER_BACKDROP_VARIANTS = {
  hidden: { opacity: 0 },
  visible: { opacity: 1 },
};

const DRAWER_PANEL_VARIANTS = {
  hidden: { y: '100%' },
  visible: { y: 0 },
};

// ---------------------------------------------------------------------------
// Accordion header (mobile only)
// ---------------------------------------------------------------------------

function AccordionHeader({
  zone,
  isOpen,
  onToggle,
}: {
  zone: ZoneId;
  isOpen: boolean;
  onToggle: () => void;
}) {
  const meta = ZONE_META[zone];
  return (
    <button
      type="button"
      onClick={onToggle}
      aria-expanded={isOpen}
      className="flex w-full items-center gap-2 py-3"
    >
      <span className="text-sm">{meta.icon}</span>
      <h2 className="text-sm font-semibold uppercase tracking-wider text-[#8b949e]">
        {meta.title}
      </h2>
      <div className="h-px flex-1 bg-[#30363d]" />
      <motion.span
        animate={{ rotate: isOpen ? 180 : 0 }}
        transition={{ duration: 0.2 }}
        className="text-[#8b949e]"
      >
        <ChevronDown />
      </motion.span>
    </button>
  );
}

function ChevronDown() {
  return (
    <svg
      width="16"
      height="16"
      viewBox="0 0 16 16"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      aria-hidden="true"
    >
      <path
        d="M4 6l4 4 4-4"
        stroke="currentColor"
        strokeWidth="1.5"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

// ---------------------------------------------------------------------------
// Drawer overlay
// ---------------------------------------------------------------------------

function DrawerOverlay({
  drawer,
  open,
  onClose,
}: {
  drawer: ReactNode;
  open: boolean;
  onClose?: () => void;
}) {
  // Close on Escape
  useEffect(() => {
    if (!open || !onClose) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', handler);
    return () => document.removeEventListener('keydown', handler);
  }, [open, onClose]);

  return (
    <AnimatePresence>
      {open && (
        <div className="fixed inset-0 z-50 flex items-end justify-center">
          {/* Backdrop */}
          <motion.div
            data-testid="drawer-backdrop"
            className="absolute inset-0 bg-black/60 backdrop-blur-sm"
            variants={DRAWER_BACKDROP_VARIANTS}
            initial="hidden"
            animate="visible"
            exit="hidden"
            transition={{ duration: 0.2 }}
            onClick={onClose}
          />

          {/* Panel */}
          <motion.div
            role="dialog"
            aria-modal="true"
            className={cn(
              'relative z-10 overflow-y-auto rounded-t-2xl bg-[#161b22] border border-[#30363d] border-b-0',
              // Mobile: full-width 90vh
              'h-[90vh] w-full',
              // Tablet: 80vh 85% width
              'sm:h-[80vh] sm:w-[85%] sm:rounded-t-2xl',
              // Desktop: 70vh 60% width
              'lg:h-[70vh] lg:w-[60%]'
            )}
            variants={DRAWER_PANEL_VARIANTS}
            initial="hidden"
            animate="visible"
            exit="hidden"
            transition={{ type: 'spring', damping: 30, stiffness: 300 }}
          >
            {/* Close button */}
            <button
              type="button"
              onClick={onClose}
              aria-label="Chiudi"
              className="absolute right-3 top-3 z-10 rounded-full p-1.5 text-[#8b949e] hover:bg-[#30363d] hover:text-[#e6edf3] transition-colors"
            >
              <svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true">
                <path
                  d="M6 6l8 8M14 6l-8 8"
                  stroke="currentColor"
                  strokeWidth="1.5"
                  strokeLinecap="round"
                />
              </svg>
            </button>

            <div className="p-4 pt-10">{drawer}</div>
          </motion.div>
        </div>
      )}
    </AnimatePresence>
  );
}

// ---------------------------------------------------------------------------
// Main component
// ---------------------------------------------------------------------------

export function GameTableLayout({
  card,
  toolsZone,
  knowledgeZone,
  sessionsZone,
  drawer,
  drawerOpen = false,
  onDrawerClose,
}: GameTableLayoutProps) {
  const isMobile = useMediaQuery('(max-width: 639px)');

  // Mobile accordion state -- one zone open at a time
  const [openZone, setOpenZone] = useState<ZoneId | null>('tools');
  // Mobile focus mode -- card expanded
  const [focusMode, setFocusMode] = useState(false);

  const toggleZone = useCallback((zone: ZoneId) => {
    setOpenZone(prev => (prev === zone ? null : zone));
  }, []);

  // -----------------------------------------------------------------------
  // Mobile layout
  // -----------------------------------------------------------------------
  if (isMobile) {
    return (
      <div className="flex min-h-screen flex-col bg-[#0d1117]">
        {/* Card hero */}
        <motion.div
          layout
          className={cn('shrink-0 transition-all', focusMode ? 'h-[85vh]' : 'h-[60vh]')}
        >
          <div className="h-full" onClick={() => !focusMode && setFocusMode(true)}>
            {card}
          </div>
        </motion.div>

        {/* Focus mode exit button */}
        <AnimatePresence>
          {focusMode && (
            <motion.div
              initial={{ opacity: 0, y: -10 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -10 }}
              className="flex justify-center py-2"
            >
              <button
                type="button"
                onClick={() => setFocusMode(false)}
                className="rounded-full bg-[#161b22] border border-[#30363d] px-4 py-1.5 text-xs font-medium text-[#8b949e] hover:text-[#e6edf3] transition-colors"
              >
                Torna al tavolo
              </button>
            </motion.div>
          )}
        </AnimatePresence>

        {/* Accordion zones */}
        <AnimatePresence>
          {!focusMode && (
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              className="flex-1 space-y-1 px-4 pb-4"
            >
              {(
                [
                  ['tools', toolsZone],
                  ['knowledge', knowledgeZone],
                  ['sessions', sessionsZone],
                ] as const
              ).map(([zone, content]) => (
                <div key={zone}>
                  <AccordionHeader
                    zone={zone}
                    isOpen={openZone === zone}
                    onToggle={() => toggleZone(zone)}
                  />
                  <AnimatePresence initial={false}>
                    {openZone === zone && (
                      <motion.div
                        key={`${zone}-content`}
                        variants={ACCORDION_VARIANTS}
                        initial="collapsed"
                        animate="expanded"
                        exit="collapsed"
                        transition={{ duration: 0.25, ease: 'easeInOut' }}
                      >
                        {content}
                      </motion.div>
                    )}
                  </AnimatePresence>
                </div>
              ))}
            </motion.div>
          )}
        </AnimatePresence>

        {/* Drawer */}
        {drawer && <DrawerOverlay drawer={drawer} open={drawerOpen} onClose={onDrawerClose} />}
      </div>
    );
  }

  // -----------------------------------------------------------------------
  // Tablet / Desktop grid layout
  // -----------------------------------------------------------------------
  return (
    <div className="min-h-screen bg-[#0d1117] p-4 lg:p-6">
      <div
        className={cn(
          'grid gap-4',
          // Tablet: 3-col compact
          'sm:grid-cols-[1fr_1.5fr_1fr]',
          // Desktop: 3-col wider center
          'lg:grid-cols-[1.2fr_2fr_1.2fr]'
        )}
      >
        {/* Left zone: Strumenti */}
        <div className="space-y-4">
          <TavoloSection icon={ZONE_META.tools.icon} title={ZONE_META.tools.title}>
            {toolsZone}
          </TavoloSection>
        </div>

        {/* Center: Card */}
        <div>{card}</div>

        {/* Right zone: Conoscenza */}
        <div className="space-y-4">
          <TavoloSection icon={ZONE_META.knowledge.icon} title={ZONE_META.knowledge.title}>
            {knowledgeZone}
          </TavoloSection>
        </div>
      </div>

      {/* Bottom zone: Sessioni */}
      <div className="mt-4">
        <TavoloSection icon={ZONE_META.sessions.icon} title={ZONE_META.sessions.title}>
          {sessionsZone}
        </TavoloSection>
      </div>

      {/* Drawer */}
      {drawer && <DrawerOverlay drawer={drawer} open={drawerOpen} onClose={onDrawerClose} />}
    </div>
  );
}
