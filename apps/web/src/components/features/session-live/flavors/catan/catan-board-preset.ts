import type { CatanHex, CatanPort, CatanTerrain } from './catan-state';

const COL_HEIGHTS = [3, 4, 5, 4, 3] as const;

// Standard base-game terrain multiset (19 tiles).
const TERRAINS: CatanTerrain[] = [
  'wood',
  'wood',
  'wood',
  'wood',
  'sheep',
  'sheep',
  'sheep',
  'sheep',
  'wheat',
  'wheat',
  'wheat',
  'wheat',
  'brick',
  'brick',
  'brick',
  'ore',
  'ore',
  'ore',
  'desert',
];

// Standard number-token set (18 tokens; no 7). One per non-desert tile.
const NUMBER_TOKENS = [2, 3, 3, 4, 4, 5, 5, 6, 6, 8, 8, 9, 9, 10, 10, 11, 11, 12];

// Fixed coastal port layout (9 ports). hexId anchors are POSITIONAL (perimeter
// tiles are the same regardless of the shuffled terrain), so they always exist.
const PORTS: CatanPort[] = [
  { hexId: 'h0', edge: 4, type: 'generic', ratio: '3:1' },
  { hexId: 'h1', edge: 3, type: 'sheep', ratio: '2:1' },
  { hexId: 'h3', edge: 5, type: 'wheat', ratio: '2:1' },
  { hexId: 'h7', edge: 0, type: 'generic', ratio: '3:1' },
  { hexId: 'h12', edge: 0, type: 'ore', ratio: '2:1' },
  { hexId: 'h16', edge: 0, type: 'wood', ratio: '2:1' },
  { hexId: 'h18', edge: 1, type: 'generic', ratio: '3:1' },
  { hexId: 'h11', edge: 2, type: 'brick', ratio: '2:1' },
  { hexId: 'h15', edge: 1, type: 'generic', ratio: '3:1' },
];

function shuffle<T>(input: readonly T[]): T[] {
  const a = [...input];
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    const tmp = a[i];
    a[i] = a[j];
    a[j] = tmp;
  }
  return a;
}

export function generateStandardBoard(): {
  hexes: CatanHex[];
  robberHexId: string;
  ports: CatanPort[];
} {
  const terrains = shuffle(TERRAINS);
  const numbers = shuffle(NUMBER_TOKENS);
  const hexes: CatanHex[] = [];
  let idx = 0;
  let numIdx = 0;
  let robberHexId = 'h0';

  for (let col = 0; col < COL_HEIGHTS.length; col++) {
    for (let row = 0; row < COL_HEIGHTS[col]; row++) {
      const terrain = terrains[idx];
      const id = `h${idx}`;
      const number = terrain === 'desert' ? null : numbers[numIdx++];
      if (terrain === 'desert') robberHexId = id;
      hexes.push({ id, col, row, terrain, number });
      idx++;
    }
  }

  return { hexes, robberHexId, ports: PORTS };
}
