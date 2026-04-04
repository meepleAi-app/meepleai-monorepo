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

export function QuickActionsGrid() {
  const router = useRouter();
  const [inviteOpen, setInviteOpen] = useState(false);

  const handleAction = (action: ActionConfig) => {
    if (action.behavior === 'navigate' && action.href) {
      router.push(action.href);
    } else if (action.behavior === 'dialog' && action.id === 'invite-user') {
      setInviteOpen(true);
    }
  };

  return (
    <>
      <div className="grid grid-cols-2 md:grid-cols-3 gap-3" data-testid="quick-actions-grid">
        {ACTIONS.map(action => (
          <button
            key={action.id}
            data-testid={`quick-action-${action.id}`}
            onClick={() => handleAction(action)}
            className="rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-sm p-4 text-left hover:border-amber-300 dark:hover:border-amber-700 hover:bg-amber-50/50 dark:hover:bg-amber-950/20 transition-colors"
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
