'use client';

/**
 * AddToWishlistDialog — Dialog for adding a game to the wishlist.
 *
 * Accepts an optional gameId to pre-fill the form.
 * Fields: gameId (UUID text), priority (low/medium/high), target price (optional), notes (optional).
 */

import React, { useState } from 'react';

import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/overlays/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { useAddToWishlist } from '@/hooks/queries/useWishlist';

// ============================================================================
// Types
// ============================================================================

interface AddToWishlistDialogProps {
  trigger?: React.ReactNode;
  /** Pre-filled game ID (disables the game ID input) */
  gameId?: string;
  /** Display name for the pre-filled game */
  gameName?: string;
  onSuccess?: () => void;
}

// ============================================================================
// Component
// ============================================================================

export function AddToWishlistDialog({
  trigger,
  gameId,
  gameName,
  onSuccess,
}: AddToWishlistDialogProps) {
  const [open, setOpen] = useState(false);

  const [formGameId, setFormGameId] = useState(gameId ?? '');
  const [priority, setPriority] = useState<'low' | 'medium' | 'high'>('medium');
  const [targetPrice, setTargetPrice] = useState('');
  const [notes, setNotes] = useState('');

  const { mutate: addToWishlist, isPending } = useAddToWishlist();

  function handleOpenChange(value: boolean) {
    setOpen(value);
    if (value) {
      // Reset form, but keep pre-filled gameId
      setFormGameId(gameId ?? '');
      setPriority('medium');
      setTargetPrice('');
      setNotes('');
    }
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();

    const parsedPrice = targetPrice !== '' ? parseFloat(targetPrice) : undefined;

    addToWishlist(
      {
        gameId: formGameId.trim(),
        priority,
        targetPrice: parsedPrice ?? null,
        notes: notes.trim() || null,
      },
      {
        onSuccess: () => {
          setOpen(false);
          onSuccess?.();
        },
      }
    );
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      {trigger ? (
        <DialogTrigger asChild>{trigger}</DialogTrigger>
      ) : (
        <DialogTrigger asChild>
          <Button>Add to Wishlist</Button>
        </DialogTrigger>
      )}

      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            {gameName ? `Add "${gameName}" to Wishlist` : 'Add to Wishlist'}
          </DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-4">
          {/* Game ID */}
          <div className="space-y-1.5">
            <Label htmlFor="wishlist-game-id">Game</Label>
            {gameId && gameName ? (
              <p className="text-sm font-medium py-2">{gameName}</p>
            ) : (
              <Input
                id="wishlist-game-id"
                placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
                value={formGameId}
                onChange={e => setFormGameId(e.target.value)}
                required
                disabled={!!gameId}
              />
            )}
          </div>

          {/* Priority */}
          <div className="space-y-1.5">
            <Label htmlFor="wishlist-priority">Priority</Label>
            <Select
              value={priority}
              onValueChange={v => setPriority(v as 'low' | 'medium' | 'high')}
            >
              <SelectTrigger id="wishlist-priority">
                <SelectValue placeholder="Select priority" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="low">Low</SelectItem>
                <SelectItem value="medium">Medium</SelectItem>
                <SelectItem value="high">High</SelectItem>
              </SelectContent>
            </Select>
          </div>

          {/* Target Price (optional) */}
          <div className="space-y-1.5">
            <Label htmlFor="wishlist-target-price">
              Target Price <span className="text-muted-foreground text-xs">(optional)</span>
            </Label>
            <Input
              id="wishlist-target-price"
              type="number"
              min="0"
              step="0.01"
              placeholder="e.g. 29.99"
              value={targetPrice}
              onChange={e => setTargetPrice(e.target.value)}
            />
          </div>

          {/* Notes (optional) */}
          <div className="space-y-1.5">
            <Label htmlFor="wishlist-notes">
              Notes <span className="text-muted-foreground text-xs">(optional)</span>
            </Label>
            <textarea
              id="wishlist-notes"
              rows={3}
              placeholder="Any notes about this game..."
              value={notes}
              onChange={e => setNotes(e.target.value)}
              className="flex w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-sm placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring resize-none"
            />
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={isPending || !formGameId.trim()}>
              {isPending ? 'Adding…' : 'Add to Wishlist'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
