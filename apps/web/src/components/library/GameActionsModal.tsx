/**
 * Game Actions Modal Component (Issue #3151)
 *
 * Modal with all 10 game actions in priority order:
 * 1. Chatta (with agent mode + PDF selection)
 * 2. Usa agente (quick start with configured agent)
 * 3. Config agente
 * 4. Note
 * 5. Aggiungi PDF
 * 6. Rimuovi
 * 7. Cambia Stato
 * 8. Favorito
 * 9. Share Community
 * 10. Gestione partita
 */

'use client';

import { useState } from 'react';
import {
  MessageCircle,
  Play,
  Settings,
  Edit2,
  Upload,
  Trash2,
  RefreshCw,
  Heart,
  Share2,
  FileText,
  ChevronRight,
  X,
} from 'lucide-react';
import Link from 'next/link';

import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/feedback/dialog';
import { Button } from '@/components/ui/primitives/button';
import type { GameStateType } from '@/lib/api/schemas/library.schemas';
import { cn } from '@/lib/utils';

export interface GameActionsModalProps {
  isOpen: boolean;
  onClose: () => void;
  gameId: string;
  gameTitle: string;
  gameImageUrl?: string | null;
  /** Whether agent is configured */
  hasAgent: boolean;
  /** Current favorite status */
  isFavorite: boolean;
  /** Current game state */
  currentState?: GameStateType;
  /** Action handlers */
  onConfigureAgent: () => void;
  onUploadPdf: () => void;
  onEditNotes: () => void;
  onRemove: () => void;
  onChangeState: (newState: GameStateType) => void;
  onToggleFavorite: () => void;
  onShare?: () => void;
  onManageSession?: () => void;
}

