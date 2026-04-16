'use client';

import { useTransition } from 'react';

import { Bell, Settings, Shield, LogOut, UserIcon, User, FileEdit } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { logoutAction } from '@/actions/auth';
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
import { useNavigationItems } from '@/hooks/useNavigationItems';
import { useNotificationStore, selectUnreadCount } from '@/stores/notification/store';

export function UserMenuDropdown() {
  const router = useRouter();
  const { isAuthLoading, isAuthenticated } = useNavigationItems();
  const { data: user } = useCurrentUser();
  const [isLoggingOut, startTransition] = useTransition();
  const unreadCount = useNotificationStore(selectUnreadCount);

  const isAdmin =
    user?.role?.toLowerCase() === 'admin' || user?.role?.toLowerCase() === 'superadmin';
  const isEditor = user?.role?.toLowerCase() === 'editor' || isAdmin;

  const handleLogout = () => {
    startTransition(async () => {
      const result = await logoutAction();
      if (result.success) {
        router.push('/login');
      }
    });
  };

  const userInitial =
    user?.displayName?.charAt(0).toUpperCase() || user?.email?.charAt(0).toUpperCase() || 'U';

  if (isAuthLoading) {
    return (
      <div className="flex items-center" data-testid="user-menu-loading">
        <div className="w-8 h-8 rounded-full bg-muted animate-pulse" />
      </div>
    );
  }

  if (!isAuthenticated || !user) {
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
        <Button variant="ghost" size="icon" aria-label="User menu" data-testid="user-menu-trigger">
          {user.displayName ? (
            <div className="w-8 h-8 rounded-full bg-primary/20 flex items-center justify-center">
              <span className="text-sm font-semibold text-primary">{userInitial}</span>
            </div>
          ) : (
            <UserIcon className="h-5 w-5" />
          )}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuLabel>
          <div className="flex flex-col space-y-1">
            <p className="text-sm font-medium leading-none">{user.displayName || 'Utente'}</p>
            <p className="text-xs leading-none text-muted-foreground">{user.email}</p>
          </div>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />

        <DropdownMenuItem asChild data-testid="notifications-menu-item">
          <Link
            href="/notifications"
            className="flex items-center justify-between w-full cursor-pointer"
          >
            <span className="flex items-center gap-2">
              <Bell className="h-4 w-4" />
              <span>Notifiche</span>
            </span>
            {unreadCount > 0 && (
              <span className="text-xs bg-destructive text-destructive-foreground px-1.5 py-0.5 rounded-full min-w-[1.25rem] text-center">
                {unreadCount > 9 ? '9+' : unreadCount}
              </span>
            )}
          </Link>
        </DropdownMenuItem>

        <DropdownMenuItem asChild data-testid="profile-menu-item">
          <Link href="/profile" className="flex items-center gap-2 cursor-pointer">
            <User className="h-4 w-4" />
            <span>Profilo</span>
          </Link>
        </DropdownMenuItem>

        <DropdownMenuSeparator />

        {isEditor && (
          <>
            <DropdownMenuItem asChild data-testid="editor-panel-menu-item">
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
            <DropdownMenuItem asChild data-testid="admin-panel-menu-item">
              <Link href="/admin/overview" className="flex items-center gap-2 cursor-pointer">
                <Shield className="h-4 w-4" />
                <span>Admin Panel</span>
              </Link>
            </DropdownMenuItem>
            <DropdownMenuSeparator />
          </>
        )}

        <DropdownMenuItem asChild data-testid="settings-menu-item">
          <Link href="/profile?tab=settings" className="flex items-center gap-2 cursor-pointer">
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
          data-testid="logout-menu-item"
        >
          <LogOut className="h-4 w-4" />
          <span>{isLoggingOut ? 'Disconnessione...' : 'Esci'}</span>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
