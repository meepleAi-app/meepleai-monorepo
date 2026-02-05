/**
 * GamesCarouselSection - Game Carousels for Homepage
 *
 * Issue #3588: GC-003 — Homepage Integration
 * Epic: #3585 — GameCarousel Integration & Production Readiness
 *
 * Features:
 * - Featured Games carousel (always visible)
 * - Trending Games carousel (always visible)
 * - User Library carousel (authenticated users only)
 * - Lazy loading for below-the-fold sections
 * - "See All" links to gallery page
 */

'use client';

import { useCallback, useRef, useEffect, useState } from 'react';

import { ArrowRight } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import {
  GameCarousel,
  GameCarouselSkeleton,
  type CarouselGame,
} from '@/components/ui/data-display/game-carousel';
import { useFeaturedGames, useTrendingGames, useUserLibraryGames } from '@/hooks/queries';
import { useCurrentUser } from '@/hooks/queries';
import { cn } from '@/lib/utils';

// ============================================================================
// Section Header Component
// ============================================================================

interface SectionHeaderProps {
  title: string;
  subtitle?: string;
  href?: string;
  className?: string;
}

function SectionHeader({ title, subtitle, href, className }: SectionHeaderProps) {
  return (
    <div className={cn('flex items-end justify-between mb-6 px-4 md:px-8', className)}>
      <div>
        <h2 className="font-quicksand font-bold text-2xl md:text-3xl text-foreground">
          {title}
        </h2>
        {subtitle && (
          <p className="mt-1 text-muted-foreground text-sm md:text-base">
            {subtitle}
          </p>
        )}
      </div>
      {href && (
        <Link
          href={href}
          className={cn(
            'flex items-center gap-1',
            'text-primary hover:text-primary/80',
            'text-sm font-medium',
            'transition-colors duration-200',
            'group'
          )}
        >
          Vedi tutti
          <ArrowRight className="w-4 h-4 transition-transform group-hover:translate-x-1" />
        </Link>
      )}
    </div>
  );
}

// ============================================================================
// Intersection Observer Hook for Lazy Loading
// ============================================================================

function useIntersectionObserver(
  ref: React.RefObject<HTMLElement | null>,
  options?: IntersectionObserverInit
) {
  const [isIntersecting, setIsIntersecting] = useState(false);

  useEffect(() => {
    if (!ref.current) return;

    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          setIsIntersecting(true);
          // Disconnect after first intersection (only load once)
          observer.disconnect();
        }
      },
      {
        threshold: 0.1,
        rootMargin: '200px 0px', // Start loading 200px before visible
        ...options,
      }
    );

    observer.observe(ref.current);

    return () => observer.disconnect();
  }, [ref, options]);

  return isIntersecting;
}

// ============================================================================
// Featured Games Section
// ============================================================================

function FeaturedGamesSection() {
  const router = useRouter();

  const {
    data: featuredData,
    isLoading,
    isError,
  } = useFeaturedGames(8);

  const handleGameSelect = useCallback(
    (game: CarouselGame) => {
      router.push(`/giochi/${game.id}`);
    },
    [router]
  );

  if (isLoading) {
    return <GameCarouselSkeleton />;
  }

  if (isError || !featuredData?.games.length) {
    return null;
  }

  return (
    <section className="py-8 md:py-12">
      <SectionHeader
        title="Giochi in Evidenza"
        subtitle="I giochi più amati dalla community"
        href="/gallery"
      />
      <GameCarousel
        games={featuredData.games}
        onGameSelect={handleGameSelect}
        showDots
        autoPlay
        autoPlayInterval={6000}
      />
    </section>
  );
}

// ============================================================================
// Trending Games Section (Lazy Loaded)
// ============================================================================

function TrendingGamesSection() {
  const router = useRouter();
  const sectionRef = useRef<HTMLElement>(null);
  const isVisible = useIntersectionObserver(sectionRef);

  const {
    data: trendingData,
    isLoading,
    isError,
  } = useTrendingGames(8, isVisible); // Only fetch when visible

  const handleGameSelect = useCallback(
    (game: CarouselGame) => {
      router.push(`/giochi/${game.id}`);
    },
    [router]
  );

  return (
    <section ref={sectionRef} className="py-8 md:py-12 bg-muted/30">
      <SectionHeader
        title="Trending Now"
        subtitle="I più giocati questa settimana"
        href="/gallery?sort=popularity"
      />
      {!isVisible || isLoading ? (
        <GameCarouselSkeleton />
      ) : isError || !trendingData?.games.length ? (
        <div className="py-8 text-center text-muted-foreground">
          Nessun gioco in tendenza al momento.
        </div>
      ) : (
        <GameCarousel
          games={trendingData.games}
          onGameSelect={handleGameSelect}
          showDots
        />
      )}
    </section>
  );
}

// ============================================================================
// User Library Section (Authenticated Only, Lazy Loaded)
// ============================================================================

function UserLibrarySection() {
  const router = useRouter();
  const sectionRef = useRef<HTMLElement>(null);
  const isVisible = useIntersectionObserver(sectionRef);

  const { data: user } = useCurrentUser();
  const isAuthenticated = !!user;

  const {
    data: libraryData,
    isLoading,
    isError,
  } = useUserLibraryGames(8, isVisible && isAuthenticated);

  const handleGameSelect = useCallback(
    (game: CarouselGame) => {
      router.push(`/giochi/${game.id}`);
    },
    [router]
  );

  // Don't render for non-authenticated users
  if (!isAuthenticated) {
    return null;
  }

  // Don't render if user has no games in library
  if (!isLoading && (!libraryData?.games.length || isError)) {
    return null;
  }

  return (
    <section ref={sectionRef} className="py-8 md:py-12">
      <SectionHeader
        title="Dalla Tua Libreria"
        subtitle="I tuoi giochi preferiti"
        href="/library"
      />
      {!isVisible || isLoading ? (
        <GameCarouselSkeleton />
      ) : (
        <GameCarousel
          games={libraryData?.games ?? []}
          onGameSelect={handleGameSelect}
          showDots
        />
      )}
    </section>
  );
}

// ============================================================================
// Main Export
// ============================================================================

/**
 * GamesCarouselSection - All carousels for homepage
 *
 * Renders:
 * 1. Featured Games (always, above the fold)
 * 2. Trending Games (always, lazy loaded)
 * 3. User Library (authenticated only, lazy loaded)
 */
export function GamesCarouselSection() {
  return (
    <div className="relative">
      {/* Featured Games - Above the fold, always visible */}
      <FeaturedGamesSection />

      {/* Trending Games - Lazy loaded when scrolled into view */}
      <TrendingGamesSection />

      {/* User Library - Only for authenticated users, lazy loaded */}
      <UserLibrarySection />
    </div>
  );
}
