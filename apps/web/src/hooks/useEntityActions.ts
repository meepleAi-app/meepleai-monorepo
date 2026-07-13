/**
 * useEntityActions - Entity-specific action factory
 *
 * Generates quick actions and menu actions for each MeepleCard entity type.
 * Handles navigation, permissions, and entity-specific operations.
 *
 * @module hooks/use-entity-actions
 * @see Issue #4031 - Entity-Specific Quick Actions
 * @see Issue #4259 - Collection Quick Actions for MeepleCard
 */

import { useMemo } from 'react';

import {
  BarChart3,
  Bookmark,
  Bot,
  CheckCircle,
  Download,
  FileDown,
  Heart,
  HeartHandshake,
  Mail,
  MessageSquare,
  Pin,
  Play,
  PlayCircle,
  Plus,
  Share2,
  Star,
  Trash2,
  UserPlus,
  Wrench,
} from 'lucide-react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';
import { api } from '@/lib/api';
import type { EntityType } from '@/lib/api/schemas/collections.schemas';
import type { QuickAction } from '@/types/quick-action';

import {
  useAddToCollection,
  useCollectionStatus,
  useRemoveFromCollection,
} from './queries/useCollections';
import { useCollectionActions, type AssociatedData } from './useCollectionActions';

// ============================================================================
// Types
// ============================================================================

export interface UseEntityActionsProps {
  /** Entity type */
  entity: MeepleEntityType;
  /** Entity ID */
  id: string;
  /** Entity name (for warning modal) */
  entityName?: string;
  /** Current user ID (for ownership checks) */
  userId?: string;
  /** User role (for role-gated actions) */
  userRole?: 'user' | 'editor' | 'admin' | 'superadmin';
  /** Entity-specific data (optional) */
  data?: {
    ownerId?: string;
    isShared?: boolean;
    status?: string;
    [key: string]: unknown;
  };
  /** Callback to show removal warning modal (Issue #4259) */
  onShowRemovalWarning?: (data: AssociatedData, onConfirm: () => void) => void;
  /** Callback to open agent creation wizard (Issue #4777) */
  onCreateAgent?: () => void;
  /** Callback to open collection wizard instead of direct add (Issue #4822) */
  onAddToCollection?: () => void;
}

export interface EntityActions {
  /** Quick actions (hover-reveal buttons) */
  quickActions: QuickAction[];
  /** More menu actions (secondary dropdown) */
  moreActions?: Array<{
    label: string;
    onClick: () => void;
    icon?: React.ComponentType;
    adminOnly?: boolean;
    destructive?: boolean;
    separator?: boolean;
  }>;
}

// ============================================================================
// Handler helpers
// ============================================================================

/**
 * Copy text to the clipboard with success/error toast feedback.
 * Guards against restricted contexts where the Clipboard API is unavailable.
 */
async function copyWithToast(text: string, successMessage: string): Promise<void> {
  try {
    if (typeof navigator === 'undefined' || !navigator.clipboard) {
      throw new Error('Clipboard API unavailable');
    }
    await navigator.clipboard.writeText(text);
    toast.success(successMessage);
  } catch {
    toast.error('Copia non riuscita');
  }
}

/**
 * Export a chat thread (default PDF) via the existing export pipeline, with toast feedback.
 */
async function exportChatWithToast(chatId: string): Promise<void> {
  try {
    await api.chat.exportChat(chatId, { format: 'pdf' });
    toast.success('Chat esportata');
  } catch {
    toast.error('Esportazione non riuscita');
  }
}

// ============================================================================
// Hook
// ============================================================================

