'use client';

import { useId, useState } from 'react';

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
import { parseTwoFactorCode } from '@/lib/auth/twoFactorCode';

interface Props {
  readonly open: boolean;
  readonly onClose: () => void;
}

export function TwoFactorDisableDialog({ open, onClose }: Props): React.JSX.Element {
  const [password, setPassword] = useState('');
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const queryClient = useQueryClient();
  const passwordId = useId();
  const codeId = useId();
  const codeHelpId = useId();

  // The authenticator's 6 digits OR a backup code, in the shape the server compares (#4806). A person
  // who lost the phone has only the backup codes, and the field used to take nothing but 6 digits.
  const parsedCode = parseTwoFactorCode(code);

  const disable = useMutation({
    mutationFn: () => {
      if (parsedCode === null) throw new Error('Enter a 6-digit code or a backup code');
      return api.auth.disable2FA(password, parsedCode.value);
    },
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
            Confirm with your password and a current authenticator code, or one of your backup
            codes.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-3">
          <div>
            <label
              htmlFor={passwordId}
              className="text-xs font-mono font-bold uppercase text-muted-foreground tracking-wide"
            >
              Password
            </label>
            <Input
              id={passwordId}
              type="password"
              value={password}
              onChange={e => setPassword(e.target.value)}
              placeholder="Your account password"
              autoComplete="current-password"
            />
          </div>
          <div>
            <label
              htmlFor={codeId}
              className="text-xs font-mono font-bold uppercase text-muted-foreground tracking-wide"
            >
              Authenticator or backup code
            </label>
            <Input
              id={codeId}
              type="text"
              value={code}
              onChange={e => setCode(e.target.value)}
              placeholder="123456 or ABCD-2345"
              autoComplete="one-time-code"
              autoCapitalize="characters"
              spellCheck={false}
              maxLength={12}
              aria-describedby={codeHelpId}
              className="font-mono tracking-widest"
            />
            <p id={codeHelpId} className="mt-1 text-xs text-muted-foreground">
              The 6 digits from your app. If you cannot reach it, one of your backup codes
              (XXXX-XXXX) works too; each one only once.
            </p>
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
              disabled={disable.isPending || password.length === 0 || parsedCode === null}
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
