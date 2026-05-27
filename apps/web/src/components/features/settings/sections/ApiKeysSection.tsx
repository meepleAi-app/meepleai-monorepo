'use client';

import { useState } from 'react';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Copy, Loader2, AlertCircle } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { api } from '@/lib/api';

export function ApiKeysSection(): React.JSX.Element {
  const queryClient = useQueryClient();
  const [keyName, setKeyName] = useState('');
  const [plaintextKey, setPlaintextKey] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [copied, setCopied] = useState(false);

  const keysQuery = useQuery({
    queryKey: ['api-keys'],
    queryFn: () => api.auth.listApiKeys(),
  });

  const createMutation = useMutation({
    mutationFn: () => api.auth.createApiKey({ keyName: keyName.trim(), scopes: 'read' }),
    onSuccess: result => {
      setPlaintextKey(result.plaintextKey);
      setDialogOpen(true);
      setKeyName('');
      void queryClient.invalidateQueries({ queryKey: ['api-keys'] });
    },
  });

  const revokeMutation = useMutation({
    mutationFn: (keyId: string) => api.auth.revokeApiKey(keyId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['api-keys'] });
    },
  });

  function handleCopy() {
    if (!plaintextKey) return;
    void navigator.clipboard.writeText(plaintextKey).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    });
  }

  function handleDialogClose() {
    setDialogOpen(false);
    setPlaintextKey(null);
    setCopied(false);
  }

  if (keysQuery.isLoading) {
    return (
      <div className="bg-card border border-border rounded-lg p-5 flex items-center justify-center">
        <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" aria-label="Loading" />
      </div>
    );
  }

  if (keysQuery.isError) {
    return (
      <div
        role="alert"
        className="bg-destructive/5 border border-destructive/30 rounded-lg p-5 text-destructive flex items-center gap-2"
      >
        <AlertCircle className="h-4 w-4 shrink-0" aria-hidden />
        Failed to load API keys.
      </div>
    );
  }

  const items = keysQuery.data?.items ?? [];
  const canCreate = keyName.trim().length >= 3;

  return (
    <div className="space-y-6">
      {/* Create form */}
      <section className="bg-card border border-border rounded-lg p-5 space-y-4">
        <h3 className="font-quicksand font-bold text-foreground">Create API Key</h3>
        <div className="flex gap-2">
          <Input
            data-testid="api-key-name-input"
            placeholder="Key name (min 3 chars)"
            value={keyName}
            onChange={e => setKeyName(e.target.value)}
            maxLength={100}
          />
          <Button
            data-testid="create-api-key-button"
            disabled={!canCreate || createMutation.isPending}
            onClick={() => createMutation.mutate()}
          >
            {createMutation.isPending && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
            Create key
          </Button>
        </div>
        {createMutation.isError && (
          <p role="alert" className="text-destructive text-sm">
            Failed to create key. Please try again.
          </p>
        )}
      </section>

      {/* Key list */}
      <section className="bg-card border border-border rounded-lg p-5 space-y-3">
        <h3 className="font-quicksand font-bold text-foreground">Your API Keys</h3>
        {items.length === 0 ? (
          <p className="text-muted-foreground text-sm">No API keys yet.</p>
        ) : (
          <ul className="space-y-2">
            {items.map(key => (
              <li
                key={key.id}
                className="flex items-center justify-between gap-3 rounded-md border border-border p-3"
              >
                <div className="min-w-0 space-y-0.5">
                  <p className="font-medium text-foreground truncate">{key.keyName}</p>
                  <p className="text-xs text-muted-foreground font-mono">{key.keyPrefix}…</p>
                </div>
                <Button
                  variant="destructive"
                  size="sm"
                  aria-label={`Revoke ${key.keyName}`}
                  disabled={revokeMutation.isPending}
                  onClick={() => revokeMutation.mutate(key.id)}
                >
                  Revoke
                </Button>
              </li>
            ))}
          </ul>
        )}
      </section>

      {/* Plaintext key dialog — shown once after creation */}
      <Dialog
        open={dialogOpen}
        onOpenChange={open => {
          if (!open) handleDialogClose();
        }}
      >
        <DialogContent hideCloseButton>
          <DialogHeader>
            <DialogTitle>Save your API key</DialogTitle>
            <DialogDescription>
              This key will not be shown again. Copy it now and store it securely.
            </DialogDescription>
          </DialogHeader>

          <div className="rounded-md bg-muted p-3 font-mono text-sm break-all select-all">
            <span data-testid="api-key-plaintext">{plaintextKey}</span>
          </div>

          <DialogFooter className="flex-col sm:flex-row gap-2">
            <Button variant="outline" onClick={handleCopy} className="flex items-center gap-2">
              <Copy className="h-4 w-4" aria-hidden />
              {copied ? 'Copied!' : 'Copy key'}
            </Button>
            <Button onClick={handleDialogClose}>I&apos;ve saved it</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
