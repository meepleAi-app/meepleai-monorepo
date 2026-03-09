/**
 * Discover Game Detail Page
 * Issue #5039 — Consolidate User Routes
 *
 * Server component: fetches game data + library status, then either:
 * - redirects to /library/games/[gameId] if already in library
 * - shows game detail with "Add to Library" CTA
 */

import { ArrowLeft } from 'lucide-react';
import { cookies } from 'next/headers';
import Link from 'next/link';
import { notFound, redirect } from 'next/navigation';

import { GameDiscoverDetail } from '@/components/discover/GameDiscoverDetail';
import type { SharedGameDetail } from '@/lib/api/schemas/shared-games.schemas';

const API_BASE =
  process.env.API_BASE_URL || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
const SESSION_COOKIE = 'meepleai_session';

interface DiscoverGamePageProps {
  params: Promise<{ gameId: string }>;
}

export default async function DiscoverGamePage({ params }: DiscoverGamePageProps) {
  const { gameId } = await params;
  const cookieStore = await cookies();
  const sessionCookie = cookieStore.get(SESSION_COOKIE);

  const [game, status] = await Promise.all([
    fetch(`${API_BASE}/api/v1/shared-games/${gameId}`, { cache: 'no-store' })
      .then<SharedGameDetail | null>(r => (r.ok ? r.json() : null))
      .catch(() => null),
    sessionCookie
      ? fetch(`${API_BASE}/api/v1/library/games/${gameId}/status`, {
          headers: { Cookie: `${SESSION_COOKIE}=${sessionCookie.value}` },
          cache: 'no-store',
        })
          .then<{ inLibrary: boolean; isFavorite: boolean } | null>(r => (r.ok ? r.json() : null))
          .catch(() => null)
      : Promise.resolve(null),
  ]);

  if (!game) notFound();
  if (status?.inLibrary) redirect(`/library/games/${gameId}`);

  return (
    <div className="min-h-screen bg-background">
      <div className="mx-auto max-w-6xl px-4 py-6 sm:px-6">
        <Link
          href="/discover"
          className="inline-flex items-center gap-2 text-sm font-medium text-muted-foreground hover:text-foreground transition-colors"
        >
          <ArrowLeft className="h-4 w-4" />
          Torna a Scopri
        </Link>
        <div className="mt-6">
          <GameDiscoverDetail game={game} />
        </div>
      </div>
    </div>
  );
}
