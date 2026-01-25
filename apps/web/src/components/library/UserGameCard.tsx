/**
 * UserGameCard Component (Issue #2518, #2613, #2618)
 *
 * Enhanced library card with:
 * - Game cover image
 * - Title, BGG ID, publisher
 * - Agent configuration status
 * - PDF document status
 * - Actions: Chat, Configure Agent, Upload PDF, Edit Notes, Remove
 * - Favorite toggle
 * - Selection mode with checkbox (Issue #2613)
 * - Framer Motion animations (Issue #2618)
 */

'use client';

import { motion } from 'framer-motion';
import { MessageCircle, Settings, Upload, Edit2, Trash2, Library, Share2 } from 'lucide-react';
import Image from 'next/image';
import Link from 'next/link';


import { FavoriteToggle } from '@/components/library/FavoriteToggle';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { useAgentConfig } from '@/hooks/queries';
import { useCanShareGame } from '@/hooks/queries/useCanShareGame';
import type { UserLibraryEntry } from '@/lib/api';
import { cn } from '@/lib/utils';

interface UserGameCardProps {
  game: UserLibraryEntry;
  onConfigureAgent: (gameId: string, gameTitle: string) => void;
  onUploadPdf: (gameId: string, gameTitle: string) => void;
  onEditNotes: (gameId: string, gameTitle: string, currentNotes?: string | null) => void;
  onRemove: (gameId: string, gameTitle: string) => void;
  /** Share game with community (Issue #2743) */
  onShare?: (gameId: string, gameTitle: string) => void;
  /** Selection mode props (Issue #2613) */
  selectionMode?: boolean;
  isSelected?: boolean;
  onSelect?: (gameId: string, shiftKey: boolean) => void;
  /** Animation props for staggered entrance (Issue #2618) */
  index?: number;
}

// Animation variants for card entrance (Issue #2618)
const cardVariants = {
  hidden: {
    opacity: 0,
    y: 20,
    scale: 0.95,
  },
  visible: (index: number) => ({
    opacity: 1,
    y: 0,
    scale: 1,
    transition: {
      delay: index * 0.05, // Staggered delay based on index
      duration: 0.3,
      ease: 'easeOut' as const,
    },
  }),
  exit: {
    opacity: 0,
    scale: 0.95,
    transition: {
      duration: 0.2,
    },
  },
};

export function UserGameCard({
  game,
  onConfigureAgent,
  onUploadPdf,
  onEditNotes,
  onRemove,
  onShare,
  selectionMode = false,
  isSelected = false,
  onSelect,
  index = 0,
}: UserGameCardProps) {
  // Fetch agent configuration status
  const { data: agentConfig } = useAgentConfig(game.gameId, true);

  // Check if game can be shared (Issue #2743)
  const { canShare, reason: shareBlockReason } = useCanShareGame(game.gameId);

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

  const handleCardClick = (e: React.MouseEvent) => {
    if (selectionMode && onSelect) {
      e.preventDefault();
      e.stopPropagation();
      onSelect(game.gameId, e.shiftKey);
    }
  };

  const handleCheckboxChange = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (onSelect) {
      onSelect(game.gameId, e.shiftKey);
    }
  };

  return (
    <motion.div
      variants={cardVariants}
      initial="hidden"
      animate="visible"
      exit="exit"
      custom={index}
      layout
      whileHover={{ y: -4, transition: { duration: 0.2 } }}
    >
      <Card
        className={cn(
          'flex flex-col hover:shadow-lg transition-shadow',
          selectionMode && 'cursor-pointer',
          isSelected && 'ring-2 ring-primary bg-primary/5'
        )}
        data-testid="game-card"
        onClick={handleCardClick}
      >
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

          {/* Selection Checkbox (Issue #2613) */}
          {selectionMode && (
            <div
              className="absolute top-2 left-2 z-10"
              onClick={handleCheckboxChange}
            >
              <Checkbox
                checked={isSelected}
                className="h-5 w-5 bg-background border-2 shadow-md"
                aria-label={`Seleziona ${game.gameTitle}`}
              />
            </div>
          )}

          {/* Favorite Toggle - hide in selection mode */}
          {!selectionMode && (
            <div className="absolute top-2 right-2">
              <FavoriteToggle
                gameId={game.gameId}
                isFavorite={game.isFavorite}
                gameTitle={game.gameTitle}
                size="sm"
              />
            </div>
          )}
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
              {/* eslint-disable-next-line security/detect-object-injection -- agentModel is from controlled config */}
              Configurato ({modelDisplayName[agentModel]})
            </Badge>
          ) : (
            <Badge variant="outline" className="text-xs" data-testid="agent-status-badge">
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

          {/* Share with Community Button (Issue #2743) */}
          {onShare && (
            <Button
              variant="outline"
              size="sm"
              className="w-full"
              onClick={() => onShare(game.gameId, game.gameTitle)}
              disabled={!canShare}
              title={shareBlockReason || 'Share this game with the community'}
            >
              <Share2 className="mr-2 h-3 w-3" />
              {canShare ? 'Share with Community' : shareBlockReason || 'Cannot Share'}
            </Button>
          )}
        </div>

        {/* Added Date */}
        <p className="text-xs text-muted-foreground pt-2 border-t">
          Aggiunto il {new Date(game.addedAt).toLocaleDateString('it-IT')}
        </p>
      </CardContent>
      </Card>
    </motion.div>
  );
}
