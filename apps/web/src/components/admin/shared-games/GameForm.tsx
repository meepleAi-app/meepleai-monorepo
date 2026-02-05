/**
 * Game Form Component - Issue #2372, #3642
 *
 * Reusable form for creating and editing shared games.
 * Features:
 * - Full validation with Zod + react-hook-form
 * - Category and mechanic multi-select
 * - Image URL preview
 * - Rules content editor
 * - BGG ID integration
 * - PDF upload with indexing status (#3642)
 */

'use client';

import { useState, useEffect } from 'react';

import { zodResolver } from '@hookform/resolvers/zod';
import { ImageIcon, X, Plus, ExternalLink } from 'lucide-react';
import { useForm, Controller, type Resolver } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';


import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Separator } from '@/components/ui/navigation/separator';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import { api, type GameCategory, type GameMechanic, type SharedGameDetail } from '@/lib/api';

import { PdfIndexingStatus } from './PdfIndexingStatus';
import { PdfUploadSection, type UploadedPdf } from './PdfUploadSection';

// ========== Form Schema ==========

const GameFormSchema = z.object({
  title: z.string().min(1, 'Il titolo è obbligatorio').max(255),
  yearPublished: z.coerce.number().int().min(1900, 'Anno non valido').max(2100),
  description: z.string().min(1, 'La descrizione è obbligatoria'),
  minPlayers: z.coerce.number().int().min(1, 'Minimo 1 giocatore').max(100),
  maxPlayers: z.coerce.number().int().min(1, 'Minimo 1 giocatore').max(100),
  playingTimeMinutes: z.coerce.number().int().min(1, 'Minimo 1 minuto').max(10000),
  minAge: z.coerce.number().int().min(0).max(100),
  complexityRating: z.coerce.number().min(0).max(5).nullable().optional(),
  averageRating: z.coerce.number().min(0).max(10).nullable().optional(),
  imageUrl: z.string().url('URL immagine non valido'),
  thumbnailUrl: z.string().url('URL thumbnail non valido'),
  bggId: z.coerce.number().int().positive().nullable().optional(),
  rulesContent: z.string().optional(),
  rulesLanguage: z.string().max(10).optional(),
});

export type GameFormData = z.infer<typeof GameFormSchema>;

// ========== Component Props ==========

export interface GameFormProps {
  /** Existing game data for edit mode */
  game?: SharedGameDetail | null;
  /** Callback on successful form submission */
  onSubmit: (gameId: string) => void;
  /** Callback to cancel form */
  onCancel: () => void;
  /** Whether to show loading state */
  isLoading?: boolean;
}

// ========== Component ==========

