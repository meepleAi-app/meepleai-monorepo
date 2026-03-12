'use client';

import { useState } from 'react';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/overlays/alert-dialog-primitives';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { api } from '@/lib/api';

const AVAILABLE_ROLES = ['User', 'Editor', 'Admin'] as const;

interface InlineRoleSelectProps {
  userId: string;
  currentRole: string;
  userName: string;
  onRoleChanged?: () => void;
}

export function InlineRoleSelect({
  userId,
  currentRole,
  userName,
  onRoleChanged,
}: InlineRoleSelectProps) {
  const queryClient = useQueryClient();
  const [pendingRole, setPendingRole] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: (newRole: string) => api.admin.changeUserRole(userId, newRole),
    onSuccess: (_data, newRole) => {
      toast.success(`Role changed to ${newRole} for ${userName}`);
      queryClient.invalidateQueries({ queryKey: ['admin', 'users'] });
      onRoleChanged?.();
    },
    onError: (err) => {
      toast.error(
        err instanceof Error ? err.message : 'Failed to change role'
      );
    },
  });

  function handleSelect(value: string) {
    if (value !== currentRole) {
      setPendingRole(value);
    }
  }

  function handleConfirm() {
    if (pendingRole) {
      mutation.mutate(pendingRole);
      setPendingRole(null);
    }
  }

  return (
    <>
      <Select
        value={currentRole}
        onValueChange={handleSelect}
        disabled={mutation.isPending}
      >
        <SelectTrigger className="h-7 w-[100px] text-xs">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {AVAILABLE_ROLES.map((role) => (
            <SelectItem key={role} value={role}>
              {role}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      <AlertDialog
        open={pendingRole !== null}
        onOpenChange={(open) => !open && setPendingRole(null)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Change Role</AlertDialogTitle>
            <AlertDialogDescription>
              Change the role of <strong>{userName}</strong> from{' '}
              <strong>{currentRole}</strong> to{' '}
              <strong>{pendingRole}</strong>?
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={handleConfirm}>
              Confirm
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
