/**
 * CustomGameForm - Inline form for manual game creation.
 * Issue #4819: AddGameSheet Step 1 - Game Source
 * Epic #4817: User Collection Wizard
 */

'use client';

import { useCallback, useState } from 'react';

import { Loader2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';

export interface CustomGameValues {
  title: string;
  minPlayers: number;
  maxPlayers: number;
  playingTimeMinutes?: number;
  description?: string;
}

interface CustomGameFormProps {
  onSubmit: (values: CustomGameValues) => void;
  onCancel: () => void;
  submitting?: boolean;
}

export function CustomGameForm({ onSubmit, onCancel, submitting = false }: CustomGameFormProps) {
  const [title, setTitle] = useState('');
  const [minPlayers, setMinPlayers] = useState('');
  const [maxPlayers, setMaxPlayers] = useState('');
  const [playingTime, setPlayingTime] = useState('');
  const [description, setDescription] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});

  const validate = useCallback((): boolean => {
    const newErrors: Record<string, string> = {};

    if (!title.trim()) {
      newErrors.title = 'Il nome è obbligatorio';
    } else if (title.length > 200) {
      newErrors.title = 'Max 200 caratteri';
    }

    const min = parseInt(minPlayers);
    if (!minPlayers || isNaN(min) || min < 1 || min > 99) {
      newErrors.minPlayers = 'Min 1, Max 99';
    }

    const max = parseInt(maxPlayers);
    if (!maxPlayers || isNaN(max) || max < 1 || max > 99) {
      newErrors.maxPlayers = 'Min 1, Max 99';
    } else if (!isNaN(min) && max < min) {
      newErrors.maxPlayers = 'Deve essere ≥ min giocatori';
    }

    if (playingTime) {
      const time = parseInt(playingTime);
      if (isNaN(time) || time < 1 || time > 10000) {
        newErrors.playingTime = 'Min 1, Max 10000 minuti';
      }
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  }, [title, minPlayers, maxPlayers, playingTime]);

  const handleSubmit = useCallback(() => {
    if (!validate()) return;

    onSubmit({
      title: title.trim(),
      minPlayers: parseInt(minPlayers),
      maxPlayers: parseInt(maxPlayers),
      playingTimeMinutes: playingTime ? parseInt(playingTime) : undefined,
      description: description.trim() || undefined,
    });
  }, [validate, onSubmit, title, minPlayers, maxPlayers, playingTime, description]);

  return (
    <div className="space-y-4 rounded-xl border border-slate-700 bg-slate-900/50 p-4">
      <h4 className="text-sm font-semibold text-slate-200">Crea gioco personalizzato</h4>

      {/* Title */}
      <div className="space-y-1.5">
        <Label htmlFor="custom-game-title" className="text-xs text-slate-400">
          Nome gioco *
        </Label>
        <Input
          id="custom-game-title"
          value={title}
          onChange={e => setTitle(e.target.value)}
          placeholder="Es. Catan, Ticket to Ride..."
          className="bg-slate-800 border-slate-700"
          disabled={submitting}
        />
        {errors.title && <p className="text-xs text-red-400">{errors.title}</p>}
      </div>

      {/* Players Row */}
      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-1.5">
          <Label htmlFor="custom-game-min" className="text-xs text-slate-400">
            Min giocatori *
          </Label>
          <Input
            id="custom-game-min"
            type="number"
            min={1}
            max={99}
            value={minPlayers}
            onChange={e => setMinPlayers(e.target.value)}
            placeholder="1"
            className="bg-slate-800 border-slate-700"
            disabled={submitting}
          />
          {errors.minPlayers && <p className="text-xs text-red-400">{errors.minPlayers}</p>}
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="custom-game-max" className="text-xs text-slate-400">
            Max giocatori *
          </Label>
          <Input
            id="custom-game-max"
            type="number"
            min={1}
            max={99}
            value={maxPlayers}
            onChange={e => setMaxPlayers(e.target.value)}
            placeholder="4"
            className="bg-slate-800 border-slate-700"
            disabled={submitting}
          />
          {errors.maxPlayers && <p className="text-xs text-red-400">{errors.maxPlayers}</p>}
        </div>
      </div>

      {/* Playing Time */}
      <div className="space-y-1.5">
        <Label htmlFor="custom-game-time" className="text-xs text-slate-400">
          Tempo di gioco (minuti)
        </Label>
        <Input
          id="custom-game-time"
          type="number"
          min={1}
          max={10000}
          value={playingTime}
          onChange={e => setPlayingTime(e.target.value)}
          placeholder="Es. 60"
          className="bg-slate-800 border-slate-700"
          disabled={submitting}
        />
        {errors.playingTime && <p className="text-xs text-red-400">{errors.playingTime}</p>}
      </div>

      {/* Description */}
      <div className="space-y-1.5">
        <Label htmlFor="custom-game-desc" className="text-xs text-slate-400">
          Descrizione
        </Label>
        <Textarea
          id="custom-game-desc"
          value={description}
          onChange={e => setDescription(e.target.value)}
          placeholder="Breve descrizione del gioco..."
          rows={2}
          className="bg-slate-800 border-slate-700 resize-none"
          disabled={submitting}
        />
      </div>

      {/* Actions */}
      <div className="flex gap-2 justify-end pt-1">
        <Button variant="outline" size="sm" onClick={onCancel} disabled={submitting}>
          Annulla
        </Button>
        <Button size="sm" onClick={handleSubmit} disabled={submitting}>
          {submitting && <Loader2 className="h-3.5 w-3.5 mr-1.5 animate-spin" />}
          Crea gioco
        </Button>
      </div>
    </div>
  );
}
