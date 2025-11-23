import React, { useEffect, useState } from 'react';
import { Game, BggGameDetails, api } from '@/lib/api';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { Separator } from '@/components/ui/separator';
import { Skeleton } from '@/components/ui/skeleton';
import { Users, Clock, Calendar, Star, TrendingUp, ExternalLink } from 'lucide-react';
import { Button } from '@/components/ui/button';

interface GameDetailModalProps {
  game: Game | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function GameDetailModal({ game, open, onOpenChange }: GameDetailModalProps) {
  const [bggDetails, setBggDetails] = useState<BggGameDetails | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open || !game?.bggId) {
      setBggDetails(null);
      setError(null);
      return;
    }

    const fetchBggDetails = async () => {
      setLoading(true);
      setError(null);
      try {
        const details = await api.bgg.getGameDetails(game.bggId!);
        setBggDetails(details);
      } catch (err) {
        setError('Failed to load BGG details');
        console.error('BGG fetch error:', err);
      } finally {
        setLoading(false);
      }
    };

    fetchBggDetails();
  }, [open, game]);

  if (!game) return null;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-start justify-between gap-2">
            <span>{game.title}</span>
            {game.bggId && (
              <Badge variant="secondary">BGG</Badge>
            )}
          </DialogTitle>
          <DialogDescription>
            {game.publisher || 'Board game details'}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          {/* Basic Game Info */}
          <div className="flex flex-wrap gap-4 text-sm">
            {(game.minPlayers !== null || game.maxPlayers !== null) && (
              <div className="flex items-center gap-2">
                <Users className="h-4 w-4 text-muted-foreground" />
                <span>
                  {game.minPlayers === game.maxPlayers
                    ? `${game.minPlayers} players`
                    : `${game.minPlayers || '?'}-${game.maxPlayers || '?'} players`}
                </span>
              </div>
            )}

            {(game.minPlayTimeMinutes !== null || game.maxPlayTimeMinutes !== null) && (
              <div className="flex items-center gap-2">
                <Clock className="h-4 w-4 text-muted-foreground" />
                <span>
                  {game.minPlayTimeMinutes === game.maxPlayTimeMinutes
                    ? `${game.minPlayTimeMinutes} min`
                    : `${game.minPlayTimeMinutes || '?'}-${game.maxPlayTimeMinutes || '?'} min`}
                </span>
              </div>
            )}

            {game.yearPublished && (
              <div className="flex items-center gap-2">
                <Calendar className="h-4 w-4 text-muted-foreground" />
                <span>{game.yearPublished}</span>
              </div>
            )}
          </div>

          {/* BGG Details */}
          {game.bggId && (
            <>
              <Separator />

              {loading && (
                <div className="space-y-3">
                  <Skeleton className="h-4 w-full" />
                  <Skeleton className="h-4 w-full" />
                  <Skeleton className="h-4 w-3/4" />
                </div>
              )}

              {error && (
                <div className="text-sm text-destructive">{error}</div>
              )}

              {bggDetails && (
                <div className="space-y-4">
                  {/* BGG Image */}
                  {bggDetails.imageUrl && (
                    <div className="flex justify-center">
                      <img
                        src={bggDetails.imageUrl}
                        alt={bggDetails.name}
                        className="max-h-64 rounded-md object-contain"
                      />
                    </div>
                  )}

                  {/* BGG Ratings */}
                  <div className="grid grid-cols-2 gap-4 text-sm">
                    {bggDetails.averageRating && (
                      <div className="flex items-center gap-2">
                        <Star className="h-4 w-4 text-yellow-500" />
                        <span>Rating: {bggDetails.averageRating.toFixed(2)}</span>
                      </div>
                    )}

                    {bggDetails.averageWeight && (
                      <div className="flex items-center gap-2">
                        <TrendingUp className="h-4 w-4 text-muted-foreground" />
                        <span>Complexity: {bggDetails.averageWeight.toFixed(2)}/5</span>
                      </div>
                    )}

                    {bggDetails.usersRated && (
                      <div className="text-muted-foreground">
                        {bggDetails.usersRated.toLocaleString()} ratings
                      </div>
                    )}
                  </div>

                  {/* Description */}
                  {bggDetails.description && (
                    <>
                      <Separator />
                      <div>
                        <h4 className="font-semibold mb-2">Description</h4>
                        <div
                          className="text-sm text-muted-foreground prose prose-sm max-w-none"
                          dangerouslySetInnerHTML={{ __html: bggDetails.description }}
                        />
                      </div>
                    </>
                  )}

                  {/* Categories & Mechanics */}
                  {(bggDetails.categories.length > 0 || bggDetails.mechanics.length > 0) && (
                    <>
                      <Separator />
                      <div className="space-y-3">
                        {bggDetails.categories.length > 0 && (
                          <div>
                            <h4 className="font-semibold mb-2 text-sm">Categories</h4>
                            <div className="flex flex-wrap gap-2">
                              {bggDetails.categories.map((category) => (
                                <Badge key={category} variant="secondary">
                                  {category}
                                </Badge>
                              ))}
                            </div>
                          </div>
                        )}

                        {bggDetails.mechanics.length > 0 && (
                          <div>
                            <h4 className="font-semibold mb-2 text-sm">Mechanics</h4>
                            <div className="flex flex-wrap gap-2">
                              {bggDetails.mechanics.map((mechanic) => (
                                <Badge key={mechanic} variant="outline">
                                  {mechanic}
                                </Badge>
                              ))}
                            </div>
                          </div>
                        )}
                      </div>
                    </>
                  )}

                  {/* Designers & Publishers */}
                  {(bggDetails.designers.length > 0 || bggDetails.publishers.length > 0) && (
                    <>
                      <Separator />
                      <div className="grid grid-cols-2 gap-4 text-sm">
                        {bggDetails.designers.length > 0 && (
                          <div>
                            <h4 className="font-semibold mb-1">Designers</h4>
                            <ul className="list-disc list-inside text-muted-foreground">
                              {bggDetails.designers.slice(0, 3).map((designer) => (
                                <li key={designer}>{designer}</li>
                              ))}
                            </ul>
                          </div>
                        )}

                        {bggDetails.publishers.length > 0 && (
                          <div>
                            <h4 className="font-semibold mb-1">Publishers</h4>
                            <ul className="list-disc list-inside text-muted-foreground">
                              {bggDetails.publishers.slice(0, 3).map((publisher) => (
                                <li key={publisher}>{publisher}</li>
                              ))}
                            </ul>
                          </div>
                        )}
                      </div>
                    </>
                  )}

                  {/* BGG Link */}
                  <Button
                    variant="outline"
                    size="sm"
                    asChild
                    className="w-full"
                  >
                    <a
                      href={`https://boardgamegeek.com/boardgame/${game.bggId}`}
                      target="_blank"
                      rel="noopener noreferrer"
                    >
                      View on BoardGameGeek
                      <ExternalLink className="ml-2 h-4 w-4" />
                    </a>
                  </Button>
                </div>
              )}
            </>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
