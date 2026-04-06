'use client';

import { useState } from 'react';

import { LogOut, Menu, Settings, User, X } from 'lucide-react';
import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';

import { useAuth } from '@/components/auth/AuthProvider';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/data-display/avatar';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

const NAV_LINKS = [
  { label: 'Dashboard', href: '/dashboard' },
  { label: 'Libreria', href: '/library' },
  { label: 'Sessioni', href: '/sessions' },
  { label: 'AI Chat', href: '/chat' },
] as const;

export function AppNavbar() {
  const pathname = usePathname();
  const router = useRouter();
  const { user, logout } = useAuth();
  const [mobileOpen, setMobileOpen] = useState(false);
  const [dropdownOpen, setDropdownOpen] = useState(false);

  const initials =
    user?.displayName
      ?.split(' ')
      .map(n => n[0])
      .join('')
      .toUpperCase()
      .slice(0, 2) ?? 'U';

  async function handleLogout() {
    setDropdownOpen(false);
    await logout();
    router.push('/login');
  }

  return (
    <header className="sticky top-0 z-50 h-[52px] bg-card/95 backdrop-blur-md border-b border-border/50">
      <div className="h-full max-w-[1400px] mx-auto px-4 flex items-center justify-between">
        {/* Logo */}
        <div className="flex items-center gap-3 shrink-0">
          <div className="w-8 h-8">
            <svg viewBox="0 0 32 32" className="w-full h-full">
              <defs>
                <linearGradient id="meepleLogoGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                  <stop offset="0%" stopColor="hsl(var(--e-game))" />
                  <stop offset="100%" stopColor="hsl(var(--e-player))" />
                </linearGradient>
              </defs>
              <path d="M16 2 L28 9 L28 23 L16 30 L4 23 L4 9 Z" fill="url(#meepleLogoGrad)" />
              <path d="M12 12 L16 10 L20 12 L20 18 L16 20 L12 18 Z" fill="white" opacity="0.9" />
            </svg>
          </div>
          <span className="font-bold text-[17px] text-foreground leading-none">
            Meeple<span className="text-primary">AI</span>
          </span>
        </div>

        {/* Nav links — desktop */}
        <nav className="hidden sm:flex items-center gap-1">
          {NAV_LINKS.map(link => (
            <Link
              key={link.href}
              href={link.href}
              className={cn(
                'px-4 py-1.5 rounded-lg font-semibold text-sm transition-colors',
                pathname.startsWith(link.href)
                  ? 'bg-secondary text-foreground'
                  : 'text-muted-foreground hover:text-foreground hover:bg-secondary/50'
              )}
            >
              {link.label}
            </Link>
          ))}
        </nav>

        {/* Right: Avatar dropdown + mobile toggle */}
        <div className="flex items-center gap-2">
          {/* Avatar with dropdown */}
          <div className="relative">
            <button
              onClick={() => setDropdownOpen(v => !v)}
              aria-label="Menu utente"
              aria-expanded={dropdownOpen}
              className="rounded-full focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              <Avatar className="w-8 h-8 border-2 border-[hsl(var(--e-player))]/50">
                <AvatarImage src="" alt={user?.displayName ?? 'User'} />
                <AvatarFallback className="bg-[hsl(var(--e-player))] text-white text-xs font-bold">
                  {initials}
                </AvatarFallback>
              </Avatar>
            </button>

            {dropdownOpen && (
              <>
                {/* Backdrop — closes dropdown on outside click */}
                <div
                  className="fixed inset-0 z-40"
                  onClick={() => setDropdownOpen(false)}
                  aria-hidden="true"
                />
                {/* Dropdown panel */}
                <div className="absolute right-0 top-full mt-2 w-48 z-50 bg-card border border-border rounded-xl shadow-xl py-1 overflow-hidden">
                  <div className="px-3 py-2 border-b border-border/50">
                    <p className="text-xs font-quicksand font-bold text-foreground truncate">
                      {user?.displayName ?? 'Utente'}
                    </p>
                    <p className="text-[11px] text-muted-foreground truncate">
                      {user?.email ?? ''}
                    </p>
                  </div>
                  <button
                    onClick={() => {
                      setDropdownOpen(false);
                      router.push('/profile');
                    }}
                    className="w-full flex items-center gap-2.5 px-3 py-2 text-sm text-foreground hover:bg-muted/50 transition-colors font-nunito"
                  >
                    <User className="w-4 h-4 text-muted-foreground" />
                    Profilo
                  </button>
                  <button
                    onClick={() => {
                      setDropdownOpen(false);
                      router.push('/settings');
                    }}
                    className="w-full flex items-center gap-2.5 px-3 py-2 text-sm text-foreground hover:bg-muted/50 transition-colors font-nunito"
                  >
                    <Settings className="w-4 h-4 text-muted-foreground" />
                    Impostazioni
                  </button>
                  <div className="border-t border-border/50 my-1" />
                  <button
                    onClick={handleLogout}
                    className="w-full flex items-center gap-2.5 px-3 py-2 text-sm hover:bg-muted/50 transition-colors font-nunito"
                    style={{ color: 'hsl(var(--e-event))' }}
                  >
                    <LogOut className="w-4 h-4" />
                    Esci
                  </button>
                </div>
              </>
            )}
          </div>

          {/* Mobile hamburger */}
          <Button
            variant="ghost"
            size="icon"
            className="sm:hidden w-9 h-9 text-muted-foreground"
            onClick={() => setMobileOpen(!mobileOpen)}
            aria-label="Menu"
          >
            {mobileOpen ? <X className="w-5 h-5" /> : <Menu className="w-5 h-5" />}
          </Button>
        </div>
      </div>

      {/* Mobile menu */}
      {mobileOpen && (
        <div className="sm:hidden absolute top-[52px] left-0 right-0 bg-card border-b border-border p-4 z-50">
          <nav className="flex flex-col gap-1">
            {NAV_LINKS.map(link => (
              <Link
                key={link.href}
                href={link.href}
                onClick={() => setMobileOpen(false)}
                className={cn(
                  'px-4 py-2 rounded-lg font-semibold text-sm',
                  pathname.startsWith(link.href)
                    ? 'bg-secondary text-foreground'
                    : 'text-muted-foreground hover:text-foreground hover:bg-secondary/50'
                )}
              >
                {link.label}
              </Link>
            ))}
          </nav>
        </div>
      )}
    </header>
  );
}
