'use client';

/**
 * Step 2: Game Creation
 *
 * Create a new game with name, icon, and image (URL or file upload).
 */

import { useState, useCallback } from 'react';

import Image from 'next/image';

import { toast } from '@/components/layout';
import { Spinner } from '@/components/loading';
import { Card } from '@/components/ui/data-display/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { api } from '@/lib/api';

interface GameCreationStepProps {
  pdfId?: string | null;  // Optional for user wizard (Issue #4)
  pdfFileName?: string | null;  // Optional for user wizard
  allowSkipPdf?: boolean;  // Show "Skip PDF" button for user wizard
  onComplete: (gameId: string, gameName: string) => void;
  onSkipPdf?: () => void;  // Callback for skip PDF button
  onBack: () => void;
  /**
   * Determines which API endpoint to use for game creation.
   * 'admin' (default): POST /api/v1/games (shared catalog — admin only)
   * 'user': POST /api/v1/private-games (user's private library)
   */
  mode?: 'admin' | 'user';
}

type ImageInputMode = 'url' | 'upload';

export function GameCreationStep({
  pdfId,
  pdfFileName,
  allowSkipPdf = false,
  onComplete,
  onSkipPdf,
  onBack,
  mode = 'admin',
}: GameCreationStepProps) {
  const [gameName, setGameName] = useState('');
  const [publisher, setPublisher] = useState('');
  const [yearPublished, setYearPublished] = useState<number | undefined>();

  // Icon
  const [iconMode, setIconMode] = useState<ImageInputMode>('url');
  const [iconUrl, setIconUrl] = useState('');
  const [iconFile, setIconFile] = useState<File | null>(null);
  const [iconPreview, setIconPreview] = useState<string | null>(null);

  // Image
  const [imageMode, setImageMode] = useState<ImageInputMode>('url');
  const [imageUrl, setImageUrl] = useState('');
  const [imageFile, setImageFile] = useState<File | null>(null);
  const [imagePreview, setImagePreview] = useState<string | null>(null);

  const [creating, setCreating] = useState(false);

  const handleIconFileChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      if (!file.type.startsWith('image/')) {
        toast.error('Seleziona un file immagine');
        return;
      }

      // SECURITY FIX: Client-side file size validation (icon max 2MB)
      // Code review finding: Validate early for better UX
      const maxIconSize = 2 * 1024 * 1024; // 2MB
      if (file.size > maxIconSize) {
        toast.error('Icona troppo grande (max 2MB)');
        return;
      }

      setIconFile(file);
      // Create preview
      const reader = new FileReader();
      reader.onload = () => setIconPreview(reader.result as string);
      reader.readAsDataURL(file);
    }
  }, []);

  const handleImageFileChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      if (!file.type.startsWith('image/')) {
        toast.error('Seleziona un file immagine');
        return;
      }

      // SECURITY FIX: Client-side file size validation (image max 5MB)
      // Code review finding: Validate early for better UX
      const maxImageSize = 5 * 1024 * 1024; // 5MB
      if (file.size > maxImageSize) {
        toast.error('Immagine troppo grande (max 5MB)');
        return;
      }

      setImageFile(file);
      // Create preview
      const reader = new FileReader();
      reader.onload = () => setImagePreview(reader.result as string);
      reader.readAsDataURL(file);
    }
  }, []);

  const handleCreate = useCallback(async () => {
    if (!gameName.trim()) {
      toast.error('Inserisci il nome del gioco');
      return;
    }

    setCreating(true);

    try {
      let gameId: string;

      if (mode === 'user') {
        // ── User wizard: create private game via /api/v1/private-games ──────────
        const result = await api.library.addPrivateGame({
          source: 'Manual',
          title: gameName.trim(),
          yearPublished: yearPublished ?? null,
          // Required fields — user can edit them later
          minPlayers: 1,
          maxPlayers: 99,
          imageUrl: null,
          thumbnailUrl: null,
        });
        gameId = result.id;

        // Upload cover image (file upload uses shared storage endpoint)
        let finalImageUrl: string | null = null;

        if (imageMode === 'url' && imageUrl.trim()) {
          finalImageUrl = imageUrl.trim();
        } else if (imageMode === 'upload' && imageFile) {
          toast.info('Caricamento immagine copertina...');
          const uploadResult = await api.games.uploadImage(imageFile, gameId, 'image');
          if (uploadResult.success && uploadResult.fileUrl) {
            finalImageUrl = uploadResult.fileUrl;
          } else {
            toast.warning(`Errore caricamento immagine: ${uploadResult.error || 'Errore sconosciuto'}`);
          }
        }

        // Update private game with image URL if available
        if (finalImageUrl) {
          await api.library.updatePrivateGame(gameId, {
            title: gameName.trim(),
            minPlayers: 1,
            maxPlayers: 99,
            imageUrl: finalImageUrl,
          });
        }
      } else {
        // ── Admin wizard: create shared game via /api/v1/games ──────────────────
        const result = await api.games.create({
          title: gameName.trim(),
          publisher: publisher.trim() || null,
          yearPublished: yearPublished ?? null,
          iconUrl: null,
          imageUrl: null,
          pdfId: pdfId || null,
        });
        gameId = result.id;

        // Step 2: Upload files if provided and link to game (Issue #2255)
        let finalIconUrl: string | null = null;
        let finalImageUrl: string | null = null;

        if (iconMode === 'url' && iconUrl.trim()) {
          finalIconUrl = iconUrl.trim();
        } else if (iconMode === 'upload' && iconFile) {
          toast.info('Caricamento icona...');
          const uploadResult = await api.games.uploadImage(iconFile, gameId, 'icon');
          if (uploadResult.success && uploadResult.fileUrl) {
            finalIconUrl = uploadResult.fileUrl;
          } else {
            toast.warning(`Errore caricamento icona: ${uploadResult.error || 'Errore sconosciuto'}`);
          }
        }

        if (imageMode === 'url' && imageUrl.trim()) {
          finalImageUrl = imageUrl.trim();
        } else if (imageMode === 'upload' && imageFile) {
          toast.info('Caricamento immagine copertina...');
          const uploadResult = await api.games.uploadImage(imageFile, gameId, 'image');
          if (uploadResult.success && uploadResult.fileUrl) {
            finalImageUrl = uploadResult.fileUrl;
          } else {
            toast.warning(`Errore caricamento immagine: ${uploadResult.error || 'Errore sconosciuto'}`);
          }
        }

        // Step 3: Link uploaded files to game record (Issue #2255 - Code review fix)
        if (finalIconUrl || finalImageUrl) {
          await api.games.update(gameId, {
            iconUrl: finalIconUrl,
            imageUrl: finalImageUrl,
          });
        }
      }

      toast.success(`Gioco "${gameName}" creato con successo!`);
      onComplete(gameId, gameName.trim());
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore sconosciuto';
      toast.error(`Creazione fallita: ${message}`);
    } finally {
      setCreating(false);
    }
  }, [
    gameName,
    publisher,
    yearPublished,
    iconMode,
    iconUrl,
    iconFile,
    imageMode,
    imageUrl,
    imageFile,
    pdfId,
    mode,
    onComplete,
  ]);

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold text-slate-900 dark:text-white mb-2">
          Crea il Gioco
        </h2>
        <p className="text-slate-600 dark:text-slate-400">
          {pdfFileName
            ? `Inserisci i dettagli del gioco. Il PDF "${pdfFileName}" verra' associato a questo gioco.`
            : 'Inserisci i dettagli del gioco. Potrai aggiungere un PDF in seguito.'}
        </p>
      </div>

      {/* Game Name */}
      <div className="space-y-2">
        <Label htmlFor="game-name" className="text-base font-medium">
          Nome del Gioco *
        </Label>
        <Input
          id="game-name"
          value={gameName}
          onChange={e => setGameName(e.target.value)}
          placeholder="es. Catan, Ticket to Ride, Wingspan..."
          className="text-lg"
          autoFocus
        />
      </div>

      {/* Optional Info */}
      <div className="grid grid-cols-2 gap-4">
        <div className="space-y-2">
          <Label htmlFor="publisher">Editore</Label>
          <Input
            id="publisher"
            value={publisher}
            onChange={e => setPublisher(e.target.value)}
            placeholder="es. Asmodee, Stonemaier..."
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor="year">Anno di Pubblicazione</Label>
          <Input
            id="year"
            type="number"
            min={1900}
            max={new Date().getFullYear() + 1}
            value={yearPublished ?? ''}
            onChange={e =>
              setYearPublished(e.target.value ? parseInt(e.target.value, 10) : undefined)
            }
            placeholder="es. 2023"
          />
        </div>
      </div>

      {/* Icon */}
      <Card className="p-4 space-y-4">
        <Label className="text-base font-medium">Icona del Gioco</Label>
        <Tabs value={iconMode} onValueChange={v => setIconMode(v as ImageInputMode)}>
          <TabsList className="grid w-full grid-cols-2">
            <TabsTrigger value="url">URL</TabsTrigger>
            <TabsTrigger value="upload">Upload</TabsTrigger>
          </TabsList>
          <TabsContent value="url" className="space-y-2">
            <Input
              value={iconUrl}
              onChange={e => setIconUrl(e.target.value)}
              placeholder="https://example.com/icon.png"
              type="url"
            />
            {iconUrl && (
              <div className="flex justify-center">
                <div className="relative w-16 h-16 rounded-lg border overflow-hidden">
                  <Image
                    src={iconUrl}
                    alt="Icon preview"
                    fill
                    className="object-cover"
                    sizes="64px"
                    onError={e => {
                      (e.target as HTMLImageElement).style.display = 'none';
                    }}
                  />
                </div>
              </div>
            )}
          </TabsContent>
          <TabsContent value="upload" className="space-y-2">
            <Input type="file" accept="image/*" onChange={handleIconFileChange} />
            {iconPreview && (
              <div className="flex justify-center">
                <div className="relative w-16 h-16 rounded-lg border overflow-hidden">
                  <Image
                    src={iconPreview}
                    alt="Icon preview"
                    fill
                    className="object-cover"
                    sizes="64px"
                    unoptimized
                  />
                </div>
              </div>
            )}
          </TabsContent>
        </Tabs>
      </Card>

      {/* Cover Image */}
      <Card className="p-4 space-y-4">
        <Label className="text-base font-medium">Immagine di Copertina</Label>
        <Tabs value={imageMode} onValueChange={v => setImageMode(v as ImageInputMode)}>
          <TabsList className="grid w-full grid-cols-2">
            <TabsTrigger value="url">URL</TabsTrigger>
            <TabsTrigger value="upload">Upload</TabsTrigger>
          </TabsList>
          <TabsContent value="url" className="space-y-2">
            <Input
              value={imageUrl}
              onChange={e => setImageUrl(e.target.value)}
              placeholder="https://example.com/cover.jpg"
              type="url"
            />
            {imageUrl && (
              <div className="flex justify-center">
                <div className="relative max-w-xs h-48 rounded-lg border overflow-hidden">
                  <Image
                    src={imageUrl}
                    alt="Cover preview"
                    fill
                    className="object-cover"
                    sizes="(max-width: 768px) 100vw, 384px"
                    onError={e => {
                      (e.target as HTMLImageElement).style.display = 'none';
                    }}
                  />
                </div>
              </div>
            )}
          </TabsContent>
          <TabsContent value="upload" className="space-y-2">
            <Input type="file" accept="image/*" onChange={handleImageFileChange} />
            {imagePreview && (
              <div className="flex justify-center">
                <div className="relative max-w-xs h-48 rounded-lg border overflow-hidden">
                  <Image
                    src={imagePreview}
                    alt="Cover preview"
                    fill
                    className="object-cover"
                    sizes="(max-width: 768px) 100vw, 384px"
                    unoptimized
                  />
                </div>
              </div>
            )}
          </TabsContent>
        </Tabs>
      </Card>

      {/* Actions */}
      <div className="flex justify-between gap-3">
        <Button variant="outline" onClick={onBack} disabled={creating}>
          ← Indietro
        </Button>
        <div className="flex gap-2">
          {allowSkipPdf && onSkipPdf && (
            <Button
              variant="outline"
              onClick={onSkipPdf}
              disabled={creating}
            >
              Salta PDF →
            </Button>
          )}
          <Button onClick={handleCreate} disabled={!gameName.trim() || creating} className="min-w-32">
            {creating ? (
              <>
                <Spinner size="sm" className="mr-2" />
                Creazione...
              </>
            ) : (
              allowSkipPdf ? 'Crea e Continua →' : 'Crea Gioco →'
            )}
          </Button>
        </div>
      </div>
    </div>
  );
}
