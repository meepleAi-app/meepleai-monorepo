'use client';

/**
 * Game Preview Card Component (Issue #2515)
 *
 * Real-time preview of how the game will appear in the catalog.
 * Updates as user fills the edit form.
 */

import { Users, Clock, BarChart3, Image as ImageIcon } from 'lucide-react';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Badge } from '@/components/ui/data-display/badge';

interface GamePreviewCardProps {
  formData: any; // Form data from useGameEdit hook
}

export function GamePreviewCard({ formData }: GamePreviewCardProps) {
  const {
    name = 'Untitled Game',
    description = 'No description provided',
    minPlayers = 1,
    maxPlayers = 4,
    playingTime = 60,
    complexity = 3,
    tags = [],
    coverImageUrl,
  } = formData || {};

  return (
    <Card className="max-w-md mx-auto">
      <CardHeader className="space-y-4">
        {/* Cover Image */}
        <div className="aspect-video bg-muted rounded-md flex items-center justify-center overflow-hidden">
          {coverImageUrl ? (
            <img
              src={coverImageUrl}
              alt={name}
              className="w-full h-full object-cover"
            />
          ) : (
            <div className="flex flex-col items-center justify-center text-muted-foreground">
              <ImageIcon className="h-12 w-12 mb-2" />
              <p className="text-sm">No cover image</p>
            </div>
          )}
        </div>

        {/* Title */}
        <div>
          <CardTitle className="text-2xl">{name}</CardTitle>
          <CardDescription className="mt-2">{description}</CardDescription>
        </div>
      </CardHeader>

      <CardContent className="space-y-4">
        {/* Game Stats */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2 text-sm">
            <Users className="h-4 w-4 text-muted-foreground" />
            <span>
              {minPlayers}-{maxPlayers} players
            </span>
          </div>

          <div className="flex items-center gap-2 text-sm">
            <Clock className="h-4 w-4 text-muted-foreground" />
            <span>{playingTime} min</span>
          </div>

          <div className="flex items-center gap-2 text-sm">
            <BarChart3 className="h-4 w-4 text-muted-foreground" />
            <span>Complexity {complexity}/5</span>
          </div>
        </div>

        {/* Tags */}
        {tags.length > 0 && (
          <div className="flex flex-wrap gap-2">
            {tags.map((tag: string) => (
              <Badge key={tag} variant="secondary">
                {tag}
              </Badge>
            ))}
          </div>
        )}

        {/* Preview Notice */}
        <div className="text-xs text-muted-foreground pt-2 border-t">
          This is how the game will appear in the catalog after publication.
        </div>
      </CardContent>
    </Card>
  );
}
