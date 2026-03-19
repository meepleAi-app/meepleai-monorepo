'use client';

import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';

interface SqlPreviewModalProps {
  sql: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function SqlPreviewModal({ sql, open, onOpenChange }: SqlPreviewModalProps) {
  const handleCopy = async () => {
    await navigator.clipboard.writeText(sql);
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>SQL Preview</DialogTitle>
        </DialogHeader>

        <div className="max-h-[60vh] overflow-auto rounded-lg border border-slate-200/60 bg-slate-50 p-4 dark:border-zinc-700/40 dark:bg-zinc-900/50">
          <pre className="whitespace-pre-wrap font-mono text-xs leading-relaxed text-foreground">
            <code>{sql}</code>
          </pre>
        </div>

        <DialogFooter>
          <Button variant="outline" size="sm" onClick={handleCopy}>
            Copy SQL
          </Button>
          <Button variant="outline" size="sm" onClick={() => onOpenChange(false)}>
            Close
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
