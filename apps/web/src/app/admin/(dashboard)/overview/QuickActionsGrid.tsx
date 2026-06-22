/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or admin-decorative inline gradient; DS-13a admin scope, mockup .e-bg pattern. Future: extract --admin-* token family (deferred to DS-15 audit). */
'use client';

import { useState } from 'react';

import { Gamepad2, ListOrdered, Plus, Upload, UserPlus, Users } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { InviteUserDialog } from '@/components/admin/invitations/InviteUserDialog';

// ============================================================================
// Types
// ============================================================================

interface ActionConfig {
  id: string;
  label: string;
  description: string;
  icon: React.ReactNode;
  behavior: 'navigate' | 'dialog';
  href?: string;
}

export interface QuickActionsGridProps {
  /**
   * Layout variant.
   * - `'grid'` (default, retro-compat): 2/3-col card grid for full-width usage.
   * - `'sidebar'`: vertical compact list for the overview right-sidebar (SP5 F1 A1 mockup `.quick-actions`).
   */
  variant?: 'grid' | 'sidebar';
}

// ============================================================================
// Constants
// ============================================================================

const ACTIONS: ActionConfig[] = [
  {
    id: 'create-game',
    label: 'Crea Gioco',
    description: 'Aggiungi al catalogo',
    icon: <Plus className="h-5 w-5 text-amber-700 dark:text-amber-400" />,
    behavior: 'navigate',
    href: '/admin/shared-games/new',
  },
  {
    id: 'invite-user',
    label: 'Invita Utente',
    description: 'Invia invito email',
    icon: <UserPlus className="h-5 w-5 text-amber-700 dark:text-amber-400" />,
    behavior: 'dialog',
  },
  {
    id: 'manage-games',
    label: 'Gestisci Giochi',
    description: 'Catalogo e filtri',
    icon: <Gamepad2 className="h-5 w-5 text-amber-700 dark:text-amber-400" />,
    behavior: 'navigate',
    href: '/admin/shared-games/all',
  },
  {
    id: 'manage-users',
    label: 'Gestisci Utenti',
    description: 'Lista e ruoli',
    icon: <Users className="h-5 w-5 text-amber-700 dark:text-amber-400" />,
    behavior: 'navigate',
    href: '/admin/users',
  },
  {
    id: 'upload-pdf',
    label: 'Upload PDF',
    description: 'Carica regolamento',
    icon: <Upload className="h-5 w-5 text-amber-700 dark:text-amber-400" />,
    behavior: 'navigate',
    href: '/admin/knowledge-base/upload',
  },
  {
    id: 'view-queue',
    label: 'Vedi Coda',
    description: 'Stato processing',
    icon: <ListOrdered className="h-5 w-5 text-amber-700 dark:text-amber-400" />,
    behavior: 'navigate',
    href: '/admin/knowledge-base/queue',
  },
];

// ============================================================================
// Component
// ============================================================================

export function QuickActionsGrid({ variant = 'grid' }: QuickActionsGridProps = {}) {
  const router = useRouter();
  const [inviteOpen, setInviteOpen] = useState(false);

  const handleAction = (action: ActionConfig) => {
    if (action.behavior === 'navigate' && action.href) {
      router.push(action.href);
    } else if (action.behavior === 'dialog' && action.id === 'invite-user') {
      setInviteOpen(true);
    }
  };

  if (variant === 'sidebar') {
    return (
      <>
        <div
          className="flex flex-col rounded-2xl border border-border/60 bg-card/70 overflow-hidden"
          data-testid="quick-actions-sidebar"
        >
          {ACTIONS.map(action => (
            <button
              key={action.id}
              data-testid={`quick-action-${action.id}`}
              onClick={() => handleAction(action)}
              className="flex items-center gap-3 px-3 py-2.5 text-left bg-transparent border-b border-border/40 last:border-b-0 hover:bg-muted transition-colors"
            >
              <span className="shrink-0">{action.icon}</span>
              <span className="flex flex-col min-w-0">
                <span className="font-quicksand text-sm font-semibold truncate">
                  {action.label}
                </span>
                <span className="font-nunito text-xs text-muted-foreground truncate">
                  {action.description}
                </span>
              </span>
            </button>
          ))}
        </div>
        <InviteUserDialog open={inviteOpen} onOpenChange={setInviteOpen} />
      </>
    );
  }

  return (
    <>
      <div className="grid grid-cols-2 md:grid-cols-3 gap-3" data-testid="quick-actions-grid">
        {ACTIONS.map(action => (
          <button
            key={action.id}
            data-testid={`quick-action-${action.id}`}
            onClick={() => handleAction(action)}
            className="rounded-2xl border border-border/60 dark:border-zinc-700/40 bg-card/70 dark:bg-zinc-800/50 backdrop-blur-sm p-4 text-left hover:border-amber-300 dark:hover:border-amber-700 hover:bg-amber-50/50 dark:hover:bg-amber-950/20 transition-colors"
          >
            <div className="h-10 w-10 rounded-lg bg-amber-100/80 dark:bg-amber-900/30 flex items-center justify-center mb-3">
              {action.icon}
            </div>
            <p className="font-quicksand text-sm font-semibold">{action.label}</p>
            <p className="font-nunito text-xs text-muted-foreground mt-0.5">{action.description}</p>
          </button>
        ))}
      </div>

      <InviteUserDialog open={inviteOpen} onOpenChange={setInviteOpen} />
    </>
  );
}

export default QuickActionsGrid;
