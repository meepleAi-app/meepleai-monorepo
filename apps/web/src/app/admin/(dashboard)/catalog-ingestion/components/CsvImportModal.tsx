'use client';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/overlays/dialog';

import { ExcelImportTab } from './ExcelImportTab';

interface CsvImportModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function CsvImportModal({ open, onOpenChange }: CsvImportModalProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-4xl">
        <DialogHeader>
          <DialogTitle>CSV / Excel import</DialogTitle>
        </DialogHeader>
        <ExcelImportTab />
      </DialogContent>
    </Dialog>
  );
}