export function useEntityActions({
  entity,
  id,
  entityName,
  userId,
  userRole = 'user',
  data,
  onShowRemovalWarning,
  onCreateAgent,
  onAddToCollection,
}: UseEntityActionsProps): EntityActions {
  const router = useRouter();

  // Reserved for future use in moreActions
  const _isAdmin = userRole === 'admin' || userRole === 'superadmin';
  const _isOwner = userId && data?.ownerId && userId === data.ownerId;

  // Issue #4259: Collection actions for game entity (Phase 1)
  // Call hook unconditionally (hooks rules), but only use for entity='game'
  // Pass userId so the hook skips the API call for unauthenticated users
  const gameCollection = useCollectionActions(
    entity === 'game' ? id : '',
    onShowRemovalWarning,
    userId
  );

  // Issue #4263: Generic collection actions for other entities (Phase 2)
  // Map MeepleEntityType to backend EntityType
  const genericEntityType: EntityType | null =
    entity === 'game'
      ? null // Use Phase 1 hooks for games
      : (entity as EntityType);

  // Generic collection status (disabled for games)
  const { data: genericStatus } = useCollectionStatus(
    genericEntityType || 'player', // Fallback to prevent type error
    id,
    { enabled: genericEntityType !== null }
  );

  // Generic collection mutations (disabled for games)
  const addToGenericCollection = useAddToCollection(genericEntityType || 'player', id);
  const removeFromGenericCollection = useRemoveFromCollection(
    genericEntityType || 'player',
    id,
    onShowRemovalWarning
  );

  return useMemo(() => {
    switch (entity) {
      case 'game': {
        const isAuthenticated = !!userId;

        // Build collection action — redirect to login if unauthenticated
        const collectionAction: QuickAction = !isAuthenticated
          ? {
              icon: Plus,
              label: 'Aggiungi a Collezione',
              onClick: () => router.push('/login?reason=collection'),
            }
          : gameCollection.isInCollection
            ? {
                icon: Trash2,
                label: 'Rimuovi da Collezione',
                onClick: () => gameCollection.remove(),
              }
            : {
                icon: Plus,
                label: 'Aggiungi a Collezione',
                onClick: () => (onAddToCollection ? onAddToCollection() : gameCollection.add()),
              };

        const hasRag = data?.hasKb === true;
        const hasAgent = data?.hasAgent === true;

        return {
          quickActions: [
            collectionAction, // Issue #4259: First action
            {
              icon: Bot,
              label: 'Crea Agente',
              onClick: () => onCreateAgent?.(),
              hidden: hasAgent || !onCreateAgent,
            },
            {
              icon: MessageSquare,
              label: 'Chat con Agent',
              onClick: () => router.push(`/chat/new?game=${id}`),
              hidden: !hasRag,
            },
            {
              icon: Play,
              label: 'Avvia Sessione',
              onClick: () =>
                isAuthenticated
                  ? router.push(
                      `/sessions/new?gameId=${encodeURIComponent(id)}${entityName ? `&gameName=${encodeURIComponent(entityName)}` : ''}`
                    )
                  : router.push('/login?reason=session'),
            },
            {
              icon: Share2,
              label: 'Condividi',
              onClick: () => {
                void copyWithToast(
                  `${window.location.origin}/games/${id}`,
                  'Link copiato negli appunti'
                );
              },
            },
          ],
          // More menu actions can be added later
        };
      }

      case 'session': {
        // Issue #4263: Collection action for session (Save/Remove)
        const saveAction: QuickAction =
          (genericStatus?.inCollection ?? false)
            ? {
                icon: Bookmark,
                label: 'Rimuovi dai Salvati',
                onClick: () => removeFromGenericCollection.remove(undefined),
              }
            : {
                icon: Bookmark,
                label: 'Salva',
                onClick: () => addToGenericCollection.mutate(undefined),
              };

        return {
          quickActions: [
            saveAction, // Issue #4263: First action
            {
              icon: PlayCircle,
              label: 'Riprendi',
              onClick: () => router.push(`/sessions/${id}`),
            },
            {
              icon: Wrench,
              label: 'Usa Toolkit',
              onClick: () => router.push(`/sessions/${id}?tab=toolkit`),
            },
            {
              icon: Share2,
              label: 'Condividi codice',
              onClick: () => {
                const sessionCode = (data?.sessionCode as string | undefined) || id;
                void copyWithToast(sessionCode, 'Codice sessione copiato');
              },
            },
          ],
        };
      }

      case 'agent': {
        // Issue #4263: Collection action for agent (Favorite/Unfavorite)
        const favoriteAction: QuickAction =
          (genericStatus?.inCollection ?? false)
            ? {
                icon: Heart,
                label: 'Rimuovi dai Preferiti',
                onClick: () => removeFromGenericCollection.remove(undefined),
              }
            : {
                icon: Heart,
                label: 'Aggiungi ai Preferiti',
                onClick: () => addToGenericCollection.mutate(undefined),
              };

        return {
          quickActions: [
            favoriteAction, // Issue #4263: First action
            {
              icon: MessageSquare,
              label: 'Chat',
              onClick: () => router.push(`/chat?agentId=${id}`),
            },
            {
              icon: BarChart3,
              label: 'Statistiche',
              onClick: () => router.push(`/agents/${id}?tab=stats`),
            },
          ],
        };
      }

      case 'kb': {
        // Issue #4263: Collection action for document (Save/Remove)
        const saveAction: QuickAction =
          (genericStatus?.inCollection ?? false)
            ? {
                icon: Bookmark,
                label: 'Rimuovi dai Salvati',
                onClick: () => removeFromGenericCollection.remove(undefined),
              }
            : {
                icon: Bookmark,
                label: 'Salva',
                onClick: () => addToGenericCollection.mutate(undefined),
              };

        return {
          quickActions: [
            saveAction, // Issue #4263: First action
            {
              icon: Download,
              label: 'Download',
              onClick: () => {
                // Browser-native download via the canonical pdf endpoint (session-cookie auth).
                window.open(api.pdf.getPdfDownloadUrl(id), '_blank');
              },
            },
            {
              icon: MessageSquare,
              label: 'Chat sui contenuti',
              onClick: () => router.push(`/chat?documentId=${id}`),
            },
          ],
        };
      }

      case 'chat': {
        // Issue #4263: Collection action for chatSession (Pin/Unpin)
        const pinAction: QuickAction =
          (genericStatus?.inCollection ?? false)
            ? {
                icon: Pin,
                label: 'Rimuovi Pin',
                onClick: () => removeFromGenericCollection.remove(undefined),
              }
            : {
                icon: Pin,
                label: 'Fissa',
                onClick: () => addToGenericCollection.mutate(undefined),
              };

        return {
          quickActions: [
            pinAction, // Issue #4263: First action
            {
              icon: MessageSquare,
              label: 'Continua Chat',
              onClick: () => router.push(`/chat/${id}`),
            },
            {
              icon: FileDown,
              label: 'Esporta',
              onClick: () => {
                void exportChatWithToast(id);
              },
            },
          ],
        };
      }

      case 'player': {
        // Issue #4263: Collection action for player (Follow/Unfollow)
        const followAction: QuickAction =
          (genericStatus?.inCollection ?? false)
            ? {
                icon: HeartHandshake,
                label: 'Smetti di Seguire',
                onClick: () => removeFromGenericCollection.remove(undefined),
              }
            : {
                icon: HeartHandshake,
                label: 'Segui',
                onClick: () => addToGenericCollection.mutate(undefined),
              };

        return {
          quickActions: [
            followAction, // Issue #4263: First action
            {
              icon: Mail,
              label: 'Messaggia',
              onClick: () => router.push(`/messages/new?recipientId=${id}`),
            },
            {
              icon: UserPlus,
              label: 'Invita a Sessione',
              onClick: () => {
                // Navigates to the session wizard. The invitePlayer param is reserved for a
                // future roster pre-fill (the wizard does not consume it yet — see #2776).
                router.push(`/sessions/new?invitePlayer=${id}`);
              },
            },
          ],
        };
      }

      case 'event': {
        // Issue #4263: Collection action for event (Interested/Remove)
        const interestedAction: QuickAction =
          (genericStatus?.inCollection ?? false)
            ? {
                icon: Star,
                label: 'Rimuovi Interesse',
                onClick: () => removeFromGenericCollection.remove(undefined),
              }
            : {
                icon: Star,
                label: 'Interessato',
                onClick: () => addToGenericCollection.mutate(undefined),
              };

        return {
          quickActions: [
            interestedAction, // Issue #4263: First action
            {
              icon: CheckCircle,
              label: 'Partecipa',
              onClick: () => {
                // RSVP lives on the game-night detail page (event id === game-night id).
                router.push(`/game-nights/${id}`);
              },
            },
            {
              icon: Share2,
              label: 'Condividi',
              onClick: () => {
                void copyWithToast(
                  `${window.location.origin}/game-nights/${id}`,
                  'Link copiato negli appunti'
                );
              },
            },
          ],
        };
      }

      default:
        return {
          quickActions: [],
        };
    }
  }, [
    entity,
    id,
    router,
    data,
    userId,
    gameCollection,
    genericStatus,
    addToGenericCollection,
    removeFromGenericCollection,
    onCreateAgent,
    onAddToCollection,
    entityName,
  ]);
}
