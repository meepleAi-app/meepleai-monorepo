/**
 * Loading State for Library Game Detail Page (Issue #3513)
 *
 * Displays skeleton loading animation matching the MeepleAI design system.
 */

import { ArrowLeft } from 'lucide-react';

import { Skeleton } from '@/components/ui/feedback/skeleton';

export default function LibraryGameDetailLoading() {
  return (
    <div className="min-h-screen bg-[#FAF8F5]">
      {/* Background gradient decoration */}
      <div
        className="pointer-events-none fixed inset-0"
        style={{
          background: `
            radial-gradient(ellipse at 20% 30%, hsla(25, 40%, 90%, 0.4) 0%, transparent 50%),
            radial-gradient(ellipse at 80% 70%, hsla(262, 30%, 92%, 0.3) 0%, transparent 50%)
          `,
        }}
      />

      {/* Header Skeleton */}
      <header className="sticky top-0 z-50 border-b border-[rgba(45,42,38,0.08)] bg-[rgba(250,248,245,0.85)] backdrop-blur-md">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-3 sm:px-6">
          <div className="flex items-center gap-2 text-[#6B665C]">
            <ArrowLeft className="h-5 w-5" />
            <span className="font-quicksand text-sm font-semibold">Torna alla Collezione</span>
          </div>
          <span className="font-quicksand text-lg font-bold tracking-tight text-[hsl(25,95%,38%)]">
            MeepleAI
          </span>
        </div>
      </header>

      {/* Main Content */}
      <main className="relative z-10 mx-auto max-w-6xl px-4 py-8 sm:px-6">
        {/* Cards Section Skeleton */}
        <section className="mb-8 flex flex-col items-center justify-center gap-6 lg:flex-row lg:items-start">
          {/* Game Card Skeleton */}
          <div className="w-full max-w-[420px] flex-shrink-0">
            <div
              className="relative overflow-hidden rounded-3xl border border-[rgba(45,42,38,0.08)] bg-[#FFFDF9]"
              style={{
                aspectRatio: '3 / 4',
                boxShadow: '0 8px 32px rgba(45, 42, 38, 0.12)',
              }}
            >
              {/* Orange left border */}
              <div className="absolute bottom-0 left-0 top-0 w-[5px] rounded-l-3xl bg-gradient-to-b from-[hsl(25,95%,38%)] to-[hsl(25,95%,48%)]" />

              {/* Cover area skeleton */}
              <div className="h-1/2 bg-[#F5F0E8]">
                <Skeleton className="h-full w-full rounded-none" />
              </div>

              {/* Info area skeleton */}
              <div className="flex h-1/2 flex-col p-6">
                {/* Title */}
                <Skeleton className="mb-2 h-8 w-3/4" />
                {/* Publisher/Year */}
                <Skeleton className="mb-4 h-5 w-1/2" />

                {/* Rating */}
                <div className="mb-4 flex items-center gap-2">
                  <Skeleton className="h-6 w-6 rounded-full" />
                  <Skeleton className="h-6 w-12" />
                </div>

                {/* Stats grid */}
                <div className="mt-auto grid grid-cols-3 gap-3">
                  {[1, 2, 3].map((i) => (
                    <div key={i} className="rounded-xl bg-[rgba(45,42,38,0.04)] p-3">
                      <Skeleton className="mx-auto mb-2 h-4 w-16" />
                      <Skeleton className="mx-auto h-6 w-12" />
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>

          {/* Side Card Skeleton */}
          <div className="w-full max-w-[420px] flex-shrink-0">
            <div
              className="overflow-hidden rounded-3xl border border-[rgba(45,42,38,0.08)] bg-[#FFFDF9]"
              style={{
                aspectRatio: '3 / 4',
                boxShadow: '0 8px 32px rgba(45, 42, 38, 0.12)',
              }}
            >
              {/* Tabs skeleton */}
              <div className="flex border-b border-[rgba(45,42,38,0.08)]">
                <Skeleton className="m-2 h-10 flex-1 rounded-lg" />
                <Skeleton className="m-2 h-10 flex-1 rounded-lg" />
              </div>

              {/* Content skeleton */}
              <div className="p-6">
                <Skeleton className="mb-4 h-6 w-1/3" />
                <div className="space-y-3">
                  {[1, 2, 3].map((i) => (
                    <div key={i} className="flex items-center gap-3 rounded-xl bg-[rgba(45,42,38,0.04)] p-4">
                      <Skeleton className="h-10 w-10 rounded-lg" />
                      <div className="flex-1">
                        <Skeleton className="mb-1 h-5 w-2/3" />
                        <Skeleton className="h-4 w-1/3" />
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* User Actions Section Skeleton */}
        <section className="rounded-3xl border border-[rgba(45,42,38,0.08)] bg-[#FFFDF9] p-6" style={{ boxShadow: '0 4px 20px rgba(45, 42, 38, 0.1)' }}>
          {/* Status and actions row */}
          <div className="mb-6 flex flex-wrap items-center gap-3">
            <Skeleton className="h-11 w-32 rounded-xl" />
            <Skeleton className="h-11 w-32 rounded-xl" />
            <Skeleton className="h-11 w-32 rounded-xl" />
            <div className="flex-grow" />
            <Skeleton className="h-11 w-28 rounded-xl" />
          </div>

          {/* Labels row */}
          <div className="mb-6 flex flex-wrap items-center gap-2 border-t border-[rgba(45,42,38,0.08)] pt-6">
            <Skeleton className="h-5 w-16" />
            <Skeleton className="h-8 w-20 rounded-full" />
            <Skeleton className="h-8 w-24 rounded-full" />
            <Skeleton className="h-8 w-16 rounded-full" />
            <Skeleton className="h-8 w-24 rounded-full" />
          </div>

          {/* Stats grid */}
          <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
            {[1, 2, 3, 4].map((i) => (
              <div key={i} className="rounded-xl bg-[rgba(45,42,38,0.04)] p-4 text-center">
                <Skeleton className="mx-auto mb-2 h-4 w-20" />
                <Skeleton className="mx-auto h-6 w-16" />
              </div>
            ))}
          </div>
        </section>
      </main>
    </div>
  );
}
