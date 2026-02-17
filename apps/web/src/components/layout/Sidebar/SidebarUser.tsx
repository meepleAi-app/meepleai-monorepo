/**
 * SidebarUser Component
 * Bottom section of sidebar: notifications, theme toggle, user dropdown.
 */

'use client';

import { useTransition } from 'react';

import {
  Settings,
  Shield,
  LogOut,
  UserIcon,
  User,
  FileEdit,
} from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { logoutAction } from '@/actions/auth';
import { NotificationBell } from '@/components/notifications';
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
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { cn } from '@/lib/utils';

export interface SidebarUserProps {
  isCollapsed: boolean;
}

export function SidebarUser({ isCollapsed }: SidebarUserProps) {
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
      if (result.success) {
        router.push('/login');
      }
    });
  };

  if (!user) return null;

  return (
    <div
      className={cn(
        'border-t border-sidebar-border p-3',
        'flex flex-col gap-2'
      )}
      data-testid="sidebar-user"
    >
      {/* Utilities row */}
      <div className={cn('flex items-center', isCollapsed ? 'justify-center' : 'justify-between px-1')}>
        {isCollapsed ? (
          <TooltipProvider delayDuration={0}>
            <Tooltip>
              <TooltipTrigger asChild>
                <div><NotificationBell /></div>
              </TooltipTrigger>
              <TooltipContent side="right">Notifiche</TooltipContent>
            </Tooltip>
          </TooltipProvider>
        ) : (
          <>
            <NotificationBell />
            <ThemeToggle size="sm" />
          </>
        )}
      </div>

      {/* User dropdown */}
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            variant="ghost"
            className={cn(
              'w-full justify-start gap-2 px-2 py-2',
              'hover:bg-sidebar-accent',
              isCollapsed && 'justify-center px-0'
            )}
            aria-label="User menu"
            data-testid="sidebar-user-trigger"
          >
            <div className="w-8 h-8 shrink-0 rounded-full bg-primary/20 flex items-center justify-center">
              <span className="text-sm font-semibold text-primary">{userInitial}</span>
            </div>
            {!isCollapsed && (
              <span className="text-sm text-sidebar-foreground truncate">
                {user.displayName || user.email || 'Utente'}
              </span>
            )}
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent
          align={isCollapsed ? 'center' : 'end'}
          side="right"
          className="w-56"
        >
          <DropdownMenuLabel>
            <div className="flex flex-col space-y-1">
              <p className="text-sm font-medium leading-none">
                {user.displayName || 'Utente'}
              </p>
              <p className="text-xs leading-none text-muted-foreground">{user.email}</p>
            </div>
          </DropdownMenuLabel>
          <DropdownMenuSeparator />

          <DropdownMenuItem asChild data-testid="sidebar-profile-item">
            <Link href="/profile" className="flex items-center gap-2 cursor-pointer">
              <User className="h-4 w-4" />
              <span>Profilo</span>
            </Link>
          </DropdownMenuItem>

          <DropdownMenuSeparator />

          {isEditor && (
            <>
              <DropdownMenuItem asChild data-testid="sidebar-editor-item">
                <Link href="/editor" className="flex items-center gap-2 cursor-pointer">
                  <FileEdit className="h-4 w-4" />
                  <span>Editor Panel</span>
                </Link>
              </DropdownMenuItem>
              <DropdownMenuSeparator />
            </>
          )}

          {isAdmin && (
            <>
              <DropdownMenuItem asChild data-testid="sidebar-admin-item">
                <Link href="/admin" className="flex items-center gap-2 cursor-pointer">
                  <Shield className="h-4 w-4" />
                  <span>Admin Panel</span>
                </Link>
              </DropdownMenuItem>
              <DropdownMenuSeparator />
            </>
          )}

          <DropdownMenuItem asChild data-testid="sidebar-settings-item">
            <Link href="/settings" className="flex items-center gap-2 cursor-pointer">
              <Settings className="h-4 w-4" />
              <span>Impostazioni</span>
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
            data-testid="sidebar-logout-item"
          >
            <LogOut className="h-4 w-4" />
            <span>{isLoggingOut ? 'Disconnessione...' : 'Esci'}</span>
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}
