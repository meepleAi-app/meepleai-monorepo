'use client';

import type { ReactNode } from 'react';

interface MobileDevicePreviewProps {
  children: ReactNode;
  className?: string;
}

/**
 * Phone frame wrapper for the dev page showcase.
 *
 * Renders a 390×720 device frame matching the wireframe from
 * `admin-mockups/mobile-card-layout-mockup.html`:
 * - Status bar (time + icons)
 * - Navbar (logo + notifications + avatar)
 * - Search bar
 * - Children slot (content area — typically MobileCardLayout)
 * - Action bar at bottom
 */
export function MobileDevicePreview({ children, className = '' }: MobileDevicePreviewProps) {
  return (
    <div
      className={`relative flex flex-col overflow-hidden rounded-[40px] border-[3px] border-[#c8c0b5] bg-[var(--mc-bg-card)] shadow-[0_30px_80px_rgba(0,0,0,0.15),0_8px_24px_rgba(0,0,0,0.1)] ${className}`}
      style={{ width: 390, height: 720 }}
    >
      {/* Status bar */}
      <div className="flex h-10 flex-shrink-0 items-end justify-between px-7 pb-1.5 text-xs font-semibold text-[var(--mc-text-primary)]">
        <span className="font-bold">14:32</span>
        <div className="flex items-center gap-1 text-[11px]">
          <span>📶</span>
          <span>🔋</span>
        </div>
      </div>

      {/* Navbar */}
      <div className="flex h-14 flex-shrink-0 items-center gap-3 border-b border-[var(--mc-border)] bg-[var(--mc-bg-card)] px-4 backdrop-blur-[12px] backdrop-saturate-[180%]">
        <div className="flex items-center gap-1.5">
          <div className="flex h-7 w-7 items-center justify-center rounded-md bg-[hsl(25,95%,45%)] text-sm font-bold text-white">
            🎲
          </div>
          <span className="font-[var(--font-quicksand)] text-[1.1rem] font-extrabold text-[hsl(25,95%,45%)]">
            MeepleAI
          </span>
        </div>
        <div className="flex-1" />
        <button
          type="button"
          className="flex h-9 w-9 items-center justify-center rounded-full bg-[var(--mc-bg-muted)] text-base text-[var(--mc-text-secondary)] transition-colors hover:bg-black/10 dark:hover:bg-white/15"
        >
          🔔
        </button>
        <div className="flex h-9 w-9 items-center justify-center rounded-full bg-[hsl(262,83%,58%)] text-xs font-bold text-white">
          MR
        </div>
      </div>

      {/* Search bar */}
      <div className="flex-shrink-0 border-b border-[var(--mc-border)] px-4 py-2">
        <div className="flex items-center gap-2 rounded-xl bg-[var(--mc-bg-muted)] px-3 py-2.5">
          <span className="text-sm text-[var(--mc-text-secondary)]">🔍</span>
          <span className="text-sm text-[var(--mc-text-muted)]">Cerca nella tua libreria...</span>
        </div>
      </div>

      {/* Content area — fills remaining space */}
      <div className="relative flex-1 overflow-hidden">{children}</div>

      {/* Action bar */}
      <div className="flex h-[72px] flex-shrink-0 items-center justify-around border-t border-[var(--mc-border)] bg-[var(--mc-bg-card)] px-4 backdrop-blur-[12px]">
        {(['🏠 Home', '📚 Library', '🎲 Sessioni', '👤 Profilo'] as const).map(item => {
          const [icon, label] = item.split(' ');
          const isActive = label === 'Library';
          return (
            <button
              key={label}
              type="button"
              className={`flex flex-col items-center gap-0.5 text-[10px] font-semibold transition-colors ${
                isActive
                  ? 'text-[hsl(25,95%,45%)]'
                  : 'text-[var(--mc-text-secondary)] hover:text-[var(--mc-text-primary)]'
              }`}
            >
              <span className="text-lg">{icon}</span>
              <span>{label}</span>
            </button>
          );
        })}
      </div>
    </div>
  );
}
