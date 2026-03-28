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
} from '@/components/ui/overlays/select';
import { api } from '@/lib/api';
import { ASSIGNABLE_ROLES, normalizeRole, displayRole } from '@/lib/utils/roles';

interface InlineRoleSelectProps {
  userId: string;
  currentRole: string;
  userName: string;
  onRoleChanged?: () => void;
}

export function InlineRoleSelect({
  userId,
  currentRole: rawCurrentRole,
  userName,
  onRoleChanged,
}: InlineRoleSelectProps) {
  const queryClient = useQueryClient();
  const [pendingRole, setPendingRole] = useState<string | null>(null);
  const currentRole = normalizeRole(rawCurrentRole);
  const isSuperAdmin = currentRole === 'superadmin';

  const mutation = useMutation({
    mutationFn: (newRole: string) => api.admin.changeUserRole(userId, newRole),
    onSuccess: (_data, newRole) => {
      toast.success(`Role changed to ${displayRole(newRole)} for ${userName}`);
      queryClient.invalidateQueries({ queryKey: ['admin', 'users'] });
      onRoleChanged?.();
    },
    onError: err => {
      toast.error(err instanceof Error ? err.message : 'Failed to change role');
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

  if (isSuperAdmin) {
    return (
      <span className="inline-flex items-center gap-1.5 rounded-md bg-amber-100 px-2.5 py-0.5 text-xs font-semibold text-amber-800 dark:bg-amber-900/30 dark:text-amber-300">
        SuperAdmin
      </span>
    );
  }

  return (
    <>
      <Select value={currentRole} onValueChange={handleSelect} disabled={mutation.isPending}>
        <SelectTrigger className="h-7 w-[100px] text-xs">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {ASSIGNABLE_ROLES.map(role => (
            <SelectItem key={role} value={role}>
              {displayRole(role)}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      <AlertDialog open={pendingRole !== null} onOpenChange={open => !open && setPendingRole(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Change Role</AlertDialogTitle>
            <AlertDialogDescription>
              Change the role of <strong>{userName}</strong> from{' '}
              <strong>{displayRole(currentRole)}</strong> to{' '}
              <strong>{displayRole(pendingRole)}</strong>?
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={handleConfirm}>Confirm</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
