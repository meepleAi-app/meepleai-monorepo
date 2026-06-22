'use client';

import { useState } from 'react';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { AlertCircle, Loader2 } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { api } from '@/lib/api';

interface Props {
  readonly open: boolean;
  readonly onClose: () => void;
}

export function TwoFactorDisableDialog({ open, onClose }: Props): React.JSX.Element {
  const [password, setPassword] = useState('');
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const queryClient = useQueryClient();

  const disable = useMutation({
    mutationFn: () => api.auth.disable2FA(password, code),
    onSuccess: result => {
      if (result.success) {
        setError(null);
        void queryClient.invalidateQueries({ queryKey: ['2fa-status'] });
        handleClose();
      } else {
        setError(result.errorMessage ?? 'Failed to disable 2FA');
      }
    },
    onError: (err: Error) => setError(err.message ?? 'Failed to disable 2FA'),
  });

  function reset(): void {
    setPassword('');
    setCode('');
    setError(null);
  }

  function handleClose(): void {
    reset();
    onClose();
  }

  return (
    <Dialog
      open={open}
      onOpenChange={o => {
        if (!o) handleClose();
      }}
    >
      <DialogContent className="max-w-sm" data-testid="disable-2fa-confirm">
        <DialogHeader>
          <DialogTitle>Disable two-factor authentication</DialogTitle>
          <DialogDescription>
            Conferma con la tua password e un codice TOTP corrente.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-3">
          <div>
            <label className="text-xs font-mono font-bold uppercase text-muted-foreground tracking-wide">
              Password
            </label>
            <Input
              type="password"
              value={password}
              onChange={e => setPassword(e.target.value)}
              placeholder="Your account password"
              autoComplete="current-password"
            />
          </div>
          <div>
            <label className="text-xs font-mono font-bold uppercase text-muted-foreground tracking-wide">
              Authenticator code
            </label>
            <Input
              type="text"
              inputMode="numeric"
              pattern="[0-9]*"
              maxLength={6}
              value={code}
              onChange={e => setCode(e.target.value.replace(/\D/g, ''))}
              placeholder="123456"
              className="font-mono tracking-widest"
            />
          </div>
          {error && (
            <div
              className="flex items-center gap-2 p-2 rounded-md bg-destructive/10 text-destructive text-sm"
              role="alert"
            >
              <AlertCircle className="h-4 w-4 shrink-0" aria-hidden />
              <span>{error}</span>
            </div>
          )}
          <div className="flex gap-2 pt-2">
            <Button variant="outline" onClick={handleClose} className="flex-1">
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={() => disable.mutate()}
              disabled={disable.isPending || password.length === 0 || code.length !== 6}
              className="flex-1"
            >
              {disable.isPending && <Loader2 className="h-3 w-3 animate-spin mr-1" />}
              Disable 2FA
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
