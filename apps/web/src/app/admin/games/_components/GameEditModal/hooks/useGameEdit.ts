/**
 * useGameEdit Hook (Issue #2515)
 *
 * Manages game edit form state and submission logic.
 * Handles both create and edit modes with approval workflow integration.
 */

import { useState, useEffect } from 'react';

import { toast } from 'sonner';

import { useApiClient } from '@/lib/api/context';
import { type SharedGameDetail } from '@/lib/api/schemas/shared-games.schemas';

interface UseGameEditParams {
  game?: SharedGameDetail | null;
  onSuccess?: () => void;
}

interface FormData {
  name?: string;
  bggId?: number;
  bggUrl?: string;
  description?: string;
  pdfDocument?: File | null;
  coverImage?: File | null;
  complexity?: number;
  minPlayers?: number;
  maxPlayers?: number;
  playingTime?: number;
  tags?: string[];
}

export function useGameEdit({ game, onSuccess }: UseGameEditParams) {
  const { sharedGames } = useApiClient();
  const [formData, setFormData] = useState<FormData>({});
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Initialize form data from game
  useEffect(() => {
    if (game) {
      setFormData({
        name: game.title,
        bggId: game.bggId || undefined,
        bggUrl: game.bggId ? `https://boardgamegeek.com/boardgame/${game.bggId}` : '',
        description: game.description || '',
        complexity: game.complexityRating || 3,
        minPlayers: game.minPlayers,
        maxPlayers: game.maxPlayers,
        playingTime: game.playingTimeMinutes,
        tags: [], // TODO: Extract from game categories/mechanics
      });
    }
  }, [game]);

  const handleFormChange = (field: string, value: string | number | boolean | string[] | File | null | undefined) => {
    setFormData((prev) => ({ ...prev, [field]: value }));
  };

  const handleSaveDraft = async () => {
    setIsSubmitting(true);
    try {
      // Validate required fields
      if (!formData.name || !formData.description) {
        toast.error('Name and description are required');
        return;
      }

      const request = {
        title: formData.name,
        description: formData.description,
        minPlayers: formData.minPlayers || 1,
        maxPlayers: formData.maxPlayers || 4,
        playingTimeMinutes: formData.playingTime || 60,
        minAge: 0, // Default
        imageUrl: '', // Will be set after image upload
        thumbnailUrl: '', // Will be set after image upload
        yearPublished: new Date().getFullYear(), // Default to current year
      };

      if (game) {
        // Update existing game
        await sharedGames.update(game.id, request);
        toast.success('Game saved as draft');
      } else {
        // Create new game
        await sharedGames.create(request);
        toast.success('Game created as draft');

        // TODO: Handle file uploads for PDF and image
        // if (formData.pdfDocument) {
        //   await uploadPdf(newId, formData.pdfDocument);
        // }
        // if (formData.coverImage) {
        //   await uploadImage(newId, formData.coverImage);
        // }
      }

      if (onSuccess) onSuccess();
    } catch (error) {
      console.error('Failed to save game:', error);
      toast.error('Failed to save game');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleSubmitForApproval = async () => {
    setIsSubmitting(true);
    try {
      // First save as draft
      await handleSaveDraft();

      // Then submit for approval
      if (game?.id) {
        await sharedGames.submitForApproval(game.id);
        toast.success('Game submitted for approval');
        if (onSuccess) onSuccess();
      }
    } catch (error) {
      console.error('Failed to submit for approval:', error);
      toast.error('Failed to submit game for approval');
    } finally {
      setIsSubmitting(false);
    }
  };

  return {
    formData,
    isSubmitting,
    handleFormChange,
    handleSaveDraft,
    handleSubmitForApproval,
  };
}
