// TODO: implement this route (see docs/superpowers/specs/route-gap-analysis.md)
// Mockup: sp3-legal.jsx — legal hub combining /terms and /privacy.
// This placeholder redirects to /terms until a unified /legal hub is built.
'use client';

import { useEffect } from 'react';

import { useRouter } from 'next/navigation';

export default function LegalPlaceholderPage() {
  const router = useRouter();

  useEffect(() => {
    const timer = setTimeout(() => {
      router.replace('/terms');
    }, 2000);
    return () => clearTimeout(timer);
  }, [router]);

  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-4 bg-background p-8">
      <h1 className="text-2xl font-semibold text-foreground">Coming Soon</h1>
      <p className="text-muted-foreground">
        This page is not yet implemented. Redirecting to <code>/terms</code>...
      </p>
    </main>
  );
}
