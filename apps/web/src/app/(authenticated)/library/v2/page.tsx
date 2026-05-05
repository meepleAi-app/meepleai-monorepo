import { LibraryV2Client, type LibraryV2Item } from './LibraryV2Client';

import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Libreria (v2)',
};

const SEED: ReadonlyArray<LibraryV2Item> = [
  {
    id: 'catan',
    title: 'Catan',
    publisher: 'Kosmos',
    owned: true,
    wishlist: false,
    description: 'Il classico di Klaus Teuber: commercio, costruzione e risorse.',
    minPlayers: 3,
    maxPlayers: 4,
    playTimeMinutes: 75,
    sessionCount: 5,
    chatCount: 2,
  },
  {
    id: 'root',
    title: 'Root',
    publisher: 'Leder Games',
    owned: false,
    wishlist: true,
    description: 'Gioco di guerra asimmetrico ambientato nella foresta.',
    minPlayers: 2,
    maxPlayers: 4,
    playTimeMinutes: 90,
    sessionCount: 0,
    chatCount: 0,
  },
];

export default function LibraryV2Page(): React.JSX.Element {
  return (
    <div className="h-full p-4">
      <LibraryV2Client items={SEED} />
    </div>
  );
}
