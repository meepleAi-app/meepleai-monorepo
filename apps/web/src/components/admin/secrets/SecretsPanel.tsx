'use client';

import { useCallback, useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, Key, Loader2, RefreshCw, Save, Shield } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { useToast } from '@/hooks/useToast';
import { adminSecretsClient, type SecretUpdate } from '@/lib/api/clients/adminSecretsClient';
import { cn } from '@/lib/utils';

import { SecretEntryInput } from './SecretEntryInput';

export function SecretsPanel() {
  const { toast } = useToast();
  const [dirtyValues, setDirtyValues] = useState<Record<string, Record<string, string>>>({});
  const [isSaving, setIsSaving] = useState(false);
  const [isRestarting, setIsRestarting] = useState(false);
  const [showRestartBanner, setShowRestartBanner] = useState(false);

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['admin', 'secrets'],
    queryFn: () => adminSecretsClient.getSecrets(),
    staleTime: 60_000,
  });

  const handleChange = useCallback((fileName: string, key: string, value: string) => {
    setDirtyValues(prev => ({
      ...prev,
      [fileName]: { ...(prev[fileName] ?? {}), [key]: value },
    }));
  }, []);

  const dirtyCount = Object.values(dirtyValues).reduce(
    (acc, fileEntries) => acc + Object.keys(fileEntries).length,
    0
  );

  const handleSave = async () => {
    const updates: SecretUpdate[] = [];
    for (const [fileName, entries] of Object.entries(dirtyValues)) {
      for (const [key, value] of Object.entries(entries)) {
        if (value) updates.push({ fileName, key, value });
      }
    }
    if (updates.length === 0) return;

    setIsSaving(true);
    try {
      await adminSecretsClient.updateSecrets(updates);
      toast({ title: `${updates.length} secret aggiornati` });
      setDirtyValues({});
      setShowRestartBanner(true);
      refetch();
    } catch (err) {
      toast({ title: 'Errore durante il salvataggio', variant: 'destructive' });
    } finally {
      setIsSaving(false);
    }
  };

  const handleRestart = async () => {
    setIsRestarting(true);
    try {
      await adminSecretsClient.restartApi();
      toast({ title: 'Riavvio API in corso...' });

      let attempts = 0;
      const poll = setInterval(async () => {
        attempts++;
        try {
          const resp = await fetch('/api/v1/health');
          if (resp.ok) {
            clearInterval(poll);
            setIsRestarting(false);
            setShowRestartBanner(false);
            toast({ title: 'API riavviata con successo' });
            refetch();
          }
        } catch {
          // Still restarting
        }
        if (attempts >= 15) {
          clearInterval(poll);
          setIsRestarting(false);
          toast({ title: "L'API non risponde. Verifica i log.", variant: 'destructive' });
        }
      }, 2000);
    } catch {
      setIsRestarting(false);
      toast({ title: 'Errore durante il riavvio', variant: 'destructive' });
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center gap-2 text-sm text-muted-foreground py-8 justify-center">
        <Loader2 className="h-4 w-4 animate-spin" /> Loading secrets...
      </div>
    );
  }

  if (error || !data) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50/50 p-4 text-sm text-red-800">
        <AlertTriangle className="inline h-4 w-4 mr-1" />
        Impossibile caricare i secret. Verifica che SECRETS_DIRECTORY sia configurato.
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Key className="h-5 w-5 text-muted-foreground" />
          <h2 className="font-quicksand text-lg font-semibold">Secrets Management</h2>
        </div>
        <span className="text-xs text-muted-foreground font-mono">{data.secretsDirectory}</span>
      </div>

      {showRestartBanner && (
        <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 flex items-center justify-between">
          <div className="flex items-center gap-2 text-sm text-amber-900">
            <RefreshCw className="h-4 w-4" />
            Secret aggiornati. Riavvia l&apos;API per applicare.
          </div>
          <Button
            size="sm"
            variant="outline"
            onClick={handleRestart}
            disabled={isRestarting}
            className="border-amber-300 text-amber-900 hover:bg-amber-100"
          >
            {isRestarting ? (
              <Loader2 className="h-3.5 w-3.5 animate-spin mr-1" />
            ) : (
              <RefreshCw className="h-3.5 w-3.5 mr-1" />
            )}
            {isRestarting ? 'Riavvio...' : 'Riavvia ora'}
          </Button>
        </div>
      )}

      <div className="grid gap-3">
        {data.files.map(file => (
          <div
            key={file.fileName}
            className={cn(
              'rounded-lg border p-4 space-y-2.5',
              file.isInfra ? 'border-amber-200/60 bg-amber-50/20' : 'border-border'
            )}
          >
            <div className="flex items-center gap-2 mb-3">
              <h3 className="text-sm font-semibold">{file.category}</h3>
              <span className="text-[10px] text-muted-foreground font-mono">{file.fileName}</span>
              {file.isInfra && (
                <span className="inline-flex items-center gap-1 text-[10px] text-amber-700 bg-amber-100 rounded-full px-1.5 py-0.5">
                  <Shield className="h-2.5 w-2.5" /> Infra
                </span>
              )}
            </div>
            {file.entries.map(entry => (
              <SecretEntryInput
                key={entry.key}
                entryKey={entry.key}
                maskedValue={entry.maskedValue}
                hasValue={entry.hasValue}
                isPlaceholder={entry.isPlaceholder}
                isDirty={!!dirtyValues[file.fileName]?.[entry.key]}
                onChange={(key, value) => handleChange(file.fileName, key, value)}
              />
            ))}
          </div>
        ))}
      </div>

      {dirtyCount > 0 && (
        <div className="sticky bottom-4">
          <Button
            onClick={handleSave}
            disabled={isSaving}
            className="w-full bg-amber-600 hover:bg-amber-700 text-white"
          >
            {isSaving ? (
              <Loader2 className="h-4 w-4 animate-spin mr-2" />
            ) : (
              <Save className="h-4 w-4 mr-2" />
            )}
            Salva modifiche ({dirtyCount} {dirtyCount === 1 ? 'campo' : 'campi'})
          </Button>
        </div>
      )}
    </div>
  );
}
