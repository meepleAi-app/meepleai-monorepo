'use client';

import { useEffect } from 'react';

import { usePathname } from 'next/navigation';

import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/navigation/sheet';

import { AdminTabSidebar } from './AdminTabSidebar';

interface AdminMobileDrawerProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function AdminMobileDrawer({ open, onOpenChange }: AdminMobileDrawerProps) {
  const pathname = usePathname();

  // Close drawer on navigation
  useEffect(() => {
    if (open) {
      onOpenChange(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pathname]);

  // Don't render Sheet at all when closed — avoids portal/overlay leaking to desktop
  if (!open) return null;

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="left" className="w-[260px] p-0" id="admin-mobile-drawer">
        <SheetHeader className="sr-only">
          <SheetTitle>Admin Navigation</SheetTitle>
        </SheetHeader>
        <AdminTabSidebar forceVisible />
      </SheetContent>
    </Sheet>
  );
}
