/**
 * UserGameCard Component (Issue #2518, #2613, #2618, #2866, #2867)
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
 * - Grid/List view modes (Issue #2866, #2867)
 */

'use client';

import { useState } from 'react';

import { motion } from 'framer-motion';
import {
  MessageCircle,
  Settings,
  Upload,
  Edit2,
  Trash2,
  Library,
  MoreVertical,
  RefreshCw,
  Zap,
  Bot,
  Dice6,
} from 'lucide-react';
import Image from 'next/image';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { FavoriteToggle } from '@/components/library/FavoriteToggle';
import { GameActionsModal } from '@/components/library/GameActionsModal';
import type { ViewMode } from '@/components/library/ViewModeToggle';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader } from '@/components/ui/data-display/card';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { useAgentConfig } from '@/hooks/queries';
import { useCanShareGame } from '@/hooks/queries/useCanShareGame';
import type { UserLibraryEntry } from '@/lib/api';
import type { GameStateType } from '@/lib/api/schemas/library.schemas';
import { cn } from '@/lib/utils';

interface UserGameCardProps {
  game: UserLibraryEntry;
  onConfigureAgent: (gameId: string, gameTitle: string) => void;
  onUploadPdf: (gameId: string, gameTitle: string) => void;
  onEditNotes: (gameId: string, gameTitle: string, currentNotes?: string | null) => void;
  onRemove: (gameId: string, gameTitle: string) => void;
  /** Ask AI Agent about game (Issue #3185) */
  onAskAgent: (gameId: string) => void;
  /** Change game state callback (Issue #2867) */
  onChangeState?: (gameId: string, gameTitle: string, newState: GameStateType) => void;
  /** Share game with community (Issue #2743) */
  onShare?: (gameId: string, gameTitle: string) => void;
  /** Selection mode props (Issue #2613) */
  selectionMode?: boolean;
  isSelected?: boolean;
  onSelect?: (gameId: string, shiftKey: boolean) => void;
  /** Animation props for staggered entrance (Issue #2618) */
  index?: number;
  /** View mode: grid or list (Issue #2866, #2867) */
  viewMode?: ViewMode;
}

// State-coded border colors (Issue #2867)
const stateBorderColors: Record<GameStateType, string> = {
  Nuovo: 'border-l-4 border-l-green-500',
  InPrestito: 'border-l-4 border-l-red-500',
  Wishlist: 'border-l-4 border-l-yellow-500',
  Owned: 'border-l-4 border-l-blue-500',
};

