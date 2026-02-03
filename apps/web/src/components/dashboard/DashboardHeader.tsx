/**
 * DashboardHeader - Global Search and Navigation Header
 * Issue #3286 - User Dashboard Layout System
 *
 * Features:
 * - Time-based greeting (Buongiorno/Buon pomeriggio/Buonasera)
 * - Global search with Cmd+K shortcut
 * - Quick filter access
 * - Notification badge
 * - Responsive design
 *
 * @example
 * ```tsx
 * <DashboardHeader />
 * ```
 */

'use client';

import { useState, useEffect, useCallback, useRef } from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import { Search, Bell, Filter, X, Command, Loader2 } from 'lucide-react';

import { useLayout } from '@/components/layout/LayoutProvider';
import { Button } from '@/components/ui/primitives/button';
import { useGlobalSearch } from '@/hooks/useGlobalSearch';
import { cn } from '@/lib/utils';

// ============================================================================
// DashboardHeader Component
// ============================================================================

export function DashboardHeader() {
  const { responsive } = useLayout();
  const { isMobile } = responsive;

  const [isSearchOpen, setIsSearchOpen] = useState(false);
  const [greeting, setGreeting] = useState('');
  const searchInputRef = useRef<HTMLInputElement>(null);

  const {
    query,
    setQuery,
    results,
    isLoading,
    recentSearches,
    clearSearch,
  } = useGlobalSearch();

  // Set greeting based on time of day
  useEffect(() => {
    const hour = new Date().getHours();
    if (hour >= 5 && hour < 12) {
      setGreeting('Buongiorno');
    } else if (hour >= 12 && hour < 18) {
      setGreeting('Buon pomeriggio');
    } else {
      setGreeting('Buonasera');
    }
  }, []);

  // Handle keyboard shortcut (Cmd+K / Ctrl+K)
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault();
        setIsSearchOpen(true);
        setTimeout(() => searchInputRef.current?.focus(), 100);
      }
      if (e.key === 'Escape' && isSearchOpen) {
        setIsSearchOpen(false);
        clearSearch();
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [isSearchOpen, clearSearch]);

  // Focus search input when opened
  useEffect(() => {
    if (isSearchOpen) {
      setTimeout(() => searchInputRef.current?.focus(), 100);
    }
  }, [isSearchOpen]);

  const handleSearchClose = useCallback(() => {
    setIsSearchOpen(false);
    clearSearch();
  }, [clearSearch]);

  return (
    <>
      {/* Header Bar */}
      <header className="sticky top-16 z-20 border-b border-border/50 bg-background/80 backdrop-blur-md">
        <div className="mx-auto max-w-7xl px-4 py-4 sm:px-6 lg:px-8">
          <div className="flex items-center justify-between gap-4">
            {/* Left: Greeting */}
            <div className="min-w-0 flex-1">
              <motion.h1
                initial={{ opacity: 0, y: -10 }}
                animate={{ opacity: 1, y: 0 }}
                className="truncate font-quicksand text-xl font-bold tracking-tight sm:text-2xl"
              >
                {greeting}!
              </motion.h1>
              <p className="mt-0.5 text-sm text-muted-foreground">
                Cosa vuoi fare oggi?
              </p>
            </div>

            {/* Right: Search + Actions */}
            <div className="flex items-center gap-2">
              {/* Search Button (Desktop: Full bar, Mobile: Icon) */}
              {isMobile ? (
                <Button
                  variant="ghost"
                  size="icon"
                  onClick={() => setIsSearchOpen(true)}
                  className="h-10 w-10"
                  aria-label="Cerca"
                >
                  <Search className="h-5 w-5" />
                </Button>
              ) : (
                <button
                  onClick={() => setIsSearchOpen(true)}
                  className={cn(
                    'flex h-10 items-center gap-3 rounded-xl border border-border/60 bg-muted/50 px-4',
                    'text-muted-foreground transition-all duration-200',
                    'hover:border-primary/30 hover:bg-muted hover:shadow-sm',
                    'focus:outline-none focus:ring-2 focus:ring-primary/20'
                  )}
                >
                  <Search className="h-4 w-4" />
                  <span className="text-sm">Cerca giochi, gruppi...</span>
                  <kbd className="ml-4 hidden rounded-md border border-border/60 bg-background px-1.5 py-0.5 text-xs font-medium text-muted-foreground sm:inline-block">
                    <Command className="mr-0.5 inline h-3 w-3" />K
                  </kbd>
                </button>
              )}

              {/* Filter Button */}
              <Button
                variant="ghost"
                size="icon"
                className="h-10 w-10 text-muted-foreground hover:text-foreground"
                aria-label="Filtri"
              >
                <Filter className="h-5 w-5" />
              </Button>

              {/* Notifications */}
              <Button
                variant="ghost"
                size="icon"
                className="relative h-10 w-10 text-muted-foreground hover:text-foreground"
                aria-label="Notifiche"
              >
                <Bell className="h-5 w-5" />
                {/* Badge */}
                <span className="absolute -right-0.5 -top-0.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-destructive px-1 text-[10px] font-bold text-destructive-foreground">
                  3
                </span>
              </Button>
            </div>
          </div>
        </div>
      </header>

      {/* Search Modal Overlay */}
      <AnimatePresence>
        {isSearchOpen && (
          <>
            {/* Backdrop */}
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              onClick={handleSearchClose}
              className="fixed inset-0 z-50 bg-background/80 backdrop-blur-sm"
            />

            {/* Search Modal */}
            <motion.div
              initial={{ opacity: 0, y: -20, scale: 0.95 }}
              animate={{ opacity: 1, y: 0, scale: 1 }}
              exit={{ opacity: 0, y: -20, scale: 0.95 }}
              transition={{ duration: 0.2, ease: [0.4, 0, 0.2, 1] }}
              className={cn(
                'fixed left-1/2 top-20 z-50 w-full max-w-xl -translate-x-1/2 px-4',
                isMobile && 'top-4'
              )}
            >
              <div className="overflow-hidden rounded-2xl border border-border bg-card shadow-2xl shadow-primary/10">
                {/* Search Input */}
                <div className="flex items-center gap-3 border-b border-border/50 px-4 py-3">
                  <Search className="h-5 w-5 text-muted-foreground" />
                  <input
                    ref={searchInputRef}
                    type="text"
                    placeholder="Cerca giochi, gruppi, partite..."
                    value={query}
                    onChange={(e) => setQuery(e.target.value)}
                    className={cn(
                      'flex-1 bg-transparent text-base outline-none placeholder:text-muted-foreground',
                      'font-nunito'
                    )}
                  />
                  {isLoading && <Loader2 className="h-5 w-5 animate-spin text-primary" />}
                  {query && !isLoading && (
                    <button
                      onClick={() => setQuery('')}
                      className="rounded-full p-1 text-muted-foreground hover:bg-muted hover:text-foreground"
                    >
                      <X className="h-4 w-4" />
                    </button>
                  )}
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={handleSearchClose}
                    className="text-muted-foreground"
                  >
                    <kbd className="text-xs">Esc</kbd>
                  </Button>
                </div>

                {/* Search Results / Recent Searches */}
                <div className="max-h-[60vh] overflow-y-auto p-2">
                  {/* Show recent searches when no query */}
                  {!query && recentSearches.length > 0 && (
                    <div className="px-2 py-2">
                      <p className="mb-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                        Ricerche recenti
                      </p>
                      <div className="space-y-1">
                        {recentSearches.map((search) => (
                          <button
                            key={search}
                            onClick={() => setQuery(search)}
                            className="flex w-full items-center gap-3 rounded-lg px-3 py-2 text-left text-sm transition-colors hover:bg-muted"
                          >
                            <Search className="h-4 w-4 text-muted-foreground" />
                            {search}
                          </button>
                        ))}
                      </div>
                    </div>
                  )}

                  {/* Show results */}
                  {query && results.length > 0 && (
                    <div className="space-y-1">
                      {results.map((result, index) => (
                        <motion.a
                          key={result.id}
                          href={result.href}
                          initial={{ opacity: 0, y: 10 }}
                          animate={{ opacity: 1, y: 0 }}
                          transition={{ delay: index * 0.03 }}
                          className="flex items-center gap-3 rounded-lg px-3 py-2.5 transition-colors hover:bg-muted"
                        >
                          {result.imageUrl ? (
                            /* eslint-disable-next-line @next/next/no-img-element -- External search result URL */
                            <img
                              src={result.imageUrl}
                              alt=""
                              className="h-10 w-10 rounded-lg object-cover"
                            />
                          ) : (
                            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10 text-primary">
                              <Search className="h-5 w-5" />
                            </div>
                          )}
                          <div className="min-w-0 flex-1">
                            <p className="truncate font-medium">{result.title}</p>
                            {result.subtitle && (
                              <p className="truncate text-sm text-muted-foreground">
                                {result.subtitle}
                              </p>
                            )}
                          </div>
                          <span className="shrink-0 rounded-md bg-muted px-2 py-0.5 text-xs text-muted-foreground">
                            {result.type === 'game' && 'Gioco'}
                            {result.type === 'document' && 'Documento'}
                            {result.type === 'session' && 'Sessione'}
                          </span>
                        </motion.a>
                      ))}
                    </div>
                  )}

                  {/* No results */}
                  {query && !isLoading && results.length === 0 && (
                    <div className="py-8 text-center">
                      <Search className="mx-auto h-10 w-10 text-muted-foreground/50" />
                      <p className="mt-3 text-muted-foreground">
                        Nessun risultato per &quot;{query}&quot;
                      </p>
                    </div>
                  )}

                  {/* Empty state */}
                  {!query && recentSearches.length === 0 && (
                    <div className="py-8 text-center">
                      <Search className="mx-auto h-10 w-10 text-muted-foreground/50" />
                      <p className="mt-3 text-muted-foreground">
                        Inizia a digitare per cercare
                      </p>
                    </div>
                  )}
                </div>

                {/* Quick Filters */}
                <div className="border-t border-border/50 px-4 py-3">
                  <p className="mb-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                    Filtra per
                  </p>
                  <div className="flex flex-wrap gap-2">
                    {['Giochi', 'Gruppi', 'Partite', 'Preferiti', '1-4 giocatori'].map((filter) => (
                      <button
                        key={filter}
                        className={cn(
                          'rounded-full border border-border/60 px-3 py-1 text-xs transition-colors',
                          'hover:border-primary/30 hover:bg-primary/5 hover:text-primary'
                        )}
                      >
                        {filter}
                      </button>
                    ))}
                  </div>
                </div>
              </div>
            </motion.div>
          </>
        )}
      </AnimatePresence>
    </>
  );
}

export default DashboardHeader;
