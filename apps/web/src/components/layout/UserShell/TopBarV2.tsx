'use client';

import { Menu, Search } from 'lucide-react';
import Link from 'next/link';

import { UserMenuDropdown } from '@/components/layout/UserMenuDropdown';
import { ThemeToggle } from '@/components/ui/navigation/ThemeToggle';
import { Button } from '@/components/ui/primitives/button';

interface TopBarV2Props {
  onHamburgerClick: () => void;
  onSearchClick?: () => void;
  adminMode?: boolean;
}

export function TopBarV2({ onHamburgerClick, onSearchClick, adminMode }: TopBarV2Props) {
  return (
    <header
      data-testid="top-bar"
      className="sticky top-0 z-40 h-12 flex items-center justify-between px-3"
      style={{
        backdropFilter: 'blur(16px)',
        WebkitBackdropFilter: 'blur(16px)',
        backgroundColor: 'color-mix(in srgb, var(--bg-base) 80%, transparent)',
        borderBottom: '1px solid color-mix(in srgb, var(--border) 50%, transparent)',
      }}
    >
      {/* Left side */}
      <div className="flex items-center gap-2">
        <Button
          variant="ghost"
          size="icon"
          aria-label="Menu navigazione"
          onClick={onHamburgerClick}
          className="shrink-0"
        >
          <Menu className="h-5 w-5" />
        </Button>

        <Link href="/dashboard" aria-label="MeepleAI" className="flex items-center gap-1.5">
          <span aria-hidden="true">🎲</span>
          <span className="font-semibold text-sm font-[family-name:var(--font-quicksand)]">
            MeepleAI
          </span>
          {adminMode && (
            <span className="ml-1 text-[10px] font-semibold px-1.5 py-0.5 rounded bg-destructive/10 text-destructive leading-none">
              Admin
            </span>
          )}
        </Link>
      </div>

      {/* Right side */}
      <div className="flex items-center gap-1">
        <Button variant="ghost" size="icon" aria-label="Cerca" onClick={onSearchClick}>
          <Search className="h-5 w-5" />
        </Button>

        <ThemeToggle size="sm" />

        <UserMenuDropdown />
      </div>
    </header>
  );
}
