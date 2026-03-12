'use client';

/**
 * NavbarMobileDrawer — Mobile navigation drawer for the 3-section Navbar
 * Issue #5036 — Navbar Component
 *
 * Slide-in Sheet from the left with 3 collapsible nav sections:
 * Tool / Discover / Admin (role-gated)
 *
 * Does NOT depend on LayoutProvider — state is managed by the parent Navbar.
 */

import { useState, useTransition } from 'react';

import {
  BookOpen,
  MessageSquare,
  Gamepad2,
  Clock,
  BarChart3,
  Users,
  Lightbulb,
  Globe,
  Shield,
  Bot,
  FolderOpen,
  Activity,
  Settings,
  LogOut,
  LogIn,
  UserPlus,
  ChevronDown,
} from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { logoutAction } from '@/actions/auth';
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible';
import { Separator } from '@/components/ui/navigation/separator';
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/navigation/sheet';
import { ThemeToggle } from '@/components/ui/navigation/ThemeToggle';
import { Button } from '@/components/ui/primitives/button';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { cn } from '@/lib/utils';

import { Logo } from '@/components/layout/TopNavbar/Logo';

// ─── Nav Section Items ───────────────────────────────────────────────────────

interface DrawerItem {
  id: string;
  label: string;
  href: string;
  icon: React.ElementType;
}

const TOOL_ITEMS: DrawerItem[] = [
  { id: 'library', label: 'Libreria', href: '/library', icon: BookOpen },
  { id: 'agents', label: 'Agenti', href: '/agents', icon: Bot },
  { id: 'chat', label: 'Chat', href: '/chat', icon: MessageSquare },
  { id: 'sessions', label: 'Sessioni', href: '/sessions', icon: Gamepad2 },
  { id: 'play-records', label: 'Partite', href: '/play-records', icon: BarChart3 },
];

const DISCOVER_ITEMS: DrawerItem[] = [
  { id: 'catalog', label: 'Catalogo', href: '/games', icon: Gamepad2 },
  { id: 'proposals', label: 'Proposte', href: '/proposals', icon: Lightbulb },
  { id: 'community', label: 'Community', href: '/community', icon: Globe },
];

const ADMIN_ITEMS: DrawerItem[] = [
  { id: 'users', label: 'Utenti', href: '/admin/users', icon: Users },
  { id: 'ai', label: 'Intelligenza Artificiale', href: '/admin/agents', icon: Bot },
  { id: 'content', label: 'Contenuti', href: '/admin/shared-games', icon: FolderOpen },
  { id: 'analytics', label: 'Analytics', href: '/admin/overview', icon: BarChart3 },
  { id: 'config', label: 'Configurazione', href: '/admin/config', icon: Settings },
  { id: 'monitor', label: 'Monitor', href: '/admin/monitor', icon: Activity },
];

// ─── DrawerSection ─────────────────────────────────────────────────────────

interface DrawerSectionProps {
  label: string;
  icon: React.ElementType;
  items: DrawerItem[];
  onNavigate: () => void;
  defaultOpen?: boolean;
}

function DrawerSection({
  label,
  icon: SectionIcon,
  items,
  onNavigate,
  defaultOpen = true,
}: DrawerSectionProps) {
  const [open, setOpen] = useState(defaultOpen);

  return (
    <Collapsible open={open} onOpenChange={setOpen}>
      <CollapsibleTrigger
        className={cn(
          'flex w-full items-center justify-between px-4 py-2.5',
          'text-xs font-semibold uppercase tracking-wider text-muted-foreground',
          'hover:text-foreground transition-colors duration-150',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring rounded-md'
        )}
        aria-expanded={open}
      >
        <span className="flex items-center gap-2">
          <SectionIcon className="h-3.5 w-3.5" aria-hidden="true" />
          {label}
        </span>
        <ChevronDown
          className={cn(
            'h-3.5 w-3.5 transition-transform duration-200',
            open && 'rotate-180'
          )}
          aria-hidden="true"
        />
      </CollapsibleTrigger>

      <CollapsibleContent>
        <ul role="list" className="mt-0.5 mb-2 space-y-0.5 px-2">
          {items.map((item) => {
            const Icon = item.icon;
            return (
              <li key={item.id}>
                <Link
                  href={item.href}
                  onClick={onNavigate}
                  className={cn(
                    'flex items-center gap-3 rounded-md px-3 py-2',
                    'text-sm text-foreground/80 hover:text-foreground hover:bg-accent',
                    'transition-colors duration-100',
                    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
                  )}
                >
                  <Icon className="h-4 w-4 shrink-0 text-foreground/60" aria-hidden="true" />
                  {item.label}
                </Link>
              </li>
            );
          })}
        </ul>
      </CollapsibleContent>
    </Collapsible>
  );
}