export function GameForm({ game, onSubmit, onCancel, isLoading = false }: GameFormProps) {
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [categories, setCategories] = useState<GameCategory[]>([]);
  const [mechanics, setMechanics] = useState<GameMechanic[]>([]);
  const [selectedCategories, setSelectedCategories] = useState<string[]>([]);
  const [selectedMechanics, setSelectedMechanics] = useState<string[]>([]);
  const [showRulesEditor, setShowRulesEditor] = useState(!!game?.rules);
  // PDF upload state (#3642)
  const [uploadedPdf, setUploadedPdf] = useState<UploadedPdf | null>(null);
  const [showPdfStatus, setShowPdfStatus] = useState(false);

  const isEditMode = !!game;
  const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

  const {
    register,
    handleSubmit,
    control,
    watch,
    setValue: _setValue,
    formState: { errors },
  } = useForm<GameFormData>({
    // Type assertion needed because z.coerce.number() creates unknown input type
    resolver: zodResolver(GameFormSchema) as Resolver<GameFormData>,
    defaultValues: game
      ? {
          title: game.title,
          yearPublished: game.yearPublished,
          description: game.description,
          minPlayers: game.minPlayers,
          maxPlayers: game.maxPlayers,
          playingTimeMinutes: game.playingTimeMinutes,
          minAge: game.minAge,
          complexityRating: game.complexityRating,
          averageRating: game.averageRating,
          imageUrl: game.imageUrl,
          thumbnailUrl: game.thumbnailUrl,
          bggId: game.bggId,
          rulesContent: game.rules?.content || '',
          rulesLanguage: game.rules?.language || 'it',
        }
      : {
          title: '',
          yearPublished: new Date().getFullYear(),
          description: '',
          minPlayers: 2,
          maxPlayers: 4,
          playingTimeMinutes: 60,
          minAge: 10,
          complexityRating: null,
          averageRating: null,
          imageUrl: '',
          thumbnailUrl: '',
          bggId: null,
          rulesContent: '',
          rulesLanguage: 'it',
        },
  });

  // Watch image URLs for preview
  const imageUrl = watch('imageUrl');
  const thumbnailUrl = watch('thumbnailUrl');

  // Fetch reference data
  useEffect(() => {
    const fetchReferenceData = async () => {
      try {
        const [categoriesResult, mechanicsResult] = await Promise.all([
          api.sharedGames.getCategories(),
          api.sharedGames.getMechanics(),
        ]);
        setCategories(categoriesResult);
        setMechanics(mechanicsResult);
      } catch (err) {
        console.error('Failed to fetch reference data:', err);
        toast.error('Errore nel caricamento di categorie e meccaniche');
      }
    };

    fetchReferenceData();
  }, []);

  // Handle form submission
  const onFormSubmit = async (data: GameFormData) => {
    setIsSubmitting(true);
    try {
      const requestData = {
        title: data.title,
        yearPublished: data.yearPublished,
        description: data.description,
        minPlayers: data.minPlayers,
        maxPlayers: data.maxPlayers,
        playingTimeMinutes: data.playingTimeMinutes,
        minAge: data.minAge,
        complexityRating: data.complexityRating || null,
        averageRating: data.averageRating || null,
        imageUrl: data.imageUrl,
        thumbnailUrl: data.thumbnailUrl,
        bggId: data.bggId || null,
        rules:
          showRulesEditor && data.rulesContent
            ? {
                content: data.rulesContent,
                language: data.rulesLanguage || 'it',
              }
            : null,
        categoryIds: selectedCategories,
        mechanicIds: selectedMechanics,
      };

      let gameId: string;
      if (isEditMode) {
        await api.sharedGames.update(game.id, requestData);
        gameId = game.id;
        toast.success('Gioco aggiornato con successo');
      } else {
        // create() returns the new game ID directly as a string
        gameId = await api.sharedGames.create(requestData);
        toast.success('Gioco creato con successo');
      }

      // Link uploaded PDF to the game if present (#3642)
      if (uploadedPdf && !isEditMode) {
        try {
          const response = await fetch(`${API_BASE}/api/v1/admin/shared-games/${gameId}/documents`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include',
            body: JSON.stringify({ documentId: uploadedPdf.id, setAsActive: true }),
          });
          if (response.ok) {
            toast.success('PDF collegato al gioco');
            setShowPdfStatus(true);
          }
        } catch {
          toast.error('Errore nel collegamento del PDF');
        }
      }

      onSubmit(gameId);
    } catch (error) {
      console.error('Failed to save game:', error);
      toast.error('Errore nel salvataggio del gioco', {
        description: error instanceof Error ? error.message : 'Errore sconosciuto',
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  // Toggle category selection
  const toggleCategory = (categoryId: string) => {
    setSelectedCategories(prev =>
      prev.includes(categoryId) ? prev.filter(id => id !== categoryId) : [...prev, categoryId]
    );
  };

  // Toggle mechanic selection
  const toggleMechanic = (mechanicId: string) => {
    setSelectedMechanics(prev =>
      prev.includes(mechanicId) ? prev.filter(id => id !== mechanicId) : [...prev, mechanicId]
    );
  };

  return (
    <form onSubmit={handleSubmit(onFormSubmit)} className="space-y-6">
      {/* Basic Information Card */}
      <Card>
        <CardHeader>
          <CardTitle>Informazioni Base</CardTitle>
          <CardDescription>Titolo, anno di pubblicazione e descrizione del gioco</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {/* Title */}
            <div className="md:col-span-2">
              <Label htmlFor="title">Titolo *</Label>
              <Input
                id="title"
                {...register('title')}
                placeholder="Nome del gioco da tavolo"
                disabled={isSubmitting || isLoading}
              />
              {errors.title && (
                <p className="text-sm text-destructive mt-1">{errors.title.message}</p>
              )}
            </div>

            {/* Year Published */}
            <div>
              <Label htmlFor="yearPublished">Anno di pubblicazione *</Label>
              <Input
                id="yearPublished"
                type="number"
                {...register('yearPublished')}
                placeholder="2024"
                disabled={isSubmitting || isLoading}
              />
              {errors.yearPublished && (
                <p className="text-sm text-destructive mt-1">{errors.yearPublished.message}</p>
              )}
            </div>

            {/* BGG ID */}
            <div>
              <Label htmlFor="bggId">BoardGameGeek ID</Label>
              <div className="flex gap-2">
                <Input
                  id="bggId"
                  type="number"
                  {...register('bggId')}
                  placeholder="ID opzionale"
                  disabled={isSubmitting || isLoading}
                />
                {watch('bggId') && (
                  <Button
                    type="button"
                    variant="outline"
                    size="icon"
                    onClick={() =>
                      window.open(`https://boardgamegeek.com/boardgame/${watch('bggId')}`, '_blank')
                    }
                  >
                    <ExternalLink className="h-4 w-4" />
                  </Button>
                )}
              </div>
              {errors.bggId && (
                <p className="text-sm text-destructive mt-1">{errors.bggId.message}</p>
              )}
            </div>
          </div>

          {/* Description */}
          <div>
            <Label htmlFor="description">Descrizione *</Label>
            <Textarea
              id="description"
              {...register('description')}
              placeholder="Descrizione dettagliata del gioco, regole base, tema..."
              rows={4}
              disabled={isSubmitting || isLoading}
            />
            {errors.description && (
              <p className="text-sm text-destructive mt-1">{errors.description.message}</p>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Game Details Card */}
      <Card>
        <CardHeader>
          <CardTitle>Dettagli Gioco</CardTitle>
          <CardDescription>Numero giocatori, durata, età e complessità</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            {/* Min Players */}
            <div>
              <Label htmlFor="minPlayers">Giocatori Min *</Label>
              <Input
                id="minPlayers"
                type="number"
                {...register('minPlayers')}
                min={1}
                max={100}
                disabled={isSubmitting || isLoading}
              />
              {errors.minPlayers && (
                <p className="text-sm text-destructive mt-1">{errors.minPlayers.message}</p>
              )}
            </div>

            {/* Max Players */}
            <div>
              <Label htmlFor="maxPlayers">Giocatori Max *</Label>
              <Input
                id="maxPlayers"
                type="number"
                {...register('maxPlayers')}
                min={1}
                max={100}
                disabled={isSubmitting || isLoading}
              />
              {errors.maxPlayers && (
                <p className="text-sm text-destructive mt-1">{errors.maxPlayers.message}</p>
              )}
            </div>

            {/* Playing Time */}
            <div>
              <Label htmlFor="playingTimeMinutes">Durata (min) *</Label>
              <Input
                id="playingTimeMinutes"
                type="number"
                {...register('playingTimeMinutes')}
                min={1}
                disabled={isSubmitting || isLoading}
              />
              {errors.playingTimeMinutes && (
                <p className="text-sm text-destructive mt-1">{errors.playingTimeMinutes.message}</p>
              )}
            </div>

            {/* Min Age */}
            <div>
              <Label htmlFor="minAge">Età Minima *</Label>
              <Input
                id="minAge"
                type="number"
                {...register('minAge')}
                min={0}
                max={100}
                disabled={isSubmitting || isLoading}
              />
              {errors.minAge && (
                <p className="text-sm text-destructive mt-1">{errors.minAge.message}</p>
              )}
            </div>
          </div>

          <Separator />

          <div className="grid grid-cols-2 gap-4">
            {/* Complexity Rating */}
            <div>
              <Label htmlFor="complexityRating">Complessità (0-5)</Label>
              <Input
                id="complexityRating"
                type="number"
                step="0.1"
                {...register('complexityRating')}
                placeholder="es. 2.5"
                min={0}
                max={5}
                disabled={isSubmitting || isLoading}
              />
              <p className="text-sm text-muted-foreground mt-1">
                Scala BGG: 1 = Leggero, 5 = Pesante
              </p>
              {errors.complexityRating && (
                <p className="text-sm text-destructive mt-1">{errors.complexityRating.message}</p>
              )}
            </div>

            {/* Average Rating */}
            <div>
              <Label htmlFor="averageRating">Rating Medio (0-10)</Label>
              <Input
                id="averageRating"
                type="number"
                step="0.1"
                {...register('averageRating')}
                placeholder="es. 7.5"
                min={0}
                max={10}
                disabled={isSubmitting || isLoading}
              />
              <p className="text-sm text-muted-foreground mt-1">Rating medio dalla community</p>
              {errors.averageRating && (
                <p className="text-sm text-destructive mt-1">{errors.averageRating.message}</p>
              )}
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Images Card */}
      <Card>
        <CardHeader>
          <CardTitle>Immagini</CardTitle>
          <CardDescription>URL dell&apos;immagine principale e thumbnail</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            {/* Image URL */}
            <div>
              <Label htmlFor="imageUrl">URL Immagine *</Label>
              <Input
                id="imageUrl"
                {...register('imageUrl')}
                placeholder="https://example.com/game-image.jpg"
                disabled={isSubmitting || isLoading}
              />
              {errors.imageUrl && (
                <p className="text-sm text-destructive mt-1">{errors.imageUrl.message}</p>
              )}
              {/* Image Preview */}
              <div className="mt-2 border rounded-lg p-2 bg-muted/50">
                {imageUrl ? (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img
                    src={imageUrl}
                    alt="Preview"
                    className="w-full h-40 object-contain rounded"
                    onError={e => {
                      (e.target as HTMLImageElement).src = '';
                      (e.target as HTMLImageElement).style.display = 'none';
                    }}
                  />
                ) : (
                  <div className="w-full h-40 flex items-center justify-center text-muted-foreground">
                    <ImageIcon className="h-12 w-12" />
                  </div>
                )}
              </div>
            </div>

            {/* Thumbnail URL */}
            <div>
              <Label htmlFor="thumbnailUrl">URL Thumbnail *</Label>
              <Input
                id="thumbnailUrl"
                {...register('thumbnailUrl')}
                placeholder="https://example.com/game-thumb.jpg"
                disabled={isSubmitting || isLoading}
              />
              {errors.thumbnailUrl && (
                <p className="text-sm text-destructive mt-1">{errors.thumbnailUrl.message}</p>
              )}
              {/* Thumbnail Preview */}
              <div className="mt-2 border rounded-lg p-2 bg-muted/50">
                {thumbnailUrl ? (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img
                    src={thumbnailUrl}
                    alt="Thumbnail Preview"
                    className="w-full h-40 object-contain rounded"
                    onError={e => {
                      (e.target as HTMLImageElement).src = '';
                      (e.target as HTMLImageElement).style.display = 'none';
                    }}
                  />
                ) : (
                  <div className="w-full h-40 flex items-center justify-center text-muted-foreground">
                    <ImageIcon className="h-12 w-12" />
                  </div>
                )}
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Categories Card */}
      <Card>
        <CardHeader>
          <CardTitle>Categorie</CardTitle>
          <CardDescription>Seleziona le categorie del gioco</CardDescription>
        </CardHeader>
        <CardContent>
          {categories.length === 0 ? (
            <p className="text-sm text-muted-foreground">Nessuna categoria disponibile</p>
          ) : (
            <div className="flex flex-wrap gap-2">
              {categories.map(category => (
                <Badge
                  key={category.id}
                  variant={selectedCategories.includes(category.id) ? 'default' : 'outline'}
                  className="cursor-pointer transition-colors"
                  onClick={() => toggleCategory(category.id)}
                >
                  {category.name}
                  {selectedCategories.includes(category.id) && <X className="h-3 w-3 ml-1" />}
                </Badge>
              ))}
            </div>
          )}
          <p className="text-sm text-muted-foreground mt-2">
            {selectedCategories.length} categorie selezionate
          </p>
        </CardContent>
      </Card>

      {/* Mechanics Card */}
      <Card>
        <CardHeader>
          <CardTitle>Meccaniche</CardTitle>
          <CardDescription>Seleziona le meccaniche del gioco</CardDescription>
        </CardHeader>
        <CardContent>
          {mechanics.length === 0 ? (
            <p className="text-sm text-muted-foreground">Nessuna meccanica disponibile</p>
          ) : (
            <div className="flex flex-wrap gap-2">
              {mechanics.map(mechanic => (
                <Badge
                  key={mechanic.id}
                  variant={selectedMechanics.includes(mechanic.id) ? 'secondary' : 'outline'}
                  className="cursor-pointer transition-colors"
                  onClick={() => toggleMechanic(mechanic.id)}
                >
                  {mechanic.name}
                  {selectedMechanics.includes(mechanic.id) && <X className="h-3 w-3 ml-1" />}
                </Badge>
              ))}
            </div>
          )}
          <p className="text-sm text-muted-foreground mt-2">
            {selectedMechanics.length} meccaniche selezionate
          </p>
        </CardContent>
      </Card>

      {/* Rules Card */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div>
              <CardTitle>Regole</CardTitle>
              <CardDescription>Contenuto delle regole del gioco (opzionale)</CardDescription>
            </div>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => setShowRulesEditor(!showRulesEditor)}
            >
              {showRulesEditor ? (
                <>
                  <X className="h-4 w-4 mr-1" />
                  Nascondi Editor
                </>
              ) : (
                <>
                  <Plus className="h-4 w-4 mr-1" />
                  Aggiungi Regole
                </>
              )}
            </Button>
          </div>
        </CardHeader>
        {showRulesEditor && (
          <CardContent className="space-y-4">
            <div>
              <Label htmlFor="rulesLanguage">Lingua</Label>
              <Controller
                name="rulesLanguage"
                control={control}
                render={({ field }) => (
                  <Select
                    onValueChange={field.onChange}
                    value={field.value || 'it'}
                    disabled={isSubmitting || isLoading}
                  >
                    <SelectTrigger className="w-[200px]">
                      <SelectValue placeholder="Seleziona lingua" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="it">Italiano</SelectItem>
                      <SelectItem value="en">English</SelectItem>
                      <SelectItem value="de">Deutsch</SelectItem>
                      <SelectItem value="fr">Français</SelectItem>
                      <SelectItem value="es">Español</SelectItem>
                    </SelectContent>
                  </Select>
                )}
              />
            </div>

            <div>
              <Label htmlFor="rulesContent">Contenuto Regole</Label>
              <Textarea
                id="rulesContent"
                {...register('rulesContent')}
                placeholder="Inserisci il testo delle regole del gioco..."
                rows={10}
                disabled={isSubmitting || isLoading}
              />
            </div>
          </CardContent>
        )}
      </Card>

      {/* PDF Upload Section (#3642) */}
      <PdfUploadSection
        gameId={isEditMode ? game.id : undefined}
        onPdfUploaded={(pdf) => {
          setUploadedPdf(pdf);
          if (isEditMode) {
            setShowPdfStatus(true);
          }
        }}
        onPdfRemoved={() => {
          setUploadedPdf(null);
          setShowPdfStatus(false);
        }}
        disabled={isSubmitting || isLoading}
        existingPdf={uploadedPdf}
      />

      {/* PDF Indexing Status (#3642) */}
      {showPdfStatus && uploadedPdf && (
        <PdfIndexingStatus
          pdfId={uploadedPdf.id}
          fileName={uploadedPdf.fileName}
          onComplete={() => toast.success('PDF indicizzato - RAG pronto!')}
          onError={(err) => toast.error(`Errore indicizzazione: ${err}`)}
        />
      )}

      {/* Form Actions */}
      <div className="flex justify-end gap-3 pt-4">
        <Button
          type="button"
          variant="outline"
          onClick={onCancel}
          disabled={isSubmitting || isLoading}
        >
          Annulla
        </Button>
        <Button type="submit" disabled={isSubmitting || isLoading}>
          {isSubmitting ? 'Salvataggio...' : isEditMode ? 'Aggiorna Gioco' : 'Crea Gioco'}
        </Button>
      </div>
    </form>
  );
}
