'use client';

/**
 * Game Edit Modal Component (Issue #2515)
 *
 * Modal dialog for configuring game details.
 * Two modes:
 * - Create: New game configuration
 * - Edit: Modify existing game
 *
 * Features:
 * - Full form with validation (Zod + react-hook-form)
 * - PDF/Image upload support
 * - Real-time preview card
 * - Save as Draft or Submit for Approval
 */

import { useState, useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { toast } from 'sonner';
import { X } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';

import { useApiClient } from '@/lib/api/context';
import { type SharedGameDetail } from '@/lib/api/schemas/shared-games.schemas';
import { GameEditForm } from './GameEditForm';
import { GamePreviewCard } from './GamePreviewCard';
import { useGameEdit } from './hooks/useGameEdit';

interface GameEditModalProps {
  isOpen: boolean;
  onClose: () => void;
  gameId?: string | null; // If provided, edit mode; otherwise create mode
  onSuccess?: () => void;
}

export function GameEditModal({ isOpen, onClose, gameId, onSuccess }: GameEditModalProps) {
  const { sharedGames } = useApiClient();
  const [game, setGame] = useState<SharedGameDetail | null>(null);
  const [loading, setLoading] = useState(false);
  const [activeTab, setActiveTab] = useState('edit');

  const {
    formData,
    isSubmitting,
    handleFormChange,
    handleSaveDraft,
    handleSubmitForApproval,
  } = useGameEdit({
    game,
    onSuccess: () => {
      onClose();
      if (onSuccess) onSuccess();
    },
  });

  // Load game data in edit mode
  useEffect(() => {
    if (!isOpen || !gameId) {
      setGame(null);
      return;
    }

    const loadGame = async () => {
      setLoading(true);
      try {
        const data = await sharedGames.getById(gameId);
        setGame(data);
      } catch (error) {
        console.error('Failed to load game:', error);
        toast.error('Failed to load game details');
        onClose();
      } finally {
        setLoading(false);
      }
    };

    loadGame();
  }, [isOpen, gameId, sharedGames, onClose]);

  const handleClose = () => {
    if (isSubmitting) {
      if (!confirm('Are you sure? Unsaved changes will be lost.')) {
        return;
      }
    }
    onClose();
  };

  return (
    <Dialog open={isOpen} onOpenChange={(open) => !open && handleClose()}>
      <DialogContent className="max-w-5xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{gameId ? 'Edit Game Configuration' : 'New Game'}</DialogTitle>
          <DialogDescription>
            {gameId
              ? 'Update game details and submit for approval when ready.'
              : 'Configure new game details. Save as draft or submit directly for approval.'}
          </DialogDescription>
        </DialogHeader>

        {loading ? (
          <div className="py-12 flex justify-center">
            <div className="text-center">Loading game data...</div>
          </div>
        ) : (
          <Tabs value={activeTab} onValueChange={setActiveTab} className="w-full">
            <TabsList className="grid w-full grid-cols-2">
              <TabsTrigger value="edit">Edit</TabsTrigger>
              <TabsTrigger value="preview">Preview</TabsTrigger>
            </TabsList>

            <TabsContent value="edit" className="space-y-4">
              <GameEditForm
                game={game}
                onChange={handleFormChange}
                isSubmitting={isSubmitting}
              />
            </TabsContent>

            <TabsContent value="preview" className="space-y-4">
              <GamePreviewCard formData={formData} />
            </TabsContent>
          </Tabs>
        )}

        <DialogFooter className="gap-2">
          <Button variant="outline" onClick={handleClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button
            variant="secondary"
            onClick={handleSaveDraft}
            disabled={isSubmitting || loading}
          >
            Save as Draft
          </Button>
          <Button onClick={handleSubmitForApproval} disabled={isSubmitting || loading}>
            Submit for Approval
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
