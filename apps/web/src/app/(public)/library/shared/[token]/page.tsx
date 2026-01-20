/**
 * Public Shared Library Page (Issue #2614)
 *
 * Public route for viewing shared game libraries via share link token.
 * No authentication required - access controlled by share token in URL.
 *
 * Route: /library/shared/{token}
 *
 * Features:
 * - Display owner's library with game cards
 * - Show owner display name and stats
 * - Support for notes visibility based on share settings
 * - Graceful handling of invalid/expired/revoked tokens
 */

'use client';

import { useParams } from 'next/navigation';

import { BookOpen, AlertTriangle, Star, Library, Clock } from 'lucide-react';
import Link from 'next/link';

import { SharedLibraryGameCard } from '@/components/library';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { Card, CardContent } from '@/components/ui/data-display/card';
import { Badge } from '@/components/ui/data-display/badge';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useSharedLibrary } from '@/hooks/queries';

export default function SharedLibraryPage() {
  const params = useParams();
  const shareToken = params?.token as string;

  // Fetch shared library data
  const {
    data: sharedLibrary,
    isLoading,
    error,
  } = useSharedLibrary(shareToken);

  // Loading state
  if (isLoading) {
    return (
      <main className="min-h-screen bg-background">
        <div className="container mx-auto px-4 py-8 space-y-6">
          {/* Header skeleton */}
          <div className="space-y-4">
            <Skeleton className="h-10 w-64" />
            <Skeleton className="h-6 w-48" />
          </div>
          {/* Stats skeleton */}
          <div className="flex gap-4">
            <Skeleton className="h-10 w-32" />
            <Skeleton className="h-10 w-32" />
          </div>
          {/* Cards skeleton */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {Array.from({ length: 6 }).map((_, i) => (
              <Skeleton key={i} className="h-64 w-full" />
            ))}
          </div>
        </div>
      </main>
    );
  }

  // Error or not found state
  if (error || !sharedLibrary) {
    return (
      <main className="min-h-screen bg-background">
        <div className="container mx-auto px-4 py-16">
          <Card className="max-w-md mx-auto">
            <CardContent className="flex flex-col items-center py-12 text-center">
              <AlertTriangle className="h-16 w-16 text-muted-foreground mb-4" />
              <h2 className="text-xl font-semibold mb-2">Libreria Non Trovata</h2>
              <p className="text-muted-foreground mb-6">
                Questo link di condivisione potrebbe essere scaduto, revocato o non valido.
              </p>
              <Button asChild>
                <Link href="/">Torna alla Home</Link>
              </Button>
            </CardContent>
          </Card>
        </div>
      </main>
    );
  }

  // Format date for display
  const formatDate = (isoDate: string) => {
    return new Date(isoDate).toLocaleDateString('it-IT', {
      day: 'numeric',
      month: 'long',
      year: 'numeric',
    });
  };

  const hasNotes = sharedLibrary.games.some(g => g.notes);

  return (
    <main className="min-h-screen bg-background">
      <div className="container mx-auto px-4 py-8 space-y-8">
        {/* Header */}
        <div className="space-y-4">
          <div className="flex items-center gap-3">
            <Library className="h-8 w-8 text-primary" />
            <h1 className="text-3xl font-bold font-quicksand">
              Libreria di {sharedLibrary.ownerDisplayName}
            </h1>
          </div>
          <p className="text-muted-foreground">
            Una collezione di giochi da tavolo condivisa pubblicamente
          </p>
        </div>

        {/* Stats */}
        <div className="flex flex-wrap gap-4">
          <Badge variant="secondary" className="text-base px-4 py-2 gap-2">
            <BookOpen className="h-4 w-4" />
            {sharedLibrary.totalGames} giochi
          </Badge>
          {sharedLibrary.favoritesCount > 0 && (
            <Badge variant="secondary" className="text-base px-4 py-2 gap-2">
              <Star className="h-4 w-4 text-amber-500" />
              {sharedLibrary.favoritesCount} preferiti
            </Badge>
          )}
          <Badge variant="outline" className="text-base px-4 py-2 gap-2">
            <Clock className="h-4 w-4" />
            Condiviso il {formatDate(sharedLibrary.sharedAt)}
          </Badge>
        </div>

        {/* Privacy Notice */}
        {sharedLibrary.privacyLevel === 'unlisted' && (
          <Alert>
            <AlertTitle className="flex items-center gap-2">
              <span>Libreria Non Elencata</span>
            </AlertTitle>
            <AlertDescription>
              Questa libreria è accessibile solo tramite link diretto.
            </AlertDescription>
          </Alert>
        )}

        {/* Games Grid */}
        {sharedLibrary.games.length > 0 ? (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
            {sharedLibrary.games.map((game) => (
              <SharedLibraryGameCard
                key={game.gameId}
                game={game}
                showNotes={hasNotes}
              />
            ))}
          </div>
        ) : (
          <Card className="border-dashed">
            <CardContent className="flex flex-col items-center justify-center py-12 text-center">
              <BookOpen className="h-16 w-16 text-muted-foreground mb-4" />
              <h3 className="text-xl font-semibold mb-2">Libreria Vuota</h3>
              <p className="text-muted-foreground max-w-md">
                Questa libreria non contiene ancora nessun gioco.
              </p>
            </CardContent>
          </Card>
        )}

        {/* Footer CTA */}
        <div className="border-t pt-8 mt-8">
          <Card className="bg-primary/5 border-primary/20">
            <CardContent className="flex flex-col md:flex-row items-center justify-between gap-4 py-6">
              <div className="text-center md:text-left">
                <h3 className="font-semibold text-lg mb-1">
                  Vuoi creare la tua libreria?
                </h3>
                <p className="text-muted-foreground">
                  Registrati gratuitamente e inizia a gestire la tua collezione di giochi da tavolo.
                </p>
              </div>
              <Button asChild size="lg">
                <Link href="/login">Inizia Ora</Link>
              </Button>
            </CardContent>
          </Card>
        </div>
      </div>
    </main>
  );
}
