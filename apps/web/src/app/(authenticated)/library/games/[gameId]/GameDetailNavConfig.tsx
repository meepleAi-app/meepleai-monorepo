'use client';

/**
 * GameDetailNavConfig — NavConfig for game detail page
 *
 * MiniNav tabs: Overview · Agent · KB · Sessions
 * ActionBar actions: Chat Agent · Upload PDF · Favorite · Notes · Remove
 *
 * Renders null — purely a side-effect component that sets navigation config.
 */

import { useEffect } from 'react';

import {
  BookOpen,
  Bot,
  FileText,
  Gamepad2,
  Heart,
  MessageCircle,
  Pencil,
  Trash2,
  Upload,
} from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useToggleLibraryFavorite } from '@/hooks/queries/useLibrary';
import { useSetNavConfig } from '@/hooks/useSetNavConfig';

export interface GameDetailNavConfigProps {
  gameId: string;
  isFavorite?: boolean;
}

export function GameDetailNavConfig({ gameId, isFavorite = false }: GameDetailNavConfigProps) {
  const setNavConfig = useSetNavConfig();
  const router = useRouter();
  const toggleFavorite = useToggleLibraryFavorite();

  useEffect(() => {
    setNavConfig({
      miniNav: [
        {
          id: 'overview',
          label: 'Overview',
          href: `/library/games/${gameId}`,
          icon: BookOpen,
        },
        {
          id: 'agent',
          label: 'Agent',
          href: `/library/games/${gameId}?tab=agent`,
          icon: Bot,
        },
        {
          id: 'kb',
          label: 'KB',
          href: `/library/games/${gameId}?tab=kb`,
          icon: FileText,
        },
        {
          id: 'sessions',
          label: 'Sessions',
          href: `/library/games/${gameId}?tab=sessions`,
          icon: Gamepad2,
        },
      ],
      actionBar: [
        {
          id: 'chat-agent',
          label: 'Chat Agent',
          icon: MessageCircle,
          variant: 'primary',
          onClick: () => router.push(`/chat?gameId=${gameId}`),
        },
        {
          id: 'upload-pdf',
          label: 'Carica PDF',
          icon: Upload,
          variant: 'ghost',
          onClick: () => {
            document.dispatchEvent(new CustomEvent('game-detail:upload-pdf'));
          },
        },
        {
          id: 'toggle-favorite',
          label: isFavorite ? 'Rimuovi Preferito' : 'Aggiungi Preferito',
          icon: Heart,
          variant: 'ghost',
          onClick: () => {
            toggleFavorite.mutate({ gameId, isFavorite: !isFavorite });
          },
        },
        {
          id: 'edit-notes',
          label: 'Note',
          icon: Pencil,
          variant: 'ghost',
          onClick: () => {
            document.dispatchEvent(new CustomEvent('game-detail:edit-notes'));
          },
        },
        {
          id: 'remove-game',
          label: 'Rimuovi',
          icon: Trash2,
          variant: 'destructive',
          onClick: () => {
            document.dispatchEvent(new CustomEvent('game-detail:remove-game'));
          },
        },
      ],
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [setNavConfig, router, gameId, isFavorite]);

  return null;
}
