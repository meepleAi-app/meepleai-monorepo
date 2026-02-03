'use client';

/**
 * Step 2: Game Details Form (Conditional)
 * Issue #3477: Custom game details (only if user created custom game)
 */

import { useState, useEffect } from 'react';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { useAddGameWizard } from '@/hooks/useAddGameWizard';

export function GameDetailsForm() {
  const { customGameData, setCustomGameData, canGoNext, goNext, goBack } = useAddGameWizard();

  // Local form state
  const [formData, setFormData] = useState({
    name: customGameData?.name ?? '',
    minPlayers: customGameData?.minPlayers?.toString() ?? '',
    maxPlayers: customGameData?.maxPlayers?.toString() ?? '',
    playTime: customGameData?.playTime?.toString() ?? '',
    complexity: customGameData?.complexity?.toString() ?? '',
  });

  const [errors, setErrors] = useState<Record<string, string>>({});

  // Sync form data to store
  useEffect(() => {
    const data = {
      name: formData.name,
      minPlayers: formData.minPlayers ? parseInt(formData.minPlayers, 10) : undefined,
      maxPlayers: formData.maxPlayers ? parseInt(formData.maxPlayers, 10) : undefined,
      playTime: formData.playTime ? parseInt(formData.playTime, 10) : undefined,
      complexity: formData.complexity ? parseInt(formData.complexity, 10) : undefined,
    };
    setCustomGameData(data);
  }, [formData, setCustomGameData]);

  const validateForm = (): boolean => {
    const newErrors: Record<string, string> = {};

    // Name required
    if (!formData.name.trim()) {
      newErrors.name = 'Game name is required';
    }

    // Numeric validations
    if (formData.minPlayers && parseInt(formData.minPlayers, 10) < 1) {
      newErrors.minPlayers = 'Minimum 1 player';
    }

    if (formData.maxPlayers && parseInt(formData.maxPlayers, 10) < 1) {
      newErrors.maxPlayers = 'Minimum 1 player';
    }

    if (
      formData.minPlayers &&
      formData.maxPlayers &&
      parseInt(formData.minPlayers, 10) > parseInt(formData.maxPlayers, 10)
    ) {
      newErrors.maxPlayers = 'Max players must be >= min players';
    }

    if (formData.playTime && parseInt(formData.playTime, 10) < 1) {
      newErrors.playTime = 'Must be at least 1 minute';
    }

    if (formData.complexity) {
      const complexity = parseInt(formData.complexity, 10);
      if (complexity < 1 || complexity > 5) {
        newErrors.complexity = 'Complexity must be between 1-5';
      }
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleNext = () => {
    if (validateForm()) {
      goNext();
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold text-slate-900 dark:text-white mb-2">
          Game Details
        </h2>
        <p className="text-slate-600 dark:text-slate-400">
          Provide details for your custom game
        </p>
      </div>

      {/* Form */}
      <div className="space-y-4">
        {/* Name (Required) */}
        <div className="space-y-2">
          <Label htmlFor="game-name" className="required">
            Game Name *
          </Label>
          <Input
            id="game-name"
            type="text"
            value={formData.name}
            onChange={e => setFormData(prev => ({ ...prev, name: e.target.value }))}
            placeholder="e.g., My Custom Board Game"
            className={errors.name ? 'border-red-500' : ''}
          />
          {errors.name && <p className="text-sm text-red-600 dark:text-red-400">{errors.name}</p>}
        </div>

        {/* Players */}
        <div className="grid grid-cols-2 gap-4">
          <div className="space-y-2">
            <Label htmlFor="min-players">Min Players</Label>
            <Input
              id="min-players"
              type="number"
              min="1"
              value={formData.minPlayers}
              onChange={e => setFormData(prev => ({ ...prev, minPlayers: e.target.value }))}
              placeholder="e.g., 2"
              className={errors.minPlayers ? 'border-red-500' : ''}
            />
            {errors.minPlayers && (
              <p className="text-sm text-red-600 dark:text-red-400">{errors.minPlayers}</p>
            )}
          </div>

          <div className="space-y-2">
            <Label htmlFor="max-players">Max Players</Label>
            <Input
              id="max-players"
              type="number"
              min="1"
              value={formData.maxPlayers}
              onChange={e => setFormData(prev => ({ ...prev, maxPlayers: e.target.value }))}
              placeholder="e.g., 4"
              className={errors.maxPlayers ? 'border-red-500' : ''}
            />
            {errors.maxPlayers && (
              <p className="text-sm text-red-600 dark:text-red-400">{errors.maxPlayers}</p>
            )}
          </div>
        </div>

        {/* Play Time */}
        <div className="space-y-2">
          <Label htmlFor="play-time">Play Time (minutes)</Label>
          <Input
            id="play-time"
            type="number"
            min="1"
            value={formData.playTime}
            onChange={e => setFormData(prev => ({ ...prev, playTime: e.target.value }))}
            placeholder="e.g., 60"
            className={errors.playTime ? 'border-red-500' : ''}
          />
          {errors.playTime && (
            <p className="text-sm text-red-600 dark:text-red-400">{errors.playTime}</p>
          )}
        </div>

        {/* Complexity */}
        <div className="space-y-2">
          <Label htmlFor="complexity">Complexity (1-5)</Label>
          <Input
            id="complexity"
            type="number"
            min="1"
            max="5"
            value={formData.complexity}
            onChange={e => setFormData(prev => ({ ...prev, complexity: e.target.value }))}
            placeholder="e.g., 3"
            className={errors.complexity ? 'border-red-500' : ''}
          />
          <p className="text-xs text-slate-500">
            1 = Very Easy, 2 = Easy, 3 = Medium, 4 = Hard, 5 = Very Hard
          </p>
          {errors.complexity && (
            <p className="text-sm text-red-600 dark:text-red-400">{errors.complexity}</p>
          )}
        </div>
      </div>

      {/* Navigation */}
      <div className="flex justify-between">
        <Button variant="outline" onClick={goBack}>
          ← Back
        </Button>
        <Button onClick={handleNext} disabled={!canGoNext()}>
          Next →
        </Button>
      </div>
    </div>
  );
}
