/**
 * useEntityActions - Entity-specific action factory
 *
 * Generates quick actions and menu actions for each MeepleCard entity type.
 * Handles navigation, permissions, and entity-specific operations.
 *
 * @module hooks/use-entity-actions
 * @see Issue #4031 - Entity-Specific Quick Actions
 */

import { useMemo } from 'react';

import {
  BarChart3,
  CheckCircle,
  Download,
  FileDown,
  Mail,
  MessageSquare,
  Play,
  PlayCircle,
  Share2,
  UserPlus,
  Wrench,
} from 'lucide-react';
import { useRouter } from 'next/navigation';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';
import type { QuickAction } from '@/components/ui/data-display/meeple-card-quick-actions';

// ============================================================================
// Types
// ============================================================================

export interface UseEntityActionsProps {
  /** Entity type */
  entity: MeepleEntityType;
  /** Entity ID */
  id: string;
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
// Hook
// ============================================================================

export function useEntityActions({
  entity,
  id,
  userId,
  userRole = 'user',
  data,
}: UseEntityActionsProps): EntityActions {
  const router = useRouter();

  // Reserved for future use in moreActions
  const _isAdmin = userRole === 'admin' || userRole === 'superadmin';
  const _isOwner = userId && data?.ownerId && userId === data.ownerId;

  return useMemo(() => {
    switch (entity) {
      case 'game':
        return {
          quickActions: [
            {
              icon: MessageSquare,
              label: 'Chat con Agent',
              onClick: () => router.push(`/chat?gameId=${id}`),
            },
            {
              icon: Play,
              label: 'Avvia Sessione',
              onClick: () => router.push(`/sessions/new?gameId=${id}`),
            },
            {
              icon: Share2,
              label: 'Condividi',
              onClick: () => {
                // TODO: Share modal or copy link
                navigator.clipboard?.writeText(`${window.location.origin}/games/${id}`);
              },
            },
          ],
          // More menu actions can be added later
        };

      case 'session':
        return {
          quickActions: [
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
                // TODO: Copy session code to clipboard
                const sessionCode = (data?.sessionCode as string | undefined) || id;
                navigator.clipboard?.writeText(sessionCode);
              },
            },
          ],
        };

      case 'agent':
        return {
          quickActions: [
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

      case 'document':
        return {
          quickActions: [
            {
              icon: Download,
              label: 'Download',
              onClick: () => {
                // TODO: Trigger download
                window.open(`/api/v1/documents/${id}/download`, '_blank');
              },
            },
            {
              icon: MessageSquare,
              label: 'Chat sui contenuti',
              onClick: () => router.push(`/chat?documentId=${id}`),
            },
          ],
        };

      case 'chatSession':
        return {
          quickActions: [
            {
              icon: MessageSquare,
              label: 'Continua Chat',
              onClick: () => router.push(`/chat/${id}`),
            },
            {
              icon: FileDown,
              label: 'Esporta',
              onClick: () => {
                // TODO: Export chat history
                router.push(`/chat/${id}/export`);
              },
            },
          ],
        };

      case 'player':
        return {
          quickActions: [
            {
              icon: Mail,
              label: 'Messaggia',
              onClick: () => router.push(`/messages/new?recipientId=${id}`),
            },
            {
              icon: UserPlus,
              label: 'Invita a Sessione',
              onClick: () => {
                // TODO: Open session invite modal
                router.push(`/sessions/new?invitePlayer=${id}`);
              },
            },
          ],
        };

      case 'event':
        return {
          quickActions: [
            {
              icon: CheckCircle,
              label: 'Partecipa',
              onClick: () => {
                // TODO: RSVP to event
                router.push(`/events/${id}/rsvp`);
              },
            },
            {
              icon: Share2,
              label: 'Condividi',
              onClick: () => {
                navigator.clipboard?.writeText(`${window.location.origin}/events/${id}`);
              },
            },
          ],
        };

      case 'custom':
      default:
        return {
          quickActions: [],
        };
    }
  }, [entity, id, router, data]);
}
