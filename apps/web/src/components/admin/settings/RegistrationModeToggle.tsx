/**
 * RegistrationModeToggle Component
 *
 * Switch to toggle between invite-only and public registration mode.
 * Fetches current state on mount, shows a confirmation dialog before changing.
 */

'use client';

import { useState } from 'react';

import { useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';

import { Switch } from '@/components/ui/forms/switch';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';

export function RegistrationModeToggle() {
  const queryClient = useQueryClient();
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [pendingValue, setPendingValue] = useState<boolean | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ['admin', 'registration-mode'],
    queryFn: () => api.accessRequests.getRegistrationMode(),
    staleTime: 60_000,
  });

  const isPublicEnabled = data?.publicRegistrationEnabled ?? false;

  function handleToggleRequest(nextValue: boolean) {
    setPendingValue(nextValue);
    setConfirmOpen(true);
  }

  async function handleConfirm() {
    if (pendingValue === null) return;
    setIsSubmitting(true);
    try {
      await api.accessRequests.setRegistrationMode(pendingValue);
      queryClient.invalidateQueries({ queryKey: ['admin', 'registration-mode'] });
      toast.success(pendingValue ? 'Public registration enabled' : 'Switched to invite-only mode');
      setConfirmOpen(false);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to update registration mode');
    } finally {
      setIsSubmitting(false);
      setPendingValue(null);
    }
  }

  function handleCancel() {
    setConfirmOpen(false);
    setPendingValue(null);
  }

  const confirmMessage = pendingValue
    ? 'This will allow anyone to register without an invitation or access request approval.'
    : 'New registrations will require an invitation or approved access request.';

  return (
    <>
      <div className="flex items-center justify-between gap-4">
        <div>
          <p className="text-sm font-medium text-foreground">
            {isLoading
              ? 'Loading...'
              : isPublicEnabled
                ? 'Public registration enabled'
                : 'Invite-only mode'}
          </p>
          <p className="text-xs text-muted-foreground mt-0.5">
            {isPublicEnabled
              ? 'Anyone can create an account.'
              : 'New users need an invitation or approved access request.'}
          </p>
        </div>
        <Switch
          checked={isPublicEnabled}
          onCheckedChange={handleToggleRequest}
          disabled={isLoading || isSubmitting}
          aria-label="Toggle public registration"
        />
      </div>

      <Dialog
        open={confirmOpen}
        onOpenChange={open => {
          if (!open) handleCancel();
        }}
      >
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>
              {pendingValue ? 'Enable Public Registration?' : 'Enable Invite-Only Mode?'}
            </DialogTitle>
            <DialogDescription>{confirmMessage}</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={handleCancel} disabled={isSubmitting}>
              Cancel
            </Button>
            <Button onClick={handleConfirm} disabled={isSubmitting}>
              {isSubmitting ? 'Saving...' : 'Confirm'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
