/**
 * UserGameCard Component (Issue #2518)
 *
 * Enhanced library card with:
 * - Game cover image
 * - Title, BGG ID, publisher
 * - Agent configuration status
 * - PDF document status
 * - Actions: Chat, Configure Agent, Upload PDF, Edit Notes, Remove
 * - Favorite toggle
 */

'use client';

import { useState } from 'react';
import Image from 'next/image';
import Link from 'next/link';

import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { MessageCircle, Settings, Upload, Edit2, Trash2, Library } from 'lucide-react';

import { FavoriteToggle } from '@/components/library/FavoriteToggle';
import { useAgentConfig } from '@/hooks/queries';
import type { UserLibraryEntry } from '@/lib/api';

interface UserGameCardProps {
  game: UserLibraryEntry;
  onConfigureAgent: (gameId: string, gameTitle: string) => void;
  onUploadPdf: (gameId: string, gameTitle: string) => void;
  onEditNotes: (gameId: string, gameTitle: string, currentNotes?: string | null) => void;
  onRemove: (gameId: string, gameTitle: string) => void;
}

export function UserGameCard({
  game,
  onConfigureAgent,
  onUploadPdf,
  onEditNotes,
  onRemove,
}: UserGameCardProps) {
  // Fetch agent configuration status
  const { data: agentConfig } = useAgentConfig(game.gameId, true);

  // Agent status
  const agentConfigured = agentConfig !== null;
  const agentModel = agentConfig?.modelType || 'default';

  // Map model type to display name
  const modelDisplayName: Record<string, string> = {
    'llama-3.3-70b-free': 'Llama Free',
    'google-gemini-pro': 'Gemini Pro',
    'deepseek-chat': 'DeepSeek',
    'llama-3.3-70b': 'Llama Pro',
    default: 'Default',
  };

  return (
    <Card className="flex flex-col hover:shadow-lg transition-shadow" data-testid="game-card">
      <CardHeader className="p-0">
        {/* Cover Image */}
        <div className="relative w-full h-40 bg-muted">
          {game.gameImageUrl ? (
            <Image
              src={game.gameImageUrl}
              alt={game.gameTitle}
              fill
              className="object-cover rounded-t-lg"
              sizes="(max-width: 768px) 100vw, (max-width: 1024px) 50vw, 33vw"
            />
          ) : (
            <div className="flex items-center justify-center h-full text-muted-foreground">
              <Library className="h-12 w-12" />
            </div>
          )}
          {/* Favorite Toggle */}
          <div className="absolute top-2 right-2">
            <FavoriteToggle
              gameId={game.gameId}
              isFavorite={game.isFavorite}
              gameTitle={game.gameTitle}
              size="sm"
            />
          </div>
        </div>
      </CardHeader>

      <CardContent className="flex-1 p-4 space-y-3">
        {/* Title & Publisher */}
        <div>
          <h3 className="font-semibold text-lg line-clamp-2">{game.gameTitle}</h3>
          {game.gamePublisher && (
            <p className="text-sm text-muted-foreground">{game.gamePublisher}</p>
          )}
        </div>

        {/* Agent Configuration Status */}
        <div className="flex items-center gap-2 text-sm">
          <span className="text-muted-foreground">🤖 Agente:</span>
          {agentConfigured ? (
            <Badge variant="secondary" className="text-xs">
              Configurato ({modelDisplayName[agentModel]})
            </Badge>
          ) : (
            <Badge variant="outline" className="text-xs">
              Non configurato
            </Badge>
          )}
        </div>

        {/* PDF Document Status */}
        <div className="flex items-center gap-2 text-sm">
          <span className="text-muted-foreground">📄 PDF:</span>
          <Badge variant="outline" className="text-xs">
            {/* TODO: Implement PDF status check when endpoint available */}
            Regolamento Standard
          </Badge>
        </div>

        {/* Notes Preview */}
        {game.notes && (
          <p className="text-sm text-muted-foreground line-clamp-2 pt-2 border-t">
            {game.notes}
          </p>
        )}

        {/* Action Buttons */}
        <div className="space-y-2 pt-2">
          {/* Primary Actions Row */}
          <div className="flex gap-2">
            <Button asChild variant="default" size="sm" className="flex-1">
              <Link href={`/chat?gameId=${game.gameId}`}>
                <MessageCircle className="mr-1 h-3 w-3" />
                Chatta
              </Link>
            </Button>
            <Button
              variant="outline"
              size="sm"
              className="flex-1"
              onClick={() => onConfigureAgent(game.gameId, game.gameTitle)}
            >
              <Settings className="mr-1 h-3 w-3" />
              Gestisci Agente
            </Button>
          </div>

          {/* Secondary Actions Row */}
          <div className="flex gap-2">
            <Button
              variant="ghost"
              size="sm"
              className="flex-1"
              onClick={() => onUploadPdf(game.gameId, game.gameTitle)}
            >
              <Upload className="mr-1 h-3 w-3" />
              Carica PDF
            </Button>
            <Button
              variant="ghost"
              size="sm"
              className="flex-1"
              onClick={() => onEditNotes(game.gameId, game.gameTitle, game.notes)}
            >
              <Edit2 className="mr-1 h-3 w-3" />
              Note
            </Button>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => onRemove(game.gameId, game.gameTitle)}
              className="text-destructive hover:text-destructive"
            >
              <Trash2 className="h-3 w-3" />
            </Button>
          </div>
        </div>

        {/* Added Date */}
        <p className="text-xs text-muted-foreground pt-2 border-t">
          Aggiunto il {new Date(game.addedAt).toLocaleDateString('it-IT')}
        </p>
      </CardContent>
    </Card>
  );
}
