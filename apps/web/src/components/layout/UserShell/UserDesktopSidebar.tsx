'use client';

/**
 * UserDesktopSidebar — Desktop sidebar navigation for the user layout.
 *
 * Features:
 * - Hidden on mobile, visible on desktop (lg:flex)
 * - Collapsed (56px) -> expands to 180px on hover
 * - 4 icon buttons matching UserTabBar tabs and entity colors
 * - Active tab: left border accent + entity-colored icon
 */

import { BookOpen, Dice5, House, MessageCircle } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useNavigation, type NavTab } from '@/hooks/useNavigation';
import { cn } from '@/lib/utils';

import type { LucideIcon } from 'lucide-react';

interface SidebarTabConfig {
  id: NavTab;
  label: string;
  sectionTitle: string;
  icon: LucideIcon;
  /** CSS color value for active state */
  colorVar: string;
}

const SIDEBAR_TABS: SidebarTabConfig[] = [
  {
    id: 'home',
    label: 'Home',
    sectionTitle: 'Home',
    icon: House,
    colorVar: 'hsl(var(--primary))',
  },
  {
    id: 'library',
    label: 'Libreria',
    sectionTitle: 'Libreria',
    icon: BookOpen,
    colorVar: 'hsl(var(--color-entity-game))',
  },
  {
    id: 'play',
    label: 'Gioca',
    sectionTitle: 'Gioca',
    icon: Dice5,
    colorVar: 'hsl(var(--color-entity-session))',
  },
  {
    id: 'chat',
    label: 'Chat',
    sectionTitle: 'Chat',
    icon: MessageCircle,
    colorVar: 'hsl(var(--color-entity-chat))',
  },
];

export function UserDesktopSidebar() {
  const router = useRouter();
  const { activeTab, setActiveTab, setSectionTitle } = useNavigation();

  const handleTabClick = (tab: SidebarTabConfig) => {
    setActiveTab(tab.id);
    setSectionTitle(tab.sectionTitle);
    if (tab.id === 'home') {
      router.push('/dashboard');
    }
  };

  return (
    <aside
      className={cn(
        'hidden lg:flex flex-col',
        'w-14 hover:w-[180px]',
        'transition-[width] duration-300 ease-in-out',
        'border-r border-border/40 bg-background',
        'group overflow-hidden'
      )}
      data-testid="user-desktop-sidebar"
    >
      {/* Main navigation */}
      <nav className="flex-1 flex flex-col gap-1 py-4" aria-label="Desktop navigation">
        {SIDEBAR_TABS.map(tab => {
          const isActive = activeTab === tab.id;
          const Icon = tab.icon;

          return (
            <button
              key={tab.id}
              type="button"
              onClick={() => handleTabClick(tab)}
              className={cn(
                'relative flex items-center gap-3 px-4 py-3',
                'transition-colors duration-200',
                'hover:bg-muted/50',
                isActive ? 'text-foreground' : 'text-muted-foreground hover:text-foreground'
              )}
              aria-label={tab.label}
              aria-current={isActive ? 'page' : undefined}
            >
              {/* Active indicator — left border */}
              {isActive && (
                <div
                  className="absolute left-0 top-1/2 -translate-y-1/2 w-0.5 h-6 rounded-r-full"
                  style={{ backgroundColor: tab.colorVar }}
                />
              )}

              <Icon
                className="w-5 h-5 shrink-0"
                style={isActive ? { color: tab.colorVar } : undefined}
              />

              <span
                className={cn(
                  'text-sm font-medium whitespace-nowrap',
                  'opacity-0 group-hover:opacity-100',
                  'transition-opacity duration-300'
                )}
              >
                {tab.label}
              </span>
            </button>
          );
        })}
      </nav>
    </aside>
  );
}
