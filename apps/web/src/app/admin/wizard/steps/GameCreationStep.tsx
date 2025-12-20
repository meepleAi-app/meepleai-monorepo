'use client';

/**
 * Step 2: Game Creation
 *
 * Create a new game with name, icon, and image (URL or file upload).
 */

import { useState, useCallback } from 'react';
import Image from 'next/image';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Spinner } from '@/components/loading';
import { toast } from '@/components/layout';
import { api } from '@/lib/api';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';

interface GameCreationStepProps {
  pdfId: string;
  pdfFileName: string;
  onComplete: (gameId: string, gameName: string) => void;
  onBack: () => void;
}

type ImageInputMode = 'url' | 'upload';

export function GameCreationStep({
  pdfId: _pdfId,
  pdfFileName,
  onComplete,
  onBack,
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
      // Determine final URLs
      let finalIconUrl: string | null = null;
      let finalImageUrl: string | null = null;

      // For now, we use URL inputs directly
      // TODO: Implement file upload to storage if iconFile/imageFile are set
      if (iconMode === 'url' && iconUrl.trim()) {
        finalIconUrl = iconUrl.trim();
      } else if (iconMode === 'upload' && iconFile) {
        // File upload would go here - for MVP, we'll use the preview data URL
        // In production, this would upload to a storage service
        finalIconUrl = iconPreview;
      }

      if (imageMode === 'url' && imageUrl.trim()) {
        finalImageUrl = imageUrl.trim();
      } else if (imageMode === 'upload' && imageFile) {
        finalImageUrl = imagePreview;
      }

      // Create game with extended parameters
      const result = await api.games.create({
        name: gameName.trim(),
        publisher: publisher.trim() || null,
        yearPublished: yearPublished ?? null,
        iconUrl: finalIconUrl,
        imageUrl: finalImageUrl,
      });

      toast.success(`Gioco "${gameName}" creato con successo!`);
      onComplete(result.id, gameName.trim());
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
    iconPreview,
    imageMode,
    imageUrl,
    imageFile,
    imagePreview,
    onComplete,
  ]);

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold text-slate-900 dark:text-white mb-2">
          Crea il Gioco
        </h2>
        <p className="text-slate-600 dark:text-slate-400">
          Inserisci i dettagli del gioco. Il PDF "{pdfFileName}" verra' associato a questo gioco.
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
        <Button onClick={handleCreate} disabled={!gameName.trim() || creating} className="min-w-32">
          {creating ? (
            <>
              <Spinner size="sm" className="mr-2" />
              Creazione...
            </>
          ) : (
            'Crea Gioco →'
          )}
        </Button>
      </div>
    </div>
  );
}
