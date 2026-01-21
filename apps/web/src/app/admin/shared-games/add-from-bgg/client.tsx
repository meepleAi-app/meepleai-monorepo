'use client';

/**
 * Add Game from BGG Client Component
 * Issue: Admin Add Shared Game from BGG flow
 *
 * Orchestrates the complete flow:
 * 1. BGG Search with autocomplete
 * 2. Duplicate detection with diff display
 * 3. Preview form with editable fields
 * 4. Import or update confirmation
 */

import { useState, useCallback } from 'react';

import { ArrowLeft, Download, Search } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { AdminAuthGuard } from '@/components/admin';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { BggSearchResult, BggDuplicateCheckResult } from '@/lib/api/schemas/shared-games.schemas';

import { BggGamePreviewForm } from './components/BggGamePreviewForm';
import { BggSearchAutocomplete } from './components/BggSearchAutocomplete';
import { DuplicateDiffModal } from './components/DuplicateDiffModal';

type FlowStep = 'search' | 'loading' | 'preview' | 'duplicate';

export function AddFromBggClient() {
  const router = useRouter();
  const { user, loading: authLoading } = useAuthUser();

  // Flow state
  const [step, setStep] = useState<FlowStep>('search');
  const [selectedBggId, setSelectedBggId] = useState<number | null>(null);
  const [duplicateResult, setDuplicateResult] = useState<BggDuplicateCheckResult | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Handle game selection from autocomplete
  const handleGameSelect = useCallback(async (result: BggSearchResult) => {
    setSelectedBggId(result.bggId);
    setStep('loading');

    try {
      // Check for duplicates
      const checkResult = await api.sharedGames.checkBggDuplicate(result.bggId);
      setDuplicateResult(checkResult);

      if (checkResult.isDuplicate) {
        setStep('duplicate');
      } else {
        setStep('preview');
      }
    } catch (error) {
      console.error('Error checking duplicate:', error);
      toast.error('Errore nel controllo duplicati');
      setStep('search');
    }
  }, []);

  // Handle import new game
  const handleImport = useCallback(async () => {
    if (!selectedBggId) return;

    setIsSubmitting(true);
    try {
      const gameId = await api.sharedGames.importFromBgg(selectedBggId);
      toast.success('Gioco importato con successo!');
      router.push(`/admin/shared-games/${gameId}`);
    } catch (error) {
      console.error('Error importing game:', error);
      toast.error('Errore nell\'importazione del gioco');
      setIsSubmitting(false);
    }
  }, [selectedBggId, router]);

  // Handle update existing game
  const handleUpdate = useCallback(async (fieldsToUpdate: string[]) => {
    if (!duplicateResult?.existingGameId || !selectedBggId) return;

    setIsSubmitting(true);
    try {
      await api.sharedGames.updateFromBgg(duplicateResult.existingGameId, {
        bggId: selectedBggId,
        fieldsToUpdate: fieldsToUpdate.length > 0 ? fieldsToUpdate : undefined,
      });
      toast.success('Gioco aggiornato con successo!');
      router.push(`/admin/shared-games/${duplicateResult.existingGameId}`);
    } catch (error) {
      console.error('Error updating game:', error);
      toast.error('Errore nell\'aggiornamento del gioco');
      setIsSubmitting(false);
    }
  }, [duplicateResult, selectedBggId, router]);

  // Reset flow
  const handleReset = useCallback(() => {
    setStep('search');
    setSelectedBggId(null);
    setDuplicateResult(null);
    setIsSubmitting(false);
  }, []);

  // Navigate back
  const handleBack = useCallback(() => {
    router.push('/admin/shared-games');
  }, [router]);

  if (!user) return null;

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto py-8 px-4">
        {/* Header */}
        <div className="mb-8">
          <Button variant="ghost" size="sm" onClick={handleBack} className="mb-4">
            <ArrowLeft className="h-4 w-4 mr-2" />
            Torna al Catalogo
          </Button>

          <div className="flex items-center gap-3">
            <div className="h-12 w-12 rounded-lg bg-primary/10 flex items-center justify-center">
              <Download className="h-6 w-6 text-primary" />
            </div>
            <div>
              <h1 className="text-3xl font-bold tracking-tight">Importa da BGG</h1>
              <p className="text-muted-foreground">Cerca e importa un gioco da BoardGameGeek</p>
            </div>
          </div>
        </div>

        {/* Search Step */}
        {step === 'search' && (
          <Card className="max-w-2xl">
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Search className="h-5 w-5" />
                Cerca su BoardGameGeek
              </CardTitle>
              <CardDescription>
                Inizia a digitare il nome del gioco per cercarlo su BGG
              </CardDescription>
            </CardHeader>
            <CardContent>
              <BggSearchAutocomplete onSelect={handleGameSelect} />
            </CardContent>
          </Card>
        )}

        {/* Loading Step */}
        {step === 'loading' && (
          <Card className="max-w-2xl">
            <CardContent className="py-12 text-center">
              <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary mx-auto mb-4" />
              <p className="text-muted-foreground">Caricamento dati da BoardGameGeek...</p>
            </CardContent>
          </Card>
        )}

        {/* Preview Step (New Game) */}
        {step === 'preview' && duplicateResult?.bggData && (
          <div className="max-w-4xl">
            <Alert className="mb-6">
              <Download className="h-4 w-4" />
              <AlertTitle>Nuovo gioco</AlertTitle>
              <AlertDescription>
                Questo gioco non esiste ancora nel catalogo. Verifica i dati e conferma l'importazione.
              </AlertDescription>
            </Alert>

            <BggGamePreviewForm
              bggData={duplicateResult.bggData}
              onConfirm={handleImport}
              onCancel={handleReset}
              isSubmitting={isSubmitting}
            />
          </div>
        )}

        {/* Duplicate Step */}
        {step === 'duplicate' && duplicateResult && (
          <DuplicateDiffModal
            duplicateResult={duplicateResult}
            onUpdate={handleUpdate}
            onCancel={handleReset}
            isSubmitting={isSubmitting}
          />
        )}
      </div>
    </AdminAuthGuard>
  );
}