// ─── NavbarMobileDrawer ───────────────────────────────────────────────────────

export interface NavbarMobileDrawerProps {
  /** Whether the drawer is open */
  open: boolean;
  /** Callback to close the drawer */
  onClose: () => void;
  /** Additional className for SheetContent */
  className?: string;
}

/**
 * NavbarMobileDrawer
 *
 * Mobile navigation sheet with 3 collapsible sections.
 * Self-contained: fetches user data internally and handles logout.
 * Does NOT depend on LayoutProvider.
 */
export function NavbarMobileDrawer({ open, onClose, className }: NavbarMobileDrawerProps) {
  const { data: user } = useCurrentUser();
  const router = useRouter();
  const [isLoggingOut, startTransition] = useTransition();

  const isAdmin =
    user?.role?.toLowerCase() === 'admin' ||
    user?.role?.toLowerCase() === 'superadmin' ||
    user?.role?.toLowerCase() === 'editor';

  function handleNavigate() {
    onClose();
  }

  function handleLogout() {
    startTransition(async () => {
      const result = await logoutAction();
      if (result.success) {
        onClose();
        router.push('/login');
      }
    });
  }

  return (
    <Sheet open={open} onOpenChange={(isOpen) => !isOpen && onClose()}>
      <SheetContent
        side="left"
        id="mobile-nav-drawer"
        className={cn('flex w-[300px] flex-col p-0 sm:w-[320px]', className)}
      >
        {/* Header */}
        <SheetHeader className="border-b border-border/50 px-4 py-3">
          <SheetTitle asChild>
            <div>
              <Logo variant="full" size="sm" asLink={false} />
            </div>
          </SheetTitle>
        </SheetHeader>

        {/* Nav sections */}
        <nav
          className="flex-1 overflow-y-auto py-3"
          aria-label="Mobile navigation"
        >
          <DrawerSection
            label="Tool"
            icon={Clock}
            items={TOOL_ITEMS}
            onNavigate={handleNavigate}
            defaultOpen
          />

          <Separator className="my-2 mx-4" />

          <DrawerSection
            label="Scopri"
            icon={Globe}
            items={DISCOVER_ITEMS}
            onNavigate={handleNavigate}
            defaultOpen
          />

          {isAdmin && (
            <>
              <Separator className="my-2 mx-4" />
              <DrawerSection
                label="Admin"
                icon={Shield}
                items={ADMIN_ITEMS}
                onNavigate={handleNavigate}
                defaultOpen={false}
              />
            </>
          )}
        </nav>

        {/* Footer */}
        <div className="border-t border-border/50 p-4 space-y-3">
          {/* Settings */}
          <Link
            href="/profile?tab=settings"
            onClick={handleNavigate}
            className={cn(
              'flex items-center gap-2 rounded-lg px-3 py-2',
              'text-sm font-medium text-muted-foreground hover:text-foreground hover:bg-muted',
              'transition-colors duration-150',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
            )}
          >
            <Settings className="h-4 w-4" aria-hidden="true" />
            Impostazioni
          </Link>

          <div className="px-1">
            <ThemeToggle showLabel size="sm" className="w-full justify-start" />
          </div>

          <Separator />

          {/* Auth actions */}
          {user ? (
            <div className="space-y-2">
              <div className="px-3 py-1">
                <p className="text-sm font-medium truncate">
                  {user.displayName || 'Utente'}
                </p>
                <p className="text-xs text-muted-foreground truncate">{user.email}</p>
              </div>
              <Button
                variant="ghost"
                onClick={handleLogout}
                disabled={isLoggingOut}
                className="w-full justify-start text-destructive hover:text-destructive hover:bg-destructive/10"
                data-testid="mobile-drawer-logout"
              >
                <LogOut className="h-4 w-4 mr-2" aria-hidden="true" />
                {isLoggingOut ? 'Disconnessione...' : 'Esci'}
              </Button>
            </div>
          ) : (
            <div className="flex flex-col gap-2">
              <Link href="/login" onClick={handleNavigate}>
                <Button variant="default" className="w-full">
                  <LogIn className="h-4 w-4 mr-2" aria-hidden="true" />
                  Accedi
                </Button>
              </Link>
              <Link href="/register" onClick={handleNavigate}>
                <Button variant="outline" className="w-full">
                  <UserPlus className="h-4 w-4 mr-2" aria-hidden="true" />
                  Registrati
                </Button>
              </Link>
            </div>
          )}
        </div>
      </SheetContent>
    </Sheet>
  );
}