export function GameActionsModal({
  isOpen,
  onClose,
  gameId,
  gameTitle,
  gameImageUrl,
  hasAgent,
  isFavorite,
  currentState,
  onConfigureAgent,
  onUploadPdf,
  onEditNotes,
  onRemove,
  onChangeState,
  onToggleFavorite,
  onShare,
  onManageSession,
}: GameActionsModalProps) {
  const [showStateSubmenu, setShowStateSubmenu] = useState(false);
  const [showSessionSubmenu, setShowSessionSubmenu] = useState(false);

  const handleClose = () => {
    setShowStateSubmenu(false);
    setShowSessionSubmenu(false);
    onClose();
  };

  const handleActionClick = (action: () => void) => {
    action();
    handleClose();
  };

  return (
    <Dialog open={isOpen} onOpenChange={handleClose}>
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <div className="flex items-start justify-between">
            <div className="flex items-center gap-3">
              {gameImageUrl && (
                <img
                  src={gameImageUrl}
                  alt={gameTitle}
                  className="w-12 h-12 rounded-lg object-cover shadow-sm"
                />
              )}
              <div>
                <DialogTitle className="font-quicksand text-2xl font-bold">
                  {gameTitle}
                </DialogTitle>
                <p className="text-sm text-muted-foreground mt-1">Azioni Rapide</p>
              </div>
            </div>
          </div>
        </DialogHeader>

        <div className="space-y-2 mt-4">
          {/* Primary Action: Chatta (navigate to game detail page) */}
          <Link href={`/library/games/${gameId}`} onClick={handleClose}>
            <button className="w-full flex items-center gap-4 p-4 rounded-xl bg-blue-50 border-2 border-blue-200 hover:bg-blue-100 hover:border-blue-300 transition-all group text-left">
              <div className="flex-shrink-0 w-10 h-10 rounded-lg bg-blue-600 flex items-center justify-center group-hover:scale-110 transition-transform">
                <MessageCircle className="w-5 h-5 text-white" />
              </div>
              <div className="flex-1">
                <div className="font-semibold text-blue-900 mb-0.5">Chatta</div>
                <div className="text-sm text-blue-700">
                  Seleziona modalità (tutor, Q&A, etc.) e PDF da utilizzare
                </div>
              </div>
              <ChevronRight className="w-5 h-5 text-blue-600 group-hover:translate-x-1 transition-transform" />
            </button>
          </Link>

          {/* Use Agent (if configured) */}
          {hasAgent && (
            <Link href={`/library/games/${gameId}?autoStart=true`} onClick={handleClose}>
              <button className="w-full flex items-center gap-4 p-3.5 rounded-lg border border-gray-200 hover:bg-gray-50 hover:border-gray-300 transition-all group text-left">
                <div className="flex-shrink-0 w-9 h-9 rounded-lg bg-green-100 flex items-center justify-center group-hover:scale-110 transition-transform">
                  <Play className="w-4 h-4 text-green-700" />
                </div>
                <div className="flex-1">
                  <div className="font-medium">Usa agente</div>
                  <div className="text-xs text-muted-foreground">
                    Avvia chat con agente configurato
                  </div>
                </div>
              </button>
            </Link>
          )}

          {/* Configure Agent */}
          <button
            onClick={() => handleActionClick(onConfigureAgent)}
            className="w-full flex items-center gap-4 p-3.5 rounded-lg border border-gray-200 hover:bg-gray-50 hover:border-gray-300 transition-all group text-left"
          >
            <div className="flex-shrink-0 w-9 h-9 rounded-lg bg-purple-100 flex items-center justify-center group-hover:scale-110 transition-transform">
              <Settings className="w-4 h-4 text-purple-700" />
            </div>
            <div className="flex-1">
              <div className="font-medium">Config agente</div>
              <div className="text-xs text-muted-foreground">
                Modifica impostazioni e modalità agente
              </div>
            </div>
          </button>

          {/* Edit Notes */}
          <button
            onClick={() => handleActionClick(onEditNotes)}
            className="w-full flex items-center gap-4 p-3.5 rounded-lg border border-gray-200 hover:bg-gray-50 hover:border-gray-300 transition-all group text-left"
          >
            <div className="flex-shrink-0 w-9 h-9 rounded-lg bg-amber-100 flex items-center justify-center group-hover:scale-110 transition-transform">
              <Edit2 className="w-4 h-4 text-amber-700" />
            </div>
            <div className="flex-1">
              <div className="font-medium">Note</div>
              <div className="text-xs text-muted-foreground">Modifica note personali sul gioco</div>
            </div>
          </button>

          {/* Upload PDF */}
          <button
            onClick={() => handleActionClick(onUploadPdf)}
            className="w-full flex items-center gap-4 p-3.5 rounded-lg border border-gray-200 hover:bg-gray-50 hover:border-gray-300 transition-all group text-left"
          >
            <div className="flex-shrink-0 w-9 h-9 rounded-lg bg-blue-100 flex items-center justify-center group-hover:scale-110 transition-transform">
              <Upload className="w-4 h-4 text-blue-700" />
            </div>
            <div className="flex-1">
              <div className="font-medium">Aggiungi PDF</div>
              <div className="text-xs text-muted-foreground">Carica regolamento o espansioni</div>
            </div>
          </button>

          <div className="h-px bg-gray-200 my-3"></div>

          {/* Change State - Submenu Toggle */}
          <button
            onClick={() => setShowStateSubmenu(!showStateSubmenu)}
            className="w-full flex items-center gap-4 p-3.5 rounded-lg border border-gray-200 hover:bg-gray-50 hover:border-gray-300 transition-all group text-left"
          >
            <div className="flex-shrink-0 w-9 h-9 rounded-lg bg-gray-100 flex items-center justify-center group-hover:scale-110 transition-transform">
              <RefreshCw className="w-4 h-4 text-gray-700" />
            </div>
            <div className="flex-1">
              <div className="font-medium">Cambia Stato</div>
              <div className="text-xs text-muted-foreground">
                Nuovo, In Prestito, Posseduto, Wishlist
              </div>
            </div>
            <ChevronRight
              className={cn(
                'w-4 h-4 text-gray-400 transition-transform',
                showStateSubmenu && 'rotate-90'
              )}
            />
          </button>

          {/* State Submenu */}
          {showStateSubmenu && (
            <div className="ml-6 space-y-1 pl-6 border-l-2 border-gray-200">
              <button
                onClick={() => handleActionClick(() => onChangeState('Nuovo'))}
                disabled={currentState === 'Nuovo'}
                className="w-full flex items-center gap-3 p-2.5 rounded-lg hover:bg-gray-50 transition-colors text-left disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <div className="w-2 h-2 rounded-full bg-green-500"></div>
                <span className="text-sm font-medium">Segna come Nuovo</span>
              </button>
              <button
                onClick={() => handleActionClick(() => onChangeState('InPrestito'))}
                disabled={currentState === 'InPrestito'}
                className="w-full flex items-center gap-3 p-2.5 rounded-lg hover:bg-gray-50 transition-colors text-left disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <div className="w-2 h-2 rounded-full bg-red-500"></div>
                <span className="text-sm font-medium">Segna In Prestito</span>
              </button>
              <button
                onClick={() => handleActionClick(() => onChangeState('Owned'))}
                disabled={currentState === 'Owned'}
                className="w-full flex items-center gap-3 p-2.5 rounded-lg hover:bg-gray-50 transition-colors text-left disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <div className="w-2 h-2 rounded-full bg-blue-500"></div>
                <span className="text-sm font-medium">Segna come Posseduto</span>
              </button>
              <button
                onClick={() => handleActionClick(() => onChangeState('Wishlist'))}
                disabled={currentState === 'Wishlist'}
                className="w-full flex items-center gap-3 p-2.5 rounded-lg hover:bg-gray-50 transition-colors text-left disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <div className="w-2 h-2 rounded-full bg-yellow-500"></div>
                <span className="text-sm font-medium">Aggiungi a Wishlist</span>
              </button>
            </div>
          )}

          {/* Toggle Favorite */}
          <button
            onClick={() => handleActionClick(onToggleFavorite)}
            className="w-full flex items-center gap-4 p-3.5 rounded-lg border border-gray-200 hover:bg-gray-50 hover:border-gray-300 transition-all group text-left"
          >
            <div className="flex-shrink-0 w-9 h-9 rounded-lg bg-rose-100 flex items-center justify-center group-hover:scale-110 transition-transform">
              <Heart className={cn('w-4 h-4 text-rose-700', isFavorite && 'fill-rose-700')} />
            </div>
            <div className="flex-1">
              <div className="font-medium">
                {isFavorite ? 'Rimuovi dai' : 'Aggiungi ai'} preferiti
              </div>
              <div className="text-xs text-muted-foreground">
                {isFavorite ? 'Rimuovi' : 'Aggiungi'} questo gioco dai/ai tuoi preferiti
              </div>
            </div>
          </button>

          {/* Share Community */}
          {onShare && (
            <button
              onClick={() => handleActionClick(onShare)}
              className="w-full flex items-center gap-4 p-3.5 rounded-lg border border-gray-200 hover:bg-gray-50 hover:border-gray-300 transition-all group text-left"
            >
              <div className="flex-shrink-0 w-9 h-9 rounded-lg bg-indigo-100 flex items-center justify-center group-hover:scale-110 transition-transform">
                <Share2 className="w-4 h-4 text-indigo-700" />
              </div>
              <div className="flex-1">
                <div className="font-medium">Share Community</div>
                <div className="text-xs text-muted-foreground">Condividi con la community</div>
              </div>
            </button>
          )}

          {/* Manage Game Session - Submenu Toggle */}
          {onManageSession && (
            <>
              <button
                onClick={() => setShowSessionSubmenu(!showSessionSubmenu)}
                className="w-full flex items-center gap-4 p-3.5 rounded-lg border border-gray-200 hover:bg-gray-50 hover:border-gray-300 transition-all group text-left"
              >
                <div className="flex-shrink-0 w-9 h-9 rounded-lg bg-teal-100 flex items-center justify-center group-hover:scale-110 transition-transform">
                  <FileText className="w-4 h-4 text-teal-700" />
                </div>
                <div className="flex-1">
                  <div className="font-medium">Gestione partita</div>
                  <div className="text-xs text-muted-foreground">
                    Crea, carica, elimina sessioni di gioco
                  </div>
                </div>
                <ChevronRight
                  className={cn(
                    'w-4 h-4 text-gray-400 transition-transform',
                    showSessionSubmenu && 'rotate-90'
                  )}
                />
              </button>

              {/* Session Submenu */}
              {showSessionSubmenu && (
                <div className="ml-6 space-y-1 pl-6 border-l-2 border-gray-200">
                  <button
                    onClick={() => handleActionClick(onManageSession)}
                    className="w-full flex items-center gap-3 p-2.5 rounded-lg hover:bg-gray-50 transition-colors text-left"
                  >
                    <span className="text-sm font-medium">Crea nuova partita</span>
                  </button>
                  <button
                    onClick={() => handleActionClick(onManageSession)}
                    className="w-full flex items-center gap-3 p-2.5 rounded-lg hover:bg-gray-50 transition-colors text-left"
                  >
                    <span className="text-sm font-medium">Carica partita salvata</span>
                  </button>
                  <button
                    onClick={() => handleActionClick(onManageSession)}
                    className="w-full flex items-center gap-3 p-2.5 rounded-lg hover:bg-gray-50 transition-colors text-left"
                  >
                    <span className="text-sm font-medium">Elimina partite</span>
                  </button>
                </div>
              )}
            </>
          )}

          <div className="h-px bg-gray-200 my-3"></div>

          {/* Destructive Action: Remove */}
          <button
            onClick={() => handleActionClick(onRemove)}
            className="w-full flex items-center gap-4 p-3.5 rounded-lg border border-red-200 bg-red-50 hover:bg-red-100 hover:border-red-300 transition-all group text-left"
          >
            <div className="flex-shrink-0 w-9 h-9 rounded-lg bg-red-600 flex items-center justify-center group-hover:scale-110 transition-transform">
              <Trash2 className="w-4 h-4 text-white" />
            </div>
            <div className="flex-1">
              <div className="font-medium text-red-900">Rimuovi dalla libreria</div>
              <div className="text-xs text-red-700">Azione irreversibile</div>
            </div>
          </button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
