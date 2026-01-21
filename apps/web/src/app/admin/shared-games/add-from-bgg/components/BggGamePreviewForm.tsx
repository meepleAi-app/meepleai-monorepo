'use client';

/**
 * BGG Game Preview Form Component
 * Issue: Admin Add Shared Game from BGG flow
 *
 * Features:
 * - Displays BGG game data in read-only preview
 * - Shows all imported fields
 * - Confirm/Cancel actions
 */

import { ExternalLink, Gamepad2, Users, Clock, BarChart3, Star, Loader2 } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { Separator } from '@/components/ui/navigation/separator';
import type { BggGameDetails } from '@/lib/api/schemas/shared-games.schemas';

export interface BggGamePreviewFormProps {
  bggData: BggGameDetails;
  onConfirm: () => void;
  onCancel: () => void;
  isSubmitting: boolean;
}

// Helper to strip HTML tags and decode entities
function stripHtml(html: string): string {
  // Remove HTML tags
  let text = html.replace(/<[^>]*>/g, '');
  // Decode common HTML entities
  text = text
    .replace(/&nbsp;/g, ' ')
    .replace(/&amp;/g, '&')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&#39;/g, "'")
    .replace(/&#x27;/g, "'")
    .replace(/&#x2F;/g, '/')
    .replace(/&mdash;/g, '—')
    .replace(/&ndash;/g, '–')
    .replace(/&hellip;/g, '…');
  // Remove extra whitespace
  return text.replace(/\s+/g, ' ').trim();
}

export function BggGamePreviewForm({
  bggData,
  onConfirm,
  onCancel,
  isSubmitting,
}: BggGamePreviewFormProps) {
  const plainDescription = stripHtml(bggData.description);
  const truncatedDescription = plainDescription.length > 500
    ? plainDescription.substring(0, 500) + '...'
    : plainDescription;

  return (
    <Card>
      <CardHeader>
        <div className="flex items-start gap-4">
          {/* Thumbnail */}
          {bggData.thumbnailUrl ? (
            <img
              src={bggData.thumbnailUrl}
              alt={bggData.name}
              className="h-24 w-24 rounded-lg object-cover flex-shrink-0"
            />
          ) : (
            <div className="h-24 w-24 rounded-lg bg-muted flex items-center justify-center flex-shrink-0">
              <Gamepad2 className="h-10 w-10 text-muted-foreground" />
            </div>
          )}

          <div className="flex-1 min-w-0">
            <CardTitle className="text-2xl">{bggData.name}</CardTitle>
            <CardDescription className="flex items-center gap-2 mt-1">
              {bggData.yearPublished && <span>({bggData.yearPublished})</span>}
              <a
                href={`https://boardgamegeek.com/boardgame/${bggData.bggId}`}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center gap-1 text-primary hover:underline"
              >
                Vedi su BGG
                <ExternalLink className="h-3 w-3" />
              </a>
            </CardDescription>
          </div>
        </div>
      </CardHeader>

      <CardContent className="space-y-6">
        {/* Description */}
        <div>
          <h4 className="font-medium mb-2">Descrizione</h4>
          <p className="text-sm text-muted-foreground leading-relaxed">
            {truncatedDescription || 'Nessuna descrizione disponibile'}
          </p>
        </div>

        <Separator />

        {/* Game Info Grid */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          {/* Players */}
          <div className="space-y-1">
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <Users className="h-4 w-4" />
              Giocatori
            </div>
            <p className="font-medium">
              {bggData.minPlayers === bggData.maxPlayers
                ? bggData.minPlayers
                : `${bggData.minPlayers}-${bggData.maxPlayers}`}
            </p>
          </div>

          {/* Playing Time */}
          <div className="space-y-1">
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <Clock className="h-4 w-4" />
              Durata
            </div>
            <p className="font-medium">
              {bggData.minPlaytime === bggData.maxPlaytime
                ? `${bggData.minPlaytime} min`
                : `${bggData.minPlaytime}-${bggData.maxPlaytime} min`}
            </p>
          </div>

          {/* Complexity */}
          <div className="space-y-1">
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <BarChart3 className="h-4 w-4" />
              Complessità
            </div>
            <p className="font-medium">
              {bggData.complexity ? `${bggData.complexity.toFixed(2)} / 5` : 'N/A'}
            </p>
          </div>

          {/* Rating */}
          <div className="space-y-1">
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <Star className="h-4 w-4" />
              Voto BGG
            </div>
            <p className="font-medium">
              {bggData.averageRating ? `${bggData.averageRating.toFixed(1)} / 10` : 'N/A'}
            </p>
          </div>
        </div>

        <Separator />

        {/* Categories and Mechanics */}
        <div className="grid md:grid-cols-2 gap-6">
          {/* Categories */}
          {bggData.categories && bggData.categories.length > 0 && (
            <div>
              <h4 className="font-medium mb-2">Categorie</h4>
              <div className="flex flex-wrap gap-1.5">
                {bggData.categories.map((category) => (
                  <Badge key={category} variant="secondary">
                    {category}
                  </Badge>
                ))}
              </div>
            </div>
          )}

          {/* Mechanics */}
          {bggData.mechanics && bggData.mechanics.length > 0 && (
            <div>
              <h4 className="font-medium mb-2">Meccaniche</h4>
              <div className="flex flex-wrap gap-1.5">
                {bggData.mechanics.map((mechanic) => (
                  <Badge key={mechanic} variant="outline">
                    {mechanic}
                  </Badge>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Designers and Publishers */}
        <div className="grid md:grid-cols-2 gap-6">
          {/* Designers */}
          {bggData.designers && bggData.designers.length > 0 && (
            <div>
              <h4 className="font-medium mb-2">Autori</h4>
              <p className="text-sm text-muted-foreground">
                {bggData.designers.join(', ')}
              </p>
            </div>
          )}

          {/* Publishers */}
          {bggData.publishers && bggData.publishers.length > 0 && (
            <div>
              <h4 className="font-medium mb-2">Editori</h4>
              <p className="text-sm text-muted-foreground">
                {bggData.publishers.slice(0, 5).join(', ')}
                {bggData.publishers.length > 5 && ` e altri ${bggData.publishers.length - 5}`}
              </p>
            </div>
          )}
        </div>
      </CardContent>

      <CardFooter className="flex justify-end gap-3 bg-muted/50">
        <Button variant="outline" onClick={onCancel} disabled={isSubmitting}>
          Annulla
        </Button>
        <Button onClick={onConfirm} disabled={isSubmitting}>
          {isSubmitting ? (
            <>
              <Loader2 className="h-4 w-4 mr-2 animate-spin" />
              Importazione...
            </>
          ) : (
            'Conferma Importazione'
          )}
        </Button>
      </CardFooter>
    </Card>
  );
}
