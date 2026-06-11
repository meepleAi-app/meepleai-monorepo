'use client';

import { HubLayout } from '@/components/layout/HubLayout';

/**
 * #2158 (visual-smoke follow-up): the original MiniNavSlot config registered a
 * single tab labeled `Toolkit` that duplicated both the AppTopBar `Toolkit`
 * voice (already highlighted on this route) and the breadcrumb. Convention
 * adopted in #2158: MiniNavSlot only carries genuine multi-tab navigation
 * (≥2 alternatives) — single-tab configs are pure noise. Same pattern as
 * `/library`, `/library/[gameId]`, and `/discover`.
 */
export default function ToolkitHubPage() {
  return (
    <HubLayout searchPlaceholder="Cerca tool..." showViewToggle>
      <div className="flex flex-col items-center gap-3 py-16 px-4 text-center text-[var(--text-muted,#94a3b8)]">
        <span className="text-5xl">🛠️</span>
        <p className="font-medium">Toolkit in arrivo.</p>
        <p className="text-sm">Qui troverai timer, contatori, mazzi e altri strumenti di gioco.</p>
      </div>
    </HubLayout>
  );
}
