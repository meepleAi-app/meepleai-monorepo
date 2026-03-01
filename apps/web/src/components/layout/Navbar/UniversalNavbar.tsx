/**
 * UniversalNavbar - Issue #4936
 * Universal top navigation bar visible on all breakpoints.
 *
 * Desktop: Logo + Search bar (functional: games) + Notifications + ProfileBar (avatar + name)
 * Mobile:  Hamburger + Logo + Notifications + Avatar
 *
 * Replaces UnifiedHeader (mobile-only) and adds top nav on desktop.
 * Height: 56px (h-14)
 */

'use client';

import { useState, useEffect, useRef, useTransition } from 'react';

import { useQuery } from '@tanstack/react-query';
import { Search, X, Settings, Shield, LogOut, User, FileEdit } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { logoutAction } from '@/actions/auth';
import { MobileNavDrawer } from '@/components/layout/MobileNavDrawer';
import { NotificationBell } from '@/components/notifications';
import { MeepleLogo } from '@/components/ui/meeple/meeple-logo';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { ThemeToggle } from '@/components/ui/navigation/ThemeToggle';
import { Button } from '@/components/ui/primitives/button';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { gamesKeys } from '@/hooks/queries/useGames';
import { useNavigationItems } from '@/hooks/useNavigationItems';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';

export interface UniversalNavbarProps {
  className?: string;
}

/** Debounce hook */
function useDebounce<T>(value: T, delay: number): T {
  const [debouncedValue, setDebouncedValue] = useState<T>(value);
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedValue(value), delay);
    return () => clearTimeout(timer);
  }, [value, delay]);
  return debouncedValue;
}

/** Inline search bar with game results dropdown */
function NavbarSearch() {
  const [query, setQuery] = useState('');
  const [isOpen, setIsOpen] = useState(false);
  const debouncedQuery = useDebounce(query, 300);
  const wrapperRef = useRef<HTMLDivElement>(null);
  const router = useRouter();

  const { data, isLoading } = useQuery({
    queryKey: gamesKeys.list({ search: debouncedQuery }, undefined, 1, 6),
    queryFn: () => api.games.getAll({ search: debouncedQuery }, undefined, 1, 6),
    enabled: debouncedQuery.length >= 2,
    staleTime: 30 * 1000,
  });

  const results = data?.games ?? [];

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const handleSelect = (gameId: string) => {
    setQuery('');
    setIsOpen(false);
    router.push(`/games/${gameId}`);
  };

  return (
    <div ref={wrapperRef} className="relative w-full max-w-sm lg:max-w-md">
      <div className="relative flex items-center">
        <Search className="absolute left-3 h-4 w-4 text-muted-foreground pointer-events-none" aria-hidden="true" />
        <input
          type="search"
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            setIsOpen(e.target.value.length >= 2);
          }}
          onFocus={() => query.length >= 2 && setIsOpen(true)}
          onKeyDown={(e) => e.key === 'Escape' && (setQuery(''), setIsOpen(false))}
          placeholder="Cerca giochi..."
          aria-label="Cerca giochi nel catalogo"
          className={cn(
            'w-full h-9 pl-9 pr-8 rounded-lg text-sm',
            'bg-muted/60 border border-border/50',
            'placeholder:text-muted-foreground',
            'focus:outline-none focus:ring-2 focus:ring-primary/40 focus:bg-background',
            'transition-all duration-200'
          )}
        />
        {query && (
          <button
            onClick={() => { setQuery(''); setIsOpen(false); }}
            className="absolute right-2.5 text-muted-foreground hover:text-foreground"
            aria-label="Cancella ricerca"
          >
            <X className="h-3.5 w-3.5" />
          </button>
        )}
      </div>

      {isOpen && debouncedQuery.length >= 2 && (
        <div
          className={cn(
            'absolute top-full mt-1.5 w-full z-50',
            'rounded-xl border border-border/50 shadow-lg',
            'bg-popover/95 backdrop-blur-md',
            'py-1 overflow-hidden'
          )}
          role="listbox"
          aria-label="Risultati ricerca"
        >
          {isLoading ? (
            <div className="px-3 py-2 text-sm text-muted-foreground">Ricerca in corso...</div>
          ) : results.length === 0 ? (
            <div className="px-3 py-2 text-sm text-muted-foreground">
              Nessun risultato per &ldquo;{debouncedQuery}&rdquo;
            </div>
          ) : (
            <>
              {results.map((game) => (
                <button
                  key={game.id}
                  onClick={() => handleSelect(game.id)}
                  role="option"
                  className={cn(
                    'w-full flex items-center gap-3 px-3 py-2 text-left',
                    'text-sm hover:bg-accent hover:text-accent-foreground',
                    'transition-colors duration-100 cursor-pointer'
                  )}
                >
                  <span className="truncate font-medium">{game.title}</span>
                  {game.yearPublished && (
                    <span className="ml-auto shrink-0 text-xs text-muted-foreground">
                      {game.yearPublished}
                    </span>
                  )}
                </button>
              ))}
              <div className="border-t border-border/30 mt-1 pt-1 px-3 pb-1">
                <Link
                  href={`/games?search=${encodeURIComponent(debouncedQuery)}`}
                  className="text-xs text-primary hover:underline"
                  onClick={() => setIsOpen(false)}
                >
                  Vedi tutti i risultati →
                </Link>
              </div>
            </>
          )}
        </div>
      )}
    </div>
  );
}

