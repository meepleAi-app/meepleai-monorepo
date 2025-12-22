/**
 * PublicHeader Component - Issue #2230
 *
 * Header per il layout pubblico con navigation responsive.
 * Features:
 * - Logo MeepleAI con link a home
 * - Navigation menu desktop (Home, Giochi, Chat, Dashboard)
 * - User menu dropdown (quando autenticato)
 * - Theme switcher
 * - Mobile hamburger menu con Sheet
 * - Sticky header on scroll
 * - Dark mode support
 */

'use client';

import { useState, useEffect } from 'react';

import {
  MenuIcon,
  UserIcon,
  LogOutIcon,
  HomeIcon,
  GamepadIcon,
  MessageSquareIcon,
  LayoutDashboardIcon,
} from 'lucide-react';
import Image from 'next/image';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { ThemeSwitcher } from '@/components/layout/ThemeSwitcher';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { MeepleLogo } from '@/components/ui/meeple/meeple-logo';
import {
  Sheet,
  SheetContent,
  SheetTrigger,
  SheetTitle,
  SheetDescription,
} from '@/components/ui/sheet';
import { cn } from '@/lib/utils';

export interface PublicUser {
  name: string;
  email: string;
  avatar?: string;
}

export interface PublicHeaderProps {
  /** Current user (undefined if not authenticated) */
  user?: PublicUser;
  /** Logout callback */
  onLogout?: () => void;
  /** Additional className */
  className?: string;
}

interface NavItem {
  href: string;
  label: string;
  icon: typeof HomeIcon;
}

const NAV_ITEMS: NavItem[] = [
  { href: '/', label: 'Home', icon: HomeIcon },
  { href: '/games', label: 'Giochi', icon: GamepadIcon },
  { href: '/chat', label: 'Chat', icon: MessageSquareIcon },
  { href: '/dashboard', label: 'Dashboard', icon: LayoutDashboardIcon },
];

