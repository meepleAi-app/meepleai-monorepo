'use client';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Badge } from '@/components/ui/badge';

interface Game {
  id: string;
  title: string;
  publisher: string;
  imageUrl: string;
  rating: number;
  playerCount: string;
  status: 'published' | 'draft' | 'archived';
}

const MOCK_GAMES: Game[] = [
  {
    id: '1',
    title: 'Catan',
    publisher: 'Catan Studio',
    imageUrl: 'https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__imagepage/img/M_3Vg1j2HlNgkv7PL3H05tvUAhk=/fit-in/900x600/filters:no_upscale():strip_icc()/pic2419375.jpg',
    rating: 7.2,
    playerCount: '3-4',
    status: 'published',
  },
  {
    id: '2',
    title: 'Wingspan',
    publisher: 'Stonemaier Games',
    imageUrl: 'https://cf.geekdo-images.com/yLZJCVLlIx4c7eJEWUNJ7w__imagepage/img/VNToqgS2-pG8VAPGRKjKSFxuRBY=/fit-in/900x600/filters:no_upscale():strip_icc()/pic4458123.jpg',
    rating: 8.1,
    playerCount: '1-5',
    status: 'published',
  },
  {
    id: '3',
    title: 'Azul',
    publisher: 'Plan B Games',
    imageUrl: 'https://cf.geekdo-images.com/aPSHJO0d0XOpQR5X-wJonw__imagepage/img/q4uWd2nXGeEkKDR7OaunhqWsVto=/fit-in/900x600/filters:no_upscale():strip_icc()/pic6973671.png',
    rating: 7.8,
    playerCount: '2-4',
    status: 'published',
  },
  {
    id: '4',
    title: 'Ticket to Ride',
    publisher: 'Days of Wonder',
    imageUrl: 'https://cf.geekdo-images.com/ZWJg0dCdrWHxVnc0eFXK8w__imagepage/img/amgwj5qkc0RuxaT6clWIDbJw-kQ=/fit-in/900x600/filters:no_upscale():strip_icc()/pic66668.jpg',
    rating: 7.4,
    playerCount: '2-5',
    status: 'published',
  },
  {
    id: '5',
    title: '7 Wonders',
    publisher: 'Repos Production',
    imageUrl: 'https://cf.geekdo-images.com/35h9Za_JvMMMtx_92kT0Jg__imagepage/img/gjk3QJmge3w0CiKJ8nqz8KLJUjI=/fit-in/900x600/filters:no_upscale():strip_icc()/pic7149798.jpg',
    rating: 7.7,
    playerCount: '2-7',
    status: 'published',
  },
  {
    id: '6',
    title: 'Pandemic',
    publisher: 'Z-Man Games',
    imageUrl: 'https://cf.geekdo-images.com/S3ybV1LAp-8SnHIXLLjVqA__imagepage/img/kIBu-2Ljb_ml5n-S8uIbE6ehGFc=/fit-in/900x600/filters:no_upscale():strip_icc()/pic1534148.jpg',
    rating: 7.6,
    playerCount: '2-4',
    status: 'published',
  },
  {
    id: '7',
    title: 'Splendor',
    publisher: 'Space Cowboys',
    imageUrl: 'https://cf.geekdo-images.com/rwOMxx4q5yuElIvo-1-OFw__imagepage/img/vDN3N-3q3Z3tULl-1z_C_Q=/fit-in/900x600/filters:no_upscale():strip_icc()/pic1904079.jpg',
    rating: 7.5,
    playerCount: '2-4',
    status: 'draft',
  },
  {
    id: '8',
    title: 'Scythe',
    publisher: 'Stonemaier Games',
    imageUrl: 'https://cf.geekdo-images.com/7k_nOxpO9OGIjhLq2BUZdA__imagepage/img/iqB2ydL8Al11ulX8MXITD69Aw1c=/fit-in/900x600/filters:no_upscale():strip_icc()/pic3163924.jpg',
    rating: 8.2,
    playerCount: '1-5',
    status: 'published',
  },
];

const statusColors = {
  published: 'bg-green-100 text-green-900 dark:bg-green-900/30 dark:text-green-300',
  draft: 'bg-amber-100 text-amber-900 dark:bg-amber-900/30 dark:text-amber-300',
  archived: 'bg-gray-100 text-gray-900 dark:bg-gray-900/30 dark:text-gray-300',
};

export function GameCatalogGrid() {
  return (
    <div className="space-y-6">
      {/* Stats Summary */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-slate-200/50 dark:border-zinc-700/50">
          <div className="text-sm text-slate-600 dark:text-zinc-400">Total Games</div>
          <div className="text-2xl font-bold text-slate-900 dark:text-zinc-100">{MOCK_GAMES.length}</div>
        </div>
        <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-slate-200/50 dark:border-zinc-700/50">
          <div className="text-sm text-slate-600 dark:text-zinc-400">Published</div>
          <div className="text-2xl font-bold text-green-600 dark:text-green-400">
            {MOCK_GAMES.filter((g) => g.status === 'published').length}
          </div>
        </div>
        <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-slate-200/50 dark:border-zinc-700/50">
          <div className="text-sm text-slate-600 dark:text-zinc-400">Draft</div>
          <div className="text-2xl font-bold text-amber-600 dark:text-amber-400">
            {MOCK_GAMES.filter((g) => g.status === 'draft').length}
          </div>
        </div>
      </div>

      {/* Game Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {MOCK_GAMES.map((game) => (
          <div key={game.id} className="relative">
            <MeepleCard
              entity="game"
              variant="grid"
              title={game.title}
              subtitle={game.publisher}
              imageUrl={game.imageUrl}
              rating={game.rating}
              ratingMax={10}
              metadata={[
                { label: 'Players', value: game.playerCount },
              ]}
            />
            <div className="absolute top-3 right-3">
              <Badge variant="outline" className={statusColors[game.status]}>
                {game.status.charAt(0).toUpperCase() + game.status.slice(1)}
              </Badge>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
