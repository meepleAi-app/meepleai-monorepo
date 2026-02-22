/**
 * Profile Page — Consolidated Hub
 * Issue #5039 — Consolidate User Routes
 *
 * Canonical profile page with tab support:
 * - ?tab=achievements → achievements section
 * - ?tab=badges       → badges section
 * - ?tab=settings     → settings (with optional ?section=notifications|security)
 * - (default)         → profile overview
 *
 * TODO (Issue #5043): Implement full tabbed profile with content for each tab.
 */

import { redirect } from 'next/navigation';

import { ProfileNavConfig } from './NavConfig';

interface ProfilePageProps {
  searchParams: Promise<{ tab?: string; section?: string }>;
}

export default async function ProfilePage({ searchParams }: ProfilePageProps) {
  const params = await searchParams;
  const tab = params.tab;

  // For now, redirect tab-less requests to achievements until full profile page
  // is implemented in Issue #5043.
  if (!tab) {
    redirect('/profile/achievements');
  }

  // Stub rendering for tabbed views — full implementation in Issue #5043
  return (
    <div className="min-h-screen bg-background">
      <ProfileNavConfig />
      <div className="mx-auto max-w-4xl px-4 py-8 sm:px-6">
        <h1 className="text-2xl font-bold tracking-tight text-foreground">Profilo</h1>
        <p className="mt-2 text-muted-foreground">
          Tab: {tab}
          {params.section ? ` / ${params.section}` : ''}
        </p>
        {/* TODO Issue #5043: Full tabbed profile page */}
      </div>
    </div>
  );
}