export function PublicHeader({ user, onLogout, className }: PublicHeaderProps) {
  const [mobileOpen, setMobileOpen] = useState(false);
  const [isScrolled, setIsScrolled] = useState(false);
  const pathname = usePathname();

  // Handle scroll for sticky header effect
  useEffect(() => {
    const handleScroll = () => {
      setIsScrolled(window.scrollY > 10);
    };

    window.addEventListener('scroll', handleScroll);
    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  // Desktop Navigation
  const DesktopNav = () => (
    <nav className="hidden md:flex items-center gap-1" aria-label="Main navigation">
      {NAV_ITEMS.map(item => {
        const Icon = item.icon;
        const isActive = pathname === item.href;

        return (
          <Link
            key={item.href}
            href={item.href}
            className={cn(
              'flex items-center gap-2 px-3 py-2 rounded-md text-sm font-medium transition-colors',
              'hover:bg-accent hover:text-accent-foreground',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
              isActive ? 'bg-accent text-accent-foreground' : 'text-muted-foreground'
            )}
            aria-current={isActive ? 'page' : undefined}
          >
            <Icon className="h-4 w-4" aria-hidden="true" />
            <span>{item.label}</span>
          </Link>
        );
      })}
    </nav>
  );

  // User Menu
  const UserMenu = () => {
    if (!user) {
      return (
        <Link href="/login">
          <Button variant="default" size="sm">
            Accedi
          </Button>
        </Link>
      );
    }

    return (
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="ghost" size="icon" className="rounded-full" aria-label="User menu">
            {user.avatar ? (
              <Image
                src={user.avatar}
                alt={user.name}
                width={32}
                height={32}
                className="h-8 w-8 rounded-full object-cover"
              />
            ) : (
              <UserIcon className="h-5 w-5" />
            )}
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-56">
          <DropdownMenuLabel>
            <div className="flex flex-col space-y-1">
              <p className="text-sm font-medium leading-none">{user.name}</p>
              <p className="text-xs leading-none text-muted-foreground">{user.email}</p>
            </div>
          </DropdownMenuLabel>
          <DropdownMenuSeparator />
          <DropdownMenuItem asChild>
            <Link href="/profile" className="cursor-pointer">
              <UserIcon className="mr-2 h-4 w-4" />
              <span>Profilo</span>
            </Link>
          </DropdownMenuItem>
          <DropdownMenuItem asChild>
            <Link href="/dashboard" className="cursor-pointer">
              <LayoutDashboardIcon className="mr-2 h-4 w-4" />
              <span>Dashboard</span>
            </Link>
          </DropdownMenuItem>
          <DropdownMenuSeparator />
          <DropdownMenuItem
            onClick={onLogout}
            className="cursor-pointer text-destructive focus:text-destructive"
          >
            <LogOutIcon className="mr-2 h-4 w-4" />
            <span>Logout</span>
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    );
  };

  // Mobile Sheet Navigation
  const MobileNav = () => (
    <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
      <SheetTrigger asChild>
        <Button variant="ghost" size="icon" className="md:hidden" aria-label="Open navigation menu">
          <MenuIcon className="h-5 w-5" />
        </Button>
      </SheetTrigger>
      <SheetContent side="left" className="w-72">
        <SheetTitle className="text-left mb-4">Menu</SheetTitle>
        <SheetDescription className="sr-only">Main navigation menu</SheetDescription>

        <nav className="flex flex-col gap-2" aria-label="Mobile navigation">
          {NAV_ITEMS.map(item => {
            const Icon = item.icon;
            const isActive = pathname === item.href;

            return (
              <Link
                key={item.href}
                href={item.href}
                onClick={() => setMobileOpen(false)}
                className={cn(
                  'flex items-center gap-3 px-3 py-3 rounded-md text-sm font-medium transition-colors',
                  'hover:bg-accent hover:text-accent-foreground',
                  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                  isActive ? 'bg-accent text-accent-foreground' : 'text-muted-foreground'
                )}
                aria-current={isActive ? 'page' : undefined}
              >
                <Icon className="h-5 w-5" aria-hidden="true" />
                <span>{item.label}</span>
              </Link>
            );
          })}
        </nav>

        {user && (
          <>
            <div className="my-4 border-t" />
            <div className="space-y-2">
              <div className="px-3 py-2">
                <p className="text-sm font-medium">{user.name}</p>
                <p className="text-xs text-muted-foreground">{user.email}</p>
              </div>
              <Link
                href="/profile"
                onClick={() => setMobileOpen(false)}
                className="flex items-center gap-3 px-3 py-3 rounded-md text-sm font-medium hover:bg-accent"
              >
                <UserIcon className="h-5 w-5" />
                <span>Profilo</span>
              </Link>
              <button
                onClick={() => {
                  setMobileOpen(false);
                  onLogout?.();
                }}
                className="w-full flex items-center gap-3 px-3 py-3 rounded-md text-sm font-medium hover:bg-accent text-destructive"
              >
                <LogOutIcon className="h-5 w-5" />
                <span>Logout</span>
              </button>
            </div>
          </>
        )}
      </SheetContent>
    </Sheet>
  );

  return (
    <header
      className={cn(
        'sticky top-0 z-50 w-full border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60',
        'transition-shadow duration-200',
        isScrolled && 'shadow-sm',
        className
      )}
    >
      <div className="container mx-auto flex h-16 items-center justify-between px-4 sm:px-6 lg:px-8">
        {/* Left: Mobile Menu + Logo */}
        <div className="flex items-center gap-4">
          <MobileNav />
          <Link href="/" className="flex items-center" aria-label="MeepleAI Home">
            <MeepleLogo variant="full" size="sm" />
          </Link>
        </div>

        {/* Center: Desktop Navigation */}
        <DesktopNav />

        {/* Right: Theme + User Menu */}
        <div className="flex items-center gap-2">
          <ThemeSwitcher />
          <UserMenu />
        </div>
      </div>
    </header>
  );
}