/** Profile dropdown: avatar + name (desktop) or avatar-only (mobile) */
function ProfileBar() {
  const router = useRouter();
  const { data: user } = useCurrentUser();
  const [isLoggingOut, startTransition] = useTransition();

  const isAdmin = user?.role?.toLowerCase() === 'admin' || user?.role?.toLowerCase() === 'superadmin';
  const isEditor = user?.role?.toLowerCase() === 'editor' || isAdmin;
  const userInitial =
    user?.displayName?.charAt(0).toUpperCase() || user?.email?.charAt(0).toUpperCase() || 'U';

  const handleLogout = () => {
    startTransition(async () => {
      const result = await logoutAction();
      if (result.success) router.push('/login');
    });
  };

  if (!user) {
    return (
      <Link href="/login">
        <Button variant="default" size="sm">Accedi</Button>
      </Link>
    );
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          className="flex items-center gap-2 px-2 h-9 hover:bg-accent rounded-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
          aria-label="Menu utente"
          data-testid="universal-navbar-profile-trigger"
        >
          <div className="w-7 h-7 rounded-full bg-primary/20 flex items-center justify-center shrink-0">
            <span className="text-xs font-semibold text-primary">{userInitial}</span>
          </div>
          <span className="hidden md:inline text-sm font-medium max-w-[120px] truncate">
            {user.displayName || user.email || 'Utente'}
          </span>
        </Button>
      </DropdownMenuTrigger>

      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuLabel>
          <div className="flex flex-col space-y-0.5">
            <p className="text-sm font-medium leading-none">{user.displayName || 'Utente'}</p>
            <p className="text-xs leading-none text-muted-foreground">{user.email}</p>
          </div>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />

        <DropdownMenuItem asChild data-testid="navbar-profile-item">
          <Link href="/profile" className="flex items-center gap-2 cursor-pointer">
            <User className="h-4 w-4" /><span>Profilo</span>
          </Link>
        </DropdownMenuItem>
        <DropdownMenuSeparator />

        {isEditor && (
          <>
            <DropdownMenuItem asChild data-testid="navbar-editor-item">
              <Link href="/editor" className="flex items-center gap-2 cursor-pointer">
                <FileEdit className="h-4 w-4" /><span>Editor Panel</span>
              </Link>
            </DropdownMenuItem>
            <DropdownMenuSeparator />
          </>
        )}

        {isAdmin && (
          <>
            <DropdownMenuItem asChild data-testid="navbar-admin-item">
              <Link href="/admin/overview" className="flex items-center gap-2 cursor-pointer">
                <Shield className="h-4 w-4" /><span>Admin Panel</span>
              </Link>
            </DropdownMenuItem>
            <DropdownMenuSeparator />
          </>
        )}

        <DropdownMenuItem asChild data-testid="navbar-settings-item">
          <Link href="/settings" className="flex items-center gap-2 cursor-pointer">
            <Settings className="h-4 w-4" /><span>Impostazioni</span>
          </Link>
        </DropdownMenuItem>
        <DropdownMenuSeparator />

        <div className="px-2 py-1.5">
          <ThemeToggle showLabel size="sm" className="w-full justify-start" />
        </div>
        <DropdownMenuSeparator />

        <DropdownMenuItem
          onClick={handleLogout}
          disabled={isLoggingOut}
          className="flex items-center gap-2 cursor-pointer text-destructive focus:text-destructive"
          data-testid="navbar-logout-item"
        >
          <LogOut className="h-4 w-4" />
          <span>{isLoggingOut ? 'Disconnessione...' : 'Esci'}</span>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

/**
 * UniversalNavbar
 * Fixed top bar visible on ALL breakpoints (h-14 = 56px).
 */
export function UniversalNavbar({ className }: UniversalNavbarProps) {
  const { isAuthenticated, isAuthLoading } = useNavigationItems();
  const [isScrolled, setIsScrolled] = useState(false);

  useEffect(() => {
    const handleScroll = () => setIsScrolled(window.scrollY > 8);
    window.addEventListener('scroll', handleScroll, { passive: true });
    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  return (
    <header
      className={cn(
        'fixed top-0 left-0 right-0 z-[110] w-full h-14 border-b',
        'bg-background/95 backdrop-blur-[16px] backdrop-saturate-[180%]',
        'dark:bg-card/95 dark:backdrop-blur-sm',
        'border-border/50 dark:border-border/30',
        'transition-shadow duration-200',
        isScrolled && 'shadow-sm dark:shadow-md',
        className
      )}
      data-testid="universal-navbar"
    >
      <div className="flex h-full items-center justify-between px-4 gap-3">
        {/* Left: Mobile hamburger + Logo */}
        <div className="flex items-center gap-2 shrink-0">
          {isAuthenticated && !isAuthLoading && (
            <div className="md:hidden">
              <MobileNavDrawer />
            </div>
          )}
          <Link href="/dashboard" className="flex items-center gap-2" aria-label="MeepleAI — vai alla dashboard">
            <MeepleLogo variant="icon" size="sm" />
            <span className="hidden sm:inline font-quicksand font-bold text-base">MeepleAI</span>
          </Link>
        </div>

        {/* Center: Search (desktop) */}
        {isAuthenticated && !isAuthLoading && (
          <div className="hidden md:flex flex-1 justify-center px-4 max-w-xl mx-auto w-full">
            <NavbarSearch />
          </div>
        )}

        {/* Right: Notifications + Profile */}
        <div className="flex items-center gap-1 shrink-0">
          {isAuthenticated && !isAuthLoading && <NotificationBell />}
          <ProfileBar />
        </div>
      </div>
    </header>
  );
}