// State labels for display
const stateLabels: Record<GameStateType, string> = {
  Nuovo: 'Nuovo',
  InPrestito: 'In Prestito',
  Wishlist: 'Wishlist',
  Owned: 'Posseduto',
};

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
  onAskAgent,
  onChangeState,
  onShare,
  selectionMode = false,
  isSelected = false,
  onSelect,
  index = 0,
  viewMode = 'grid',
}: UserGameCardProps) {
  const router = useRouter();

  // Modal state for GameActionsModal (Issue #3151)
  const [actionsModalOpen, setActionsModalOpen] = useState(false);

  // List view mode (Issue #2866, #2867)
  const isListView = viewMode === 'list';
  // Fetch agent configuration status
  const { data: agentConfig } = useAgentConfig(game.gameId, true);

  // Get state border color (Issue #2867)
  const stateBorderClass = game.currentState ? stateBorderColors[game.currentState] : '';

  // Check if game can be shared (Issue #2743)
  const { canShare: _canShare, reason: _shareBlockReason } = useCanShareGame(game.gameId);

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
    // Selection mode: toggle selection
    if (selectionMode && onSelect) {
      e.preventDefault();
      e.stopPropagation();
      onSelect(game.gameId, e.shiftKey);
      return;
    }

    // Normal mode: navigate to game detail page (Issue #3151, #3152)
    router.push(`/library/games/${game.gameId}`);
  };

  const handleCheckboxChange = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (onSelect) {
      onSelect(game.gameId, e.shiftKey);
    }
  };

  const handleActionsClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    e.preventDefault();
    setActionsModalOpen(true);
  };

  const handleToggleFavorite = () => {
    // Favorite toggle handled by FavoriteToggle component
    // This is a placeholder if we need additional logic
  };

  // List view rendering (Issue #2866, #2867)
  if (isListView) {
    return (
      <TooltipProvider>
      <motion.div
        variants={cardVariants}
        initial="hidden"
        animate="visible"
        exit="exit"
        custom={index}
        layout
        whileHover={{ x: 4, transition: { duration: 0.2 } }}
      >
        <Card
          className={cn(
            'flex flex-row hover:shadow-md hover:border-primary/50 transition-all',
            stateBorderClass,
            selectionMode && 'cursor-pointer',
            isSelected && 'ring-2 ring-primary bg-primary/5'
          )}
          data-testid="game-card"
          data-view-mode="list"
          data-game-state={game.currentState}
          onClick={handleCardClick}
        >
          {/* Cover Image - Compact for list view */}
          <div className="relative w-24 h-24 flex-shrink-0 bg-muted">
            {game.gameImageUrl ? (
              <Image
                src={game.gameImageUrl}
                alt={game.gameTitle}
                fill
                className="object-cover"
                sizes="96px"
              />
            ) : (
              <div className="flex items-center justify-center h-full text-muted-foreground">
                <Library className="h-8 w-8" />
              </div>
            )}

            {/* Selection Checkbox (Issue #2613) */}
            {selectionMode && (
              <div
                className="absolute top-1 left-1 z-10"
                onClick={handleCheckboxChange}
              >
                <Checkbox
                  checked={isSelected}
                  className="h-4 w-4 bg-background border-2 shadow-md"
                  aria-label={`Seleziona ${game.gameTitle}`}
                />
              </div>
            )}
          </div>

          {/* Content - Horizontal layout */}
          <CardContent className="flex-1 p-3 flex items-center justify-between gap-4">
            <div className="flex-1 min-w-0">
              {/* Title & Publisher */}
              <h3 className="font-semibold text-base truncate">{game.gameTitle}</h3>
              {game.gamePublisher && (
                <p className="text-sm text-muted-foreground truncate">{game.gamePublisher}</p>
              )}
              {/* Date */}
              <p className="text-xs text-muted-foreground mt-1">
                Aggiunto il {new Date(game.addedAt).toLocaleDateString('it-IT')}
              </p>
            </div>

            {/* Status Badges */}
            <div className="flex items-center gap-2 flex-shrink-0">
              {/* State Badge (Issue #2867) */}
              {game.currentState && (
                <Badge
                  variant="outline"
                  className={cn(
                    'text-xs',
                    game.currentState === 'Nuovo' && 'bg-green-50 text-green-700 border-green-300',
                    game.currentState === 'InPrestito' && 'bg-red-50 text-red-700 border-red-300',
                    game.currentState === 'Wishlist' && 'bg-yellow-50 text-yellow-700 border-yellow-300',
                    game.currentState === 'Owned' && 'bg-blue-50 text-blue-700 border-blue-300'
                  )}
                >
                  {stateLabels[game.currentState]}
                </Badge>
              )}
              {agentConfigured && (
                <Badge variant="secondary" className="text-xs">
                  {/* eslint-disable-next-line security/detect-object-injection -- agentModel is a controlled enum value */}
                  🤖 {modelDisplayName[agentModel]}
                </Badge>
              )}
              {game.isFavorite && (
                <Badge variant="outline" className="text-xs bg-purple-50 text-purple-700">
                  ❤️
                </Badge>
              )}
            </div>

            {/* Compact Actions */}
            <div className="flex items-center gap-1 flex-shrink-0">
              {!selectionMode && (
                <FavoriteToggle
                  gameId={game.gameId}
                  isFavorite={game.isFavorite}
                  gameTitle={game.gameTitle}
                  size="sm"
                />
              )}
              <Button asChild variant="default" size="sm">
                <Link href={`/chat?gameId=${game.gameId}`}>
                  <MessageCircle className="h-3 w-3" />
                </Link>
              </Button>
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    variant="secondary"
                    size="sm"
                    onClick={(e: React.MouseEvent) => {
                      e.stopPropagation();
                      onAskAgent(game.gameId);
                    }}
                    disabled={!game.hasPdfDocuments}
                  >
                    <Bot className="h-3 w-3" />
                  </Button>
                </TooltipTrigger>
                {!game.hasPdfDocuments && (
                  <TooltipContent>
                    <p>No rulebook available</p>
                  </TooltipContent>
                )}
              </Tooltip>

              {/* Quick Actions Dropdown (Issue #2867) */}
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button variant="ghost" size="sm">
                    <MoreVertical className="h-4 w-4" />
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <DropdownMenuItem asChild>
                    <Link href={`/library/games/${game.gameId}/toolkit`}>
                      <Dice6 className="mr-2 h-4 w-4" />
                      Toolkit Punteggi
                    </Link>
                  </DropdownMenuItem>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem onClick={() => onEditNotes(game.gameId, game.gameTitle, game.notes)}>
                    <Edit2 className="mr-2 h-4 w-4" />
                    Modifica Note
                  </DropdownMenuItem>
                  <DropdownMenuItem onClick={() => onConfigureAgent(game.gameId, game.gameTitle)}>
                    <Settings className="mr-2 h-4 w-4" />
                    Configura Agente
                  </DropdownMenuItem>
                  <DropdownMenuItem onClick={() => onUploadPdf(game.gameId, game.gameTitle)}>
                    <Upload className="mr-2 h-4 w-4" />
                    Carica PDF
                  </DropdownMenuItem>
                  {onChangeState && (
                    <>
                      <DropdownMenuSeparator />
                      <DropdownMenuItem
                        onClick={() => onChangeState(game.gameId, game.gameTitle, 'Nuovo')}
                        disabled={game.currentState === 'Nuovo'}
                      >
                        <RefreshCw className="mr-2 h-4 w-4 text-green-600" />
                        Segna come Nuovo
                      </DropdownMenuItem>
                      <DropdownMenuItem
                        onClick={() => onChangeState(game.gameId, game.gameTitle, 'InPrestito')}
                        disabled={game.currentState === 'InPrestito'}
                      >
                        <RefreshCw className="mr-2 h-4 w-4 text-red-600" />
                        Segna In Prestito
                      </DropdownMenuItem>
                      <DropdownMenuItem
                        onClick={() => onChangeState(game.gameId, game.gameTitle, 'Owned')}
                        disabled={game.currentState === 'Owned'}
                      >
                        <RefreshCw className="mr-2 h-4 w-4 text-blue-600" />
                        Segna come Posseduto
                      </DropdownMenuItem>
                    </>
                  )}
                  <DropdownMenuSeparator />
                  <DropdownMenuItem
                    onClick={() => onRemove(game.gameId, game.gameTitle)}
                    className="text-destructive focus:text-destructive"
                  >
                    <Trash2 className="mr-2 h-4 w-4" />
                    Rimuovi
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </div>
          </CardContent>
        </Card>
      </motion.div>
    </TooltipProvider>
    );
  }

  // Grid view rendering (default)
  return (
    <TooltipProvider>
    <motion.div
      variants={cardVariants}
      initial="hidden"
      animate="visible"
      exit="exit"
      custom={index}
      layout
      whileHover={{ y: -4, scale: 1.02, transition: { duration: 0.2 } }}
    >
      <Card
        className={cn(
          'flex flex-col hover:shadow-lg hover:border-primary/50 transition-all cursor-pointer',
          stateBorderClass,
          isSelected && 'ring-2 ring-primary bg-primary/5'
        )}
        data-testid="game-card"
        data-view-mode="grid"
        data-game-state={game.currentState}
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

          {/* State Badge Overlay (Issue #2867) */}
          {game.currentState && (
            <div className="absolute top-2 left-2">
              <Badge
                variant="secondary"
                className={cn(
                  'text-xs shadow-md',
                  game.currentState === 'Nuovo' && 'bg-green-500 text-white',
                  game.currentState === 'InPrestito' && 'bg-red-500 text-white',
                  game.currentState === 'Wishlist' && 'bg-yellow-500 text-black',
                  game.currentState === 'Owned' && 'bg-blue-500 text-white'
                )}
              >
                {stateLabels[game.currentState]}
              </Badge>
            </div>
          )}

          {/* Selection Checkbox (Issue #2613) */}
          {selectionMode && (
            <div
              className={cn('absolute top-2 z-10', game.currentState ? 'left-20' : 'left-2')}
              onClick={handleCheckboxChange}
            >
              <Checkbox
                checked={isSelected}
                className="h-5 w-5 bg-background border-2 shadow-md"
                aria-label={`Seleziona ${game.gameTitle}`}
              />
            </div>
          )}

          {/* Agent Configuration Indicator (Issue #3185) */}
          {agentConfigured && !selectionMode && (
            <div className="absolute bottom-2 left-2 z-10">
              <div className="bg-secondary text-secondary-foreground rounded-full p-1.5 shadow-md">
                <Settings className="h-3 w-3" />
              </div>
            </div>
          )}

          {/* Favorite Toggle - hide in selection mode (Issue #3151) */}
          {!selectionMode && (
            <div className="absolute top-2 right-2" onClick={(e) => e.stopPropagation()}>
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

        {/* Action Buttons (Issue #3151, #3185, #3164) */}
        {!selectionMode && (
          <div className="flex gap-2 pt-2">
            <Button
              asChild
              variant="default"
              size="sm"
              className="flex-1"
              onClick={(e: React.MouseEvent) => e.stopPropagation()}
            >
              <Link href={`/library/games/${game.gameId}/toolkit`}>
                <Dice6 className="mr-1 h-3 w-3" />
                Toolkit
              </Link>
            </Button>
            <Tooltip>
              <TooltipTrigger asChild>
                <Button
                  variant="secondary"
                  size="sm"
                  className="flex-1"
                  onClick={(e: React.MouseEvent) => {
                    e.stopPropagation();
                    onAskAgent(game.gameId);
                  }}
                  disabled={!game.hasPdfDocuments}
                >
                  <Bot className="mr-1 h-3 w-3" />
                  Ask Agent
                </Button>
              </TooltipTrigger>
              {!game.hasPdfDocuments && (
                <TooltipContent>
                  <p>No rulebook available</p>
                </TooltipContent>
              )}
            </Tooltip>
            <Button
              variant="outline"
              size="sm"
              className="flex-1"
              onClick={handleActionsClick}
            >
              <Zap className="mr-1 h-3 w-3" />
              Azioni
            </Button>
          </div>
        )}

        {/* Added Date */}
        <p className="text-xs text-muted-foreground pt-2 border-t">
          Aggiunto il {new Date(game.addedAt).toLocaleDateString('it-IT')}
        </p>
      </CardContent>
      </Card>

      {/* Game Actions Modal (Issue #3151) */}
      <GameActionsModal
        isOpen={actionsModalOpen}
        onClose={() => setActionsModalOpen(false)}
        gameId={game.gameId}
        gameTitle={game.gameTitle}
        gameImageUrl={game.gameImageUrl}
        hasAgent={agentConfigured}
        isFavorite={game.isFavorite}
        currentState={game.currentState}
        onConfigureAgent={() => onConfigureAgent(game.gameId, game.gameTitle)}
        onUploadPdf={() => onUploadPdf(game.gameId, game.gameTitle)}
        onEditNotes={() => onEditNotes(game.gameId, game.gameTitle, game.notes)}
        onRemove={() => onRemove(game.gameId, game.gameTitle)}
        onChangeState={(newState) => onChangeState?.(game.gameId, game.gameTitle, newState)}
        onToggleFavorite={handleToggleFavorite}
        onShare={onShare ? () => onShare(game.gameId, game.gameTitle) : undefined}
        onManageSession={() => {
          // TODO: Implement session management
          console.log('Manage session for', game.gameId);
        }}
      />
    </motion.div>
    </TooltipProvider>
  );
}
