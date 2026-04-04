'use client';

import type { ReactNode } from 'react';

import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/navigation/sheet';

interface BottomSheetProps {
  isOpen: boolean;
  onClose: () => void;
  title?: string;
  height?: string;
  children: ReactNode;
}

export function BottomSheet({
  isOpen,
  onClose,
  title,
  height = '60vh',
  children,
}: BottomSheetProps) {
  return (
    <Sheet
      open={isOpen}
      onOpenChange={open => {
        if (!open) onClose();
      }}
    >
      <SheetContent
        side="bottom"
        className="rounded-t-[20px] pb-[env(safe-area-inset-bottom,20px)]"
        style={{ maxHeight: height }}
      >
        {title && (
          <SheetHeader>
            <SheetTitle>{title}</SheetTitle>
          </SheetHeader>
        )}
        <div className="overflow-y-auto">{children}</div>
      </SheetContent>
    </Sheet>
  );
}
