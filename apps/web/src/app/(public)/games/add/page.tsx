'use client';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardFooter, CardHeader } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { api } from '@/lib/api';
import { BggSearchResult } from '@/lib/api/schemas/games.schemas';
import { Loader2 } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { toast } from 'sonner';

export default function AddGamePage() {
  const router = useRouter();
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<BggSearchResult[]>([]);
  const [loading, setLoading] = useState(false);
  const [addingId, setAddingId] = useState<number | null>(null);

  const handleSearch = async (e?: React.FormEvent) => {
    e?.preventDefault();
    if (!query.trim()) return;

    setLoading(true);
    setResults([]);
    try {
      const response = await api.bgg.search(query);
      setResults(response.results);
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

        <form onSubmit={handleSearch} className="flex gap-4 mb-8">
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
                  {game.thumbnailUrl ? (
                    // eslint-disable-next-line @next/next/no-img-element
                    <img
                      src={game.thumbnailUrl}
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
      </div>
    </div>
  );
}
