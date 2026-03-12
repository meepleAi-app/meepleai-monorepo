/**
 * Onboarding Layout
 * Issue #132 - Invitation Acceptance & Onboarding Wizard
 *
 * Minimal layout for invitation acceptance flow.
 * Glassmorphic design with amber accent per project design tokens.
 */

import { ReactNode } from 'react';

export default function OnboardingLayout({ children }: { children: ReactNode }) {
  return (
    <div className="min-h-screen bg-gradient-to-b from-amber-50/50 to-white">
      <header className="border-b bg-white/70 backdrop-blur-md px-6 py-4">
        <div className="mx-auto max-w-3xl">
          <span className="font-quicksand text-xl font-bold text-amber-900">MeepleAI</span>
        </div>
      </header>
      <main className="mx-auto max-w-3xl px-6 py-8">{children}</main>
    </div>
  );
}
