'use client';

import { useState } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Plus,
  Pencil,
  Trash2,
  Zap,
  CheckCircle2,
  XCircle,
  ExternalLink,
  Loader2,
} from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/data-display/badge';
import { useToast } from '@/hooks/useToast';
import { api } from '@/lib/api';

type N8nConfig = {
  id: string;
  name: string;
  baseUrl: string;
  webhookUrl?: string | null;
  isActive: boolean;
  lastTestedAt?: string | null;
  lastTestResult?: string | null;
  createdAt: string;
  updatedAt: string;
};

export function N8nConfigContent() {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [showForm, setShowForm] = useState(false);
  const [editingConfig, setEditingConfig] = useState<N8nConfig | null>(null);
  const [formData, setFormData] = useState({
    name: '',
    baseUrl: '',
    apiKey: '',
    webhookUrl: '',
  });

  const { data: configs = [], isLoading } = useQuery({
    queryKey: ['admin', 'n8n', 'configs'],
    queryFn: () => api.admin.getN8nConfigs(),
  });

  const createMutation = useMutation({
    mutationFn: (data: { name: string; baseUrl: string; apiKey: string; webhookUrl?: string }) =>
      api.admin.createN8nConfig(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'n8n', 'configs'] });
      resetForm();
      toast({
        title: 'Configurazione creata',
        description: 'n8n configurazione creata con successo',
      });
    },
    onError: () =>
      toast({
        title: 'Errore',
        description: 'Impossibile creare la configurazione',
        variant: 'destructive',
      }),
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: string; data: Record<string, unknown> }) =>
      api.admin.updateN8nConfig(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'n8n', 'configs'] });
      resetForm();
      toast({ title: 'Aggiornato', description: 'Configurazione aggiornata' });
    },
    onError: () =>
      toast({ title: 'Errore', description: 'Impossibile aggiornare', variant: 'destructive' }),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => api.admin.deleteN8nConfig(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'n8n', 'configs'] });
      toast({ title: 'Eliminata', description: 'Configurazione eliminata' });
    },
    onError: () =>
      toast({ title: 'Errore', description: 'Impossibile eliminare', variant: 'destructive' }),
  });

  const testMutation = useMutation({
    mutationFn: (id: string) => api.admin.testN8nConnection(id),
    onSuccess: result => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'n8n', 'configs'] });
      toast({
        title: result.success ? 'Connessione OK' : 'Test fallito',
        description: result.message,
        variant: result.success ? 'default' : 'destructive',
      });
    },
    onError: () =>
      toast({ title: 'Errore', description: 'Test connessione fallito', variant: 'destructive' }),
  });

  const resetForm = () => {
    setShowForm(false);
    setEditingConfig(null);
    setFormData({ name: '', baseUrl: '', apiKey: '', webhookUrl: '' });
  };

  const handleEdit = (config: N8nConfig) => {
    setEditingConfig(config);
    setFormData({
      name: config.name,
      baseUrl: config.baseUrl,
      apiKey: '',
      webhookUrl: config.webhookUrl || '',
    });
    setShowForm(true);
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (editingConfig) {
      const data: Record<string, unknown> = {};
      if (formData.name !== editingConfig.name) data.name = formData.name;
      if (formData.baseUrl !== editingConfig.baseUrl) data.baseUrl = formData.baseUrl;
      if (formData.apiKey) data.apiKey = formData.apiKey;
      if (formData.webhookUrl !== (editingConfig.webhookUrl || ''))
        data.webhookUrl = formData.webhookUrl || null;
      updateMutation.mutate({ id: editingConfig.id, data });
    } else {
      createMutation.mutate({
        name: formData.name,
        baseUrl: formData.baseUrl,
        apiKey: formData.apiKey,
        webhookUrl: formData.webhookUrl || undefined,
      });
    }
  };

  const handleToggleActive = (config: N8nConfig) => {
    updateMutation.mutate({ id: config.id, data: { isActive: !config.isActive } });
  };

  if (isLoading) return null;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold font-quicksand">n8n Workflow Integration</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Configura e gestisci le connessioni n8n per automazione workflow
          </p>
        </div>
        <Button
          onClick={() => {
            if (showForm) resetForm();
            else setShowForm(true);
          }}
          variant={showForm ? 'outline' : 'default'}
        >
          {showForm ? (
            'Annulla'
          ) : (
            <>
              <Plus className="mr-2 h-4 w-4" /> Aggiungi Configurazione
            </>
          )}
        </Button>
      </div>

      {/* Form */}
      {showForm && (
        <div className="rounded-lg border bg-card p-6">
          <h3 className="mb-4 text-lg font-semibold">
            {editingConfig ? 'Modifica Configurazione' : 'Nuova Configurazione'}
          </h3>
          <form onSubmit={handleSubmit} className="grid gap-4 sm:grid-cols-2">
            <div>
              <label className="text-sm font-medium">Nome *</label>
              <input
                type="text"
                value={formData.name}
                onChange={e => setFormData({ ...formData, name: e.target.value })}
                required
                className="mt-1 w-full rounded-md border bg-background px-3 py-2 text-sm"
                placeholder="Produzione n8n"
              />
            </div>
            <div>
              <label className="text-sm font-medium">Base URL *</label>
              <input
                type="url"
                value={formData.baseUrl}
                onChange={e => setFormData({ ...formData, baseUrl: e.target.value })}
                required
                className="mt-1 w-full rounded-md border bg-background px-3 py-2 text-sm"
                placeholder="http://localhost:5678"
              />
            </div>
            <div>
              <label className="text-sm font-medium">
                API Key {editingConfig ? '(lascia vuoto per mantenere)' : '*'}
              </label>
              <input
                type="password"
                value={formData.apiKey}
                onChange={e => setFormData({ ...formData, apiKey: e.target.value })}
                required={!editingConfig}
                className="mt-1 w-full rounded-md border bg-background px-3 py-2 text-sm"
                placeholder="n8n API key"
              />
            </div>
            <div>
              <label className="text-sm font-medium">Webhook URL (opzionale)</label>
              <input
                type="url"
                value={formData.webhookUrl}
                onChange={e => setFormData({ ...formData, webhookUrl: e.target.value })}
                className="mt-1 w-full rounded-md border bg-background px-3 py-2 text-sm"
                placeholder="http://localhost:5678/webhook"
              />
            </div>
            <div className="sm:col-span-2">
              <Button type="submit" disabled={createMutation.isPending || updateMutation.isPending}>
                {(createMutation.isPending || updateMutation.isPending) && (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                )}
                {editingConfig ? 'Aggiorna' : 'Crea Configurazione'}
              </Button>
            </div>
          </form>
        </div>
      )}

      {/* Config List */}
      {configs.length === 0 ? (
        <div className="rounded-lg border bg-card p-12 text-center text-muted-foreground">
          <Zap className="mx-auto mb-4 h-12 w-12 opacity-50" />
          <p className="text-lg font-medium">Nessuna configurazione n8n</p>
          <p className="mt-1 text-sm">Clicca &quot;Aggiungi Configurazione&quot; per iniziare.</p>
        </div>
      ) : (
        <div className="grid gap-4">
          {configs.map(config => (
            <div
              key={config.id}
              className="rounded-lg border bg-card p-6 transition-colors hover:bg-accent/5"
            >
              <div className="flex items-start justify-between">
                <div className="flex-1">
                  <div className="flex items-center gap-3">
                    <h3 className="text-lg font-semibold">{config.name}</h3>
                    <Badge variant={config.isActive ? 'default' : 'secondary'}>
                      {config.isActive ? 'Attivo' : 'Inattivo'}
                    </Badge>
                  </div>
                  <p className="mt-1 text-sm text-muted-foreground">{config.baseUrl}</p>
                  {config.webhookUrl && (
                    <p className="mt-0.5 text-sm text-muted-foreground">
                      Webhook: {config.webhookUrl}
                    </p>
                  )}
                </div>
                <div className="flex items-center gap-2">
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => testMutation.mutate(config.id)}
                    disabled={testMutation.isPending}
                  >
                    {testMutation.isPending ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <Zap className="h-4 w-4" />
                    )}
                    <span className="ml-1">Test</span>
                  </Button>
                  <Button size="sm" variant="outline" onClick={() => handleToggleActive(config)}>
                    {config.isActive ? 'Disattiva' : 'Attiva'}
                  </Button>
                  <Button size="sm" variant="outline" onClick={() => handleEdit(config)}>
                    <Pencil className="h-4 w-4" />
                  </Button>
                  <Button
                    size="sm"
                    variant="destructive"
                    onClick={() => {
                      if (confirm('Sei sicuro di voler eliminare questa configurazione?')) {
                        deleteMutation.mutate(config.id);
                      }
                    }}
                  >
                    <Trash2 className="h-4 w-4" />
                  </Button>
                  <Button size="sm" variant="ghost" asChild>
                    <a href={config.baseUrl} target="_blank" rel="noopener noreferrer">
                      <ExternalLink className="h-4 w-4" />
                    </a>
                  </Button>
                </div>
              </div>

              {/* Last test result */}
              {config.lastTestedAt && (
                <div className="mt-4 rounded-md bg-muted/50 p-3">
                  <div className="flex items-center gap-2 text-sm">
                    {config.lastTestResult?.toLowerCase().includes('success') ? (
                      <CheckCircle2 className="h-4 w-4 text-green-600" />
                    ) : (
                      <XCircle className="h-4 w-4 text-red-600" />
                    )}
                    <span className="font-medium">
                      Ultimo test: {new Date(config.lastTestedAt).toLocaleString('it-IT')}
                    </span>
                  </div>
                  <p className="mt-1 text-sm text-muted-foreground">{config.lastTestResult}</p>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
