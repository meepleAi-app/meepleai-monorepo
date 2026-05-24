// TODO: implement this route (see docs/superpowers/specs/route-gap-analysis.md)
// Mockup: sp3-shared-game-detail — public detail view for a shared game.
// Canonically implemented at /shared-games/[id]; this slug-less variant
// is a legacy mockup route. Redirects to /shared-games until an id is resolved.
'use client';

import { useEffect } from 'react';

import { useRouter } from 'next/navigation';

export default function SharedGameDetailPlaceholderPage() {
  const router = useRouter();

  useEffect(() => {
    const timer = setTimeout(() => {
      router.replace('/shared-games');
    }, 2000);
    return () => clearTimeout(timer);
  }, [router]);

  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-4 bg-background p-8">
      <h1 className="text-2xl font-semibold text-foreground">Coming Soon</h1>
      <p className="text-muted-foreground">
        This page is not yet implemented. Redirecting to <code>/shared-games</code>...
      </p>
    </main>
  );
}
