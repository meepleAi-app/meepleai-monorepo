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
import { useTranslation } from '@/hooks/useTranslation';
import { api } from '@/lib/api';
import { ApiError } from '@/lib/api/core/errors';
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
  const { t } = useTranslation();
  const passwordId = useId();
  const codeId = useId();
  const codeHelpId = useId();

  // The authenticator's 6 digits OR a backup code, in the shape the server compares (#4806). A person
  // who lost the phone has only the backup codes, and the field used to take nothing but 6 digits.
  const parsedCode = parseTwoFactorCode(code);

  const disable = useMutation({
    mutationFn: () => {
      if (parsedCode === null) throw new Error(t('settings.security.disable.errorInvalidCode'));
      return api.auth.disable2FA(password, parsedCode.value);
    },
    // A refusal is an error status, not `{ success: false }` (#4806): it lands in `onError`.
    onSuccess: () => {
      setError(null);
      void queryClient.invalidateQueries({ queryKey: ['2fa-status'] });
      handleClose();
    },
    onError: (err: Error) =>
      // A wrong password is a bare 401 with no body, whose message would be «API … failed with status 401».
      setError(
        err instanceof ApiError && err.statusCode === 401
          ? t('settings.security.disable.errorWrongPassword')
          : (err.message ?? t('settings.security.disable.errorFailed'))
      ),
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
          <DialogTitle>{t('settings.security.disable.title')}</DialogTitle>
          <DialogDescription>{t('settings.security.disable.description')}</DialogDescription>
        </DialogHeader>

        <div className="space-y-3">
          <div>
            <label
              htmlFor={passwordId}
              className="text-xs font-mono font-bold uppercase text-muted-foreground tracking-wide"
            >
              {t('settings.security.disable.passwordLabel')}
            </label>
            <Input
              id={passwordId}
              type="password"
              value={password}
              onChange={e => setPassword(e.target.value)}
              placeholder={t('settings.security.disable.passwordPlaceholder')}
              autoComplete="current-password"
            />
          </div>
          <div>
            <label
              htmlFor={codeId}
              className="text-xs font-mono font-bold uppercase text-muted-foreground tracking-wide"
            >
              {t('settings.security.disable.codeLabel')}
            </label>
            <Input
              id={codeId}
              type="text"
              value={code}
              onChange={e => setCode(e.target.value)}
              placeholder={t('settings.security.disable.codePlaceholder')}
              autoComplete="one-time-code"
              autoCapitalize="characters"
              spellCheck={false}
              maxLength={12}
              aria-describedby={codeHelpId}
              className="font-mono tracking-widest"
            />
            <p id={codeHelpId} className="mt-1 text-xs text-muted-foreground">
              {t('settings.security.disable.codeHelp')}
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
              {t('settings.security.disable.cancel')}
            </Button>
            <Button
              variant="destructive"
              onClick={() => disable.mutate()}
              disabled={disable.isPending || password.length === 0 || parsedCode === null}
              className="flex-1"
            >
              {disable.isPending && <Loader2 className="h-3 w-3 animate-spin mr-1" />}
              {t('settings.security.disable.confirm')}
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
