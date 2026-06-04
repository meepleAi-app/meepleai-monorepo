'use client';
import { toast } from 'sonner';

import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/overlays/dialog';

import { AssignBggIdForm } from './AssignBggIdForm';

interface ManualAssignModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function ManualAssignModal({ open, onOpenChange }: ManualAssignModalProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Manual BGG assignment</DialogTitle>
        </DialogHeader>
        <AssignBggIdForm
          onSubmit={async values => {
            const res = await fetch('/api/v1/admin/catalog-ingestion/assign-bgg-id', {
              method: 'POST',
              credentials: 'include',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify(values),
            });
            if (res.ok) {
              toast.success('BGG ID assigned');
              onOpenChange(false);
            } else {
              const body = await res.json().catch(() => ({}));
              toast.error(body.error ?? 'Assignment failed');
            }
          }}
          onCancel={() => onOpenChange(false)}
        />
      </DialogContent>
    </Dialog>
  );
}
