'use client';

/**
 * Game Edit Form Component (Issue #2515)
 *
 * Form fields for game configuration.
 * Simplified version focusing on admin dashboard requirements.
 *
 * Fields:
 * - Name (readonly in edit mode)
 * - BGG ID (readonly in edit mode)
 * - BGG URL (validation)
 * - Description (textarea, max 2000 chars)
 * - Upload PDF (max 100MB)
 * - Cover Image (file or URL, max 5MB)
 * - Complexity (slider 1-5)
 * - Players Min/Max
 * - Average Duration (minutes)
 * - Tags (multi-select)
 */

import { useState } from 'react';

import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Slider } from '@/components/ui/slider';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Upload, Image as ImageIcon, X } from 'lucide-react';
import { toast } from '@/components/layout/Toast';

import { type SharedGameDetail } from '@/lib/api/schemas/shared-games.schemas';

interface GameEditFormProps {
  game?: SharedGameDetail | null;
  onChange: (field: string, value: any) => void;
  isSubmitting: boolean;
}

const AVAILABLE_TAGS = [
  'Strategy',
  'Party',
  'Cooperative',
  'Competitive',
  'Family',
  'Card Game',
  'Board Game',
  'Dice Game',
  'War Game',
  'Euro Game',
];

export function GameEditForm({ game, onChange, isSubmitting }: GameEditFormProps) {
  const isEditMode = Boolean(game);

  // Form state (controlled externally via onChange)
  const [selectedTags, setSelectedTags] = useState<string[]>([]);
  const [complexity, setComplexity] = useState(3);
  const [pdfFile, setPdfFile] = useState<File | null>(null);
  const [imageFile, setImageFile] = useState<File | null>(null);

  const handleTagToggle = (tag: string) => {
    const newTags = selectedTags.includes(tag)
      ? selectedTags.filter((t) => t !== tag)
      : [...selectedTags, tag];
    setSelectedTags(newTags);
    onChange('tags', newTags);
  };

  const handlePdfUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (file.size > 100 * 1024 * 1024) {
      toast.error('PDF file must be less than 100MB');
      return;
    }

    setPdfFile(file);
    onChange('pdfDocument', file);
  };

  const handleImageUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (file.size > 5 * 1024 * 1024) {
      toast.error('Image file must be less than 5MB');
      return;
    }

    setImageFile(file);
    onChange('coverImage', file);
  };

  return (
    <div className="space-y-6">
      {/* Basic Info */}
      <div className="grid grid-cols-2 gap-4">
        <div className="space-y-2">
          <Label htmlFor="name">
            Name {isEditMode && <span className="text-muted-foreground">(readonly)</span>}
          </Label>
          <Input
            id="name"
            defaultValue={game?.title || ''}
            onChange={(e) => onChange('name', e.target.value)}
            disabled={isEditMode || isSubmitting}
            placeholder="Game name"
          />
        </div>

        <div className="space-y-2">
          <Label htmlFor="bggId">
            BGG ID {isEditMode && <span className="text-muted-foreground">(readonly)</span>}
          </Label>
          <Input
            id="bggId"
            type="number"
            defaultValue={game?.bggId || ''}
            onChange={(e) => onChange('bggId', e.target.value)}
            disabled={isEditMode || isSubmitting}
            placeholder="BoardGameGeek ID"
          />
        </div>
      </div>

      {/* BGG URL */}
      <div className="space-y-2">
        <Label htmlFor="bggUrl">BGG URL</Label>
        <Input
          id="bggUrl"
          type="url"
          defaultValue={game?.bggId ? `https://boardgamegeek.com/boardgame/${game.bggId}` : ''}
          onChange={(e) => onChange('bggUrl', e.target.value)}
          disabled={isSubmitting}
          placeholder="https://boardgamegeek.com/boardgame/13"
        />
      </div>

      {/* Description */}
      <div className="space-y-2">
        <Label htmlFor="description">Description</Label>
        <Textarea
          id="description"
          defaultValue={game?.description || ''}
          onChange={(e) => onChange('description', e.target.value)}
          disabled={isSubmitting}
          placeholder="Game description (max 2000 characters)"
          rows={5}
          maxLength={2000}
        />
      </div>

      {/* File Uploads */}
      <div className="grid grid-cols-2 gap-4">
        <div className="space-y-2">
          <Label htmlFor="pdf-upload">PDF Rulebook (max 100MB)</Label>
          <div className="flex items-center gap-2">
            <Input
              id="pdf-upload"
              type="file"
              accept=".pdf"
              onChange={handlePdfUpload}
              disabled={isSubmitting}
            />
            {pdfFile && (
              <Button
                variant="ghost"
                size="icon"
                onClick={() => {
                  setPdfFile(null);
                  onChange('pdfDocument', null);
                }}
              >
                <X className="h-4 w-4" />
              </Button>
            )}
          </div>
          {pdfFile && (
            <p className="text-xs text-muted-foreground">
              {pdfFile.name} ({(pdfFile.size / 1024 / 1024).toFixed(2)} MB)
            </p>
          )}
        </div>

        <div className="space-y-2">
          <Label htmlFor="image-upload">Cover Image (max 5MB)</Label>
          <div className="flex items-center gap-2">
            <Input
              id="image-upload"
              type="file"
              accept="image/*"
              onChange={handleImageUpload}
              disabled={isSubmitting}
            />
            {imageFile && (
              <Button
                variant="ghost"
                size="icon"
                onClick={() => {
                  setImageFile(null);
                  onChange('coverImage', null);
                }}
              >
                <X className="h-4 w-4" />
              </Button>
            )}
          </div>
          {imageFile && (
            <p className="text-xs text-muted-foreground">
              {imageFile.name} ({(imageFile.size / 1024 / 1024).toFixed(2)} MB)
            </p>
          )}
        </div>
      </div>

      {/* Complexity Slider */}
      <div className="space-y-2">
        <Label>Complexity (1-5)</Label>
        <div className="flex items-center gap-4">
          <Slider
            value={[complexity]}
            onValueChange={(values) => {
              setComplexity(values[0]);
              onChange('complexity', values[0]);
            }}
            min={1}
            max={5}
            step={1}
            disabled={isSubmitting}
            className="flex-1"
          />
          <span className="text-sm font-medium w-8 text-center">{complexity}</span>
        </div>
      </div>

      {/* Players & Duration */}
      <div className="grid grid-cols-3 gap-4">
        <div className="space-y-2">
          <Label htmlFor="minPlayers">Min Players</Label>
          <Input
            id="minPlayers"
            type="number"
            min={1}
            max={100}
            defaultValue={game?.minPlayers || 1}
            onChange={(e) => onChange('minPlayers', parseInt(e.target.value))}
            disabled={isSubmitting}
          />
        </div>

        <div className="space-y-2">
          <Label htmlFor="maxPlayers">Max Players</Label>
          <Input
            id="maxPlayers"
            type="number"
            min={1}
            max={100}
            defaultValue={game?.maxPlayers || 4}
            onChange={(e) => onChange('maxPlayers', parseInt(e.target.value))}
            disabled={isSubmitting}
          />
        </div>

        <div className="space-y-2">
          <Label htmlFor="playingTime">Avg Duration (min)</Label>
          <Input
            id="playingTime"
            type="number"
            min={1}
            defaultValue={game?.playingTimeMinutes || 60}
            onChange={(e) => onChange('playingTime', parseInt(e.target.value))}
            disabled={isSubmitting}
          />
        </div>
      </div>

      {/* Tags Multi-Select */}
      <div className="space-y-2">
        <Label>Tags</Label>
        <div className="flex flex-wrap gap-2">
          {AVAILABLE_TAGS.map((tag) => (
            <Badge
              key={tag}
              variant={selectedTags.includes(tag) ? 'default' : 'outline'}
              className="cursor-pointer"
              onClick={() => !isSubmitting && handleTagToggle(tag)}
            >
              {tag}
            </Badge>
          ))}
        </div>
      </div>
    </div>
  );
}
