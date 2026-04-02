'use client';

import { useEffect } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';

import { Switch } from '@/components/ui/forms/switch';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { api } from '@/lib/api';

interface Props {
  gameId: string;
}

interface SettingsForm {
  maxChunks: number | '';
  chunkSize: number | '';
  cacheEnabled: boolean;
  language: string;
}

export function GameKbSettings({ gameId }: Props) {
  const queryClient = useQueryClient();

  const { data } = useQuery({
    queryKey: ['admin-game-kb-settings', gameId],
    queryFn: () => api.knowledgeBase.getGameKbSettings(gameId),
  });

  const { register, handleSubmit, setValue, watch } = useForm<SettingsForm>({
    defaultValues: { maxChunks: '', chunkSize: '', cacheEnabled: true, language: '' },
  });

  useEffect(() => {
    if (!data) return;
    if (data.maxChunksOverride !== null && data.maxChunksOverride !== undefined)
      setValue('maxChunks', data.maxChunksOverride);
    if (data.chunkSizeOverride !== null && data.chunkSizeOverride !== undefined)
      setValue('chunkSize', data.chunkSizeOverride);
    if (data.cacheEnabledOverride !== null && data.cacheEnabledOverride !== undefined)
      setValue('cacheEnabled', data.cacheEnabledOverride);
    if (data.languageOverride) setValue('language', data.languageOverride);
  }, [data, setValue]);

  const saveMutation = useMutation({
    mutationFn: (values: SettingsForm) =>
      api.knowledgeBase.setGameKbSettings(gameId, {
        maxChunksOverride: values.maxChunks === '' ? undefined : Number(values.maxChunks),
        chunkSizeOverride: values.chunkSize === '' ? undefined : Number(values.chunkSize),
        cacheEnabledOverride: values.cacheEnabled,
        languageOverride: values.language || undefined,
      }),
    onSuccess: () => {
      toast.success('Impostazioni KB salvate');
      queryClient.invalidateQueries({ queryKey: ['admin-game-kb-settings', gameId] });
    },
    onError: () => toast.error('Errore nel salvataggio'),
  });

  const cacheEnabled = watch('cacheEnabled');

  return (
    <form onSubmit={handleSubmit(v => saveMutation.mutate(v))} className="space-y-4 max-w-sm">
      <div className="space-y-1.5">
        <Label htmlFor="maxChunks">Max chunks override</Label>
        <Input
          id="maxChunks"
          type="number"
          min={1}
          placeholder="default"
          {...register('maxChunks', { valueAsNumber: true })}
        />
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="chunkSize">Chunk size override</Label>
        <Input
          id="chunkSize"
          type="number"
          min={100}
          placeholder="default"
          {...register('chunkSize', { valueAsNumber: true })}
        />
      </div>

      <div className="flex items-center gap-2">
        <Switch
          id="cacheEnabled"
          checked={cacheEnabled}
          onCheckedChange={v => setValue('cacheEnabled', v)}
        />
        <Label htmlFor="cacheEnabled">Cache abilitata</Label>
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="language">Lingua override</Label>
        <Input id="language" placeholder="es. it, en" {...register('language')} />
      </div>

      <Button type="submit" disabled={saveMutation.isPending}>
        {saveMutation.isPending ? 'Salvataggio...' : 'Salva impostazioni'}
      </Button>
    </form>
  );
}
