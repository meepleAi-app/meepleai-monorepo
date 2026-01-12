'use client';

import { useState } from 'react';

import { Loader2 } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardFooter, CardHeader } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { api } from '@/lib/api';
import { BggSearchResult } from '@/lib/api/schemas/games.schemas';

export default function AddGamePage() {
  const router = useRouter();
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<BggSearchResult[]>([]);
  const [thumbnails, setThumbnails] = useState<Record<number, string | null>>({});
  const [loading, setLoading] = useState(false);
  const [loadingThumbnails, setLoadingThumbnails] = useState(false);
  const [addingId, setAddingId] = useState<number | null>(null);
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(0);
  const [total, setTotal] = useState(0);

  const handleSearch = async (e?: React.FormEvent, page: number = 1) => {
    e?.preventDefault();
    if (!query.trim()) return;

    setLoading(true);
    setResults([]);
    setThumbnails({});
    try {
      const response = await api.bgg.search(query, false, page, 20);
      setResults(response.results);
      setCurrentPage(response.page);
      setTotalPages(response.totalPages);
      setTotal(response.total);

      // Load thumbnails for current page results
      if (response.results.length > 0) {
        setLoadingThumbnails(true);
        try {
          const bggIds = response.results.map(r => r.bggId);
          const thumbs = await api.bgg.batchThumbnails(bggIds);
          // Convert string keys to numbers
          const thumbsMap: Record<number, string | null> = {};
          Object.entries(thumbs).forEach(([key, value]) => {
            thumbsMap[parseInt(key)] = value as string | null;
          });
          setThumbnails(thumbsMap);
        } catch (error) {
          console.error('Failed to load thumbnails:', error);
        } finally {
          setLoadingThumbnails(false);
        }
      }
    } catch (error) {
      console.error('Search failed:', error);
      toast.error('Ricerca fallita. Riprova più tardi.');
    } finally {
      setLoading(false);
    }
  };

  const handleAddGame = async (game: BggSearchResult) => {
    if (addingId) return;
    setAddingId(game.bggId);

    try {
      // 1. Get full details from BGG
      const details = await api.bgg.getGameDetails(game.bggId);

      // 2. Create game in our system
      await api.games.create({
        name: details.name,
        publisher: details.publishers?.[0] || null,
        yearPublished: details.yearPublished,
        minPlayers: details.minPlayers,
        maxPlayers: details.maxPlayers,
        minPlayTimeMinutes: details.minPlayTime || details.playingTime,
        maxPlayTimeMinutes: details.maxPlayTime || details.playingTime,
        iconUrl: details.thumbnailUrl,
        imageUrl: details.imageUrl,
        bggId: details.bggId,
      });

      toast.success('Gioco aggiunto con successo!');
      router.push('/games');
      router.refresh();
    } catch (error) {
      console.error('Failed to add game:', error);
      toast.error("Errore durante l'aggiunta del gioco.");
      setAddingId(null);
    }
  };

  return (
    <div className="container mx-auto px-4 py-8">
      <div className="max-w-4xl mx-auto">
        <div className="mb-8">
          <h1 className="text-3xl font-bold mb-2">Aggiungi Gioco</h1>
          <p className="text-muted-foreground">
            Cerca un gioco su BoardGameGeek per aggiungerlo alla tua collezione.
          </p>
        </div>

        <form noValidate onSubmit={handleSearch} className="flex gap-4 mb-8">
          <Input
            placeholder="Cerca su BoardGameGeek..."
            value={query}
            onChange={e => setQuery(e.target.value)}
            className="flex-1"
          />
          <Button type="submit" disabled={loading || !query.trim()}>
            {loading ? <Loader2 className="w-4 h-4 animate-spin mr-2" /> : null}
            Cerca
          </Button>
        </form>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {results.map(game => (
            <Card key={game.bggId} className="flex flex-col">
              <CardHeader className="p-4">
                <div className="aspect-square relative flex items-center justify-center bg-muted rounded-md overflow-hidden mb-2">
                  {loadingThumbnails ? (
                    <Loader2 className="w-8 h-8 animate-spin text-muted-foreground" />
                  ) : thumbnails[game.bggId] ? (
                    // eslint-disable-next-line @next/next/no-img-element
                    <img
                      src={thumbnails[game.bggId]!}
                      alt={game.name}
                      className="object-contain w-full h-full"
                    />
                  ) : (
                    <div className="text-muted-foreground text-sm">No Image</div>
                  )}
                </div>
              </CardHeader>
              <CardContent className="flex-1 p-4 pt-0">
                <h3 className="font-semibold text-lg line-clamp-2">{game.name}</h3>
                {game.yearPublished && (
                  <p className="text-sm text-muted-foreground">{game.yearPublished}</p>
                )}
              </CardContent>
              <CardFooter className="p-4 pt-0">
                <Button
                  className="w-full"
                  onClick={() => handleAddGame(game)}
                  disabled={addingId !== null}
                  variant="secondary"
                >
                  {addingId === game.bggId ? (
                    <Loader2 className="w-4 h-4 animate-spin mr-2" />
                  ) : null}
                  Aggiungi
                </Button>
              </CardFooter>
            </Card>
          ))}
        </div>

        {!loading && results.length === 0 && query && (
          <p className="text-center text-muted-foreground py-8">Nessun risultato trovato.</p>
        )}

        {/* Pagination Controls */}
        {totalPages > 1 && (
          <div className="flex items-center justify-center gap-2 mt-8">
            <Button
              variant="outline"
              onClick={() => handleSearch(undefined, currentPage - 1)}
              disabled={currentPage === 1 || loading}
            >
              Precedente
            </Button>
            <span className="text-sm text-muted-foreground px-4">
              Pagina {currentPage} di {totalPages} ({total} risultati totali)
            </span>
            <Button
              variant="outline"
              onClick={() => handleSearch(undefined, currentPage + 1)}
              disabled={currentPage === totalPages || loading}
            >
              Successiva
            </Button>
          </div>
        )}
      </div>
    </div>
  );
}
