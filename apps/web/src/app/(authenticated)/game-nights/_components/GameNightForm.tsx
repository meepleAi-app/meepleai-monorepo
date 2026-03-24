/**
 * GameNightForm — Shared create/edit form for game nights
 * Issue #33 — P3 Game Night Frontend
 */

'use client';

import { useState } from 'react';

import { CalendarIcon, Loader2 } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import type { CreateGameNightInput } from '@/lib/api/schemas/game-nights.schemas';

interface GameNightFormProps {
  defaultValues?: Partial<CreateGameNightInput>;
  onSubmit: (data: CreateGameNightInput) => void;
  isSubmitting?: boolean;
  submitLabel?: string;
}

export function GameNightForm({
  defaultValues,
  onSubmit,
  isSubmitting,
  submitLabel = 'Crea Serata',
}: GameNightFormProps) {
  const [title, setTitle] = useState(defaultValues?.title ?? '');
  const [description, setDescription] = useState(defaultValues?.description ?? '');
  const [scheduledAt, setScheduledAt] = useState(
    defaultValues?.scheduledAt ? new Date(defaultValues.scheduledAt).toISOString().slice(0, 16) : ''
  );
  const [location, setLocation] = useState(defaultValues?.location ?? '');
  const [maxPlayers, setMaxPlayers] = useState<string>(defaultValues?.maxPlayers?.toString() ?? '');

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const data: CreateGameNightInput = {
      title: title.trim(),
      scheduledAt: new Date(scheduledAt).toISOString(),
      ...(description.trim() && { description: description.trim() }),
      ...(location.trim() && { location: location.trim() }),
      ...(maxPlayers && { maxPlayers: Number(maxPlayers) }),
    };
    onSubmit(data);
  }

  return (
    <Card className="max-w-2xl mx-auto">
      <CardHeader>
        <CardTitle className="font-quicksand">{submitLabel}</CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="title">Titolo *</Label>
            <Input
              id="title"
              value={title}
              onChange={e => setTitle(e.target.value)}
              placeholder="Es. Serata Catan & Carcassonne"
              required
              minLength={3}
              maxLength={200}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="description">Descrizione</Label>
            <Textarea
              id="description"
              value={description}
              onChange={(e: React.ChangeEvent<HTMLTextAreaElement>) =>
                setDescription(e.target.value)
              }
              placeholder="Dettagli sulla serata, cosa portare..."
              maxLength={2000}
              rows={3}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="scheduledAt">Data e Ora *</Label>
            <div className="relative">
              <CalendarIcon className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
              <Input
                id="scheduledAt"
                type="datetime-local"
                value={scheduledAt}
                onChange={e => setScheduledAt(e.target.value)}
                className="pl-10"
                required
              />
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="location">Luogo</Label>
            <Input
              id="location"
              value={location}
              onChange={e => setLocation(e.target.value)}
              placeholder="Es. Casa di Marco, Via Roma 42"
              maxLength={500}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="maxPlayers">Numero Massimo Giocatori</Label>
            <Input
              id="maxPlayers"
              type="number"
              value={maxPlayers}
              onChange={e => setMaxPlayers(e.target.value)}
              placeholder="Lascia vuoto per nessun limite"
              min={2}
              max={50}
            />
          </div>

          <Button type="submit" disabled={isSubmitting} className="w-full">
            {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {submitLabel}
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}
