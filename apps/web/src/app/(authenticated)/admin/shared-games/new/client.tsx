'use client';

/**
 * New Game Client Component - Issue #2372
 *
 * Client component for creating a new shared game.
 * Wraps GameForm with navigation and header.
 */

import { ArrowLeft, Plus } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { AdminAuthGuard, GameForm } from '@/components/admin';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { Button } from '@/components/ui/primitives/button';

export function NewGameClient() {
  const router = useRouter();
  const { user, loading: authLoading } = useAuthUser();

  const handleSubmit = (gameId: string) => {
    // Navigate to the newly created game's edit page
    router.push(`/admin/shared-games/${gameId}`);
  };

  const handleCancel = () => {
    router.push('/admin/shared-games');
  };

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto py-8 px-4">
        {/* Header */}
        <div className="mb-8">
          <Button variant="ghost" size="sm" onClick={handleCancel} className="mb-4">
            <ArrowLeft className="h-4 w-4 mr-2" />
            Torna al Catalogo
          </Button>

          <div className="flex items-center gap-3">
            <div className="h-12 w-12 rounded-lg bg-primary/10 flex items-center justify-center">
              <Plus className="h-6 w-6 text-primary" />
            </div>
            <div>
              <h1 className="text-3xl font-bold tracking-tight">Nuovo Gioco</h1>
              <p className="text-muted-foreground">Aggiungi un nuovo gioco al catalogo condiviso</p>
            </div>
          </div>
        </div>

        {/* Game Form */}
        <div className="max-w-4xl">
          <GameForm game={null} onSubmit={handleSubmit} onCancel={handleCancel} />
        </div>
      </div>
    </AdminAuthGuard>
  );
}