/**
 * PlayerManager - Add/Remove Players for Play Records
 *
 * Features:
 * - Add registered users (autocomplete search)
 * - Add guest players (free-form name)
 * - Remove players
 * - Player list with user/guest indicators
 * - No duplicate validation
 *
 * Issue #3892: Play Records Frontend UI
 */

'use client';

import { useState } from 'react';

import { zodResolver } from '@hookform/resolvers/zod';
import { UserPlus, X, User, UserCircle } from 'lucide-react';
import { useForm } from 'react-hook-form';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/forms/form';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { RadioGroup, RadioGroupItem } from '@/components/ui/primitives/radio-group';
import { PlayerAddFormSchema, type PlayerAddForm } from '@/lib/api/schemas/play-records.schemas';
import type { SessionPlayer } from '@/lib/api/schemas/play-records.schemas';

export interface PlayerManagerProps {
  players: SessionPlayer[];
  onAddPlayer: (player: { userId?: string; displayName: string }) => Promise<void>;
  onRemovePlayer?: (playerId: string) => void;
  maxPlayers?: number;
  isAddingPlayer?: boolean;
  recordId?: string; // If editing existing record
}

export function PlayerManager({
  players,
  onAddPlayer,
  onRemovePlayer,
  maxPlayers = 20,
  isAddingPlayer = false,
}: PlayerManagerProps) {
  const [isAdding, setIsAdding] = useState(false);

  const form = useForm<PlayerAddForm>({
    resolver: zodResolver(PlayerAddFormSchema),
    defaultValues: {
      playerType: 'guest',
      displayName: '',
    },
  });

  const handleAddPlayer = form.handleSubmit(async (data) => {
    await onAddPlayer({
      userId: data.userId,
      displayName: data.displayName,
    });
    form.reset();
    setIsAdding(false);
  });

  const canAddMore = players.length < maxPlayers;

  return (
    <div className="space-y-4">
      {/* Player List */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <h3 className="text-lg font-semibold">
            Players ({players.length}/{maxPlayers})
          </h3>
          {!isAdding && canAddMore && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => setIsAdding(true)}
            >
              <UserPlus className="w-4 h-4 mr-2" />
              Add Player
            </Button>
          )}
        </div>

        {players.length === 0 && !isAdding && (
          <div className="text-center py-8 text-muted-foreground">
            <UserCircle className="w-12 h-12 mx-auto mb-2 opacity-30" />
            <p>No players added yet</p>
            <p className="text-sm">Click &quot;Add Player&quot; to start</p>
          </div>
        )}

        {players.length > 0 && (
          <div className="space-y-2">
            {players.map((player, index) => (
              <div
                key={player.id}
                className="flex items-center justify-between p-3 rounded-lg border bg-card"
              >
                <div className="flex items-center gap-3">
                  <span className="flex items-center justify-center w-8 h-8 rounded-full bg-primary/10 text-primary font-semibold text-sm">
                    {index + 1}
                  </span>
                  <div>
                    <p className="font-medium">{player.displayName}</p>
                    {player.userId ? (
                      <Badge variant="secondary" className="mt-1">
                        <User className="w-3 h-3 mr-1" />
                        Registered
                      </Badge>
                    ) : (
                      <Badge variant="outline" className="mt-1">
                        Guest
                      </Badge>
                    )}
                  </div>
                </div>

                {onRemovePlayer && (
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    onClick={() => onRemovePlayer(player.id)}
                  >
                    <X className="w-4 h-4" />
                  </Button>
                )}
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Add Player Form */}
      {isAdding && (
        <div className="p-4 rounded-lg border bg-muted/30">
          <Form {...form}>
            <form onSubmit={handleAddPlayer} className="space-y-4">
              <FormField
                control={form.control}
                name="playerType"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Player Type</FormLabel>
                    <FormControl>
                      <RadioGroup
                        onValueChange={field.onChange}
                        defaultValue={field.value}
                        className="flex gap-4"
                      >
                        <div className="flex items-center space-x-2">
                          <RadioGroupItem value="guest" id="guest" />
                          <Label htmlFor="guest">Guest</Label>
                        </div>
                        <div className="flex items-center space-x-2">
                          <RadioGroupItem value="user" id="user" />
                          <Label htmlFor="user">Registered User</Label>
                        </div>
                      </RadioGroup>
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              {form.watch('playerType') === 'user' ? (
                <FormField
                  control={form.control}
                  name="userId"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Search User</FormLabel>
                      <FormControl>
                        <Input
                          placeholder="Search by username or email..."
                          {...field}
                          value={field.value || ''}
                        />
                      </FormControl>
                      <FormDescription>
                        {/* TODO: Implement user autocomplete */}
                        User search coming soon
                      </FormDescription>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              ) : (
                <FormField
                  control={form.control}
                  name="displayName"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Player Name</FormLabel>
                      <FormControl>
                        <Input placeholder="Enter player name..." {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              )}

              <div className="flex gap-2 justify-end">
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  onClick={() => {
                    setIsAdding(false);
                    form.reset();
                  }}
                  disabled={isAddingPlayer}
                >
                  Cancel
                </Button>
                <Button type="submit" size="sm" disabled={isAddingPlayer}>
                  {isAddingPlayer ? 'Adding...' : 'Add Player'}
                </Button>
              </div>
            </form>
          </Form>
        </div>
      )}
    </div>
  );
}
