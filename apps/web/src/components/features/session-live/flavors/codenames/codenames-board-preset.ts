import {
  CODENAMES_KEY_COUNTS,
  type CodenamesCell,
  type CodenamesKey,
  type CodenamesTeam,
} from './codenames-state';

// A static Italian-leaning word bank (≥ 50 distinct single words). Fixed const, not i18n.
export const CODENAMES_WORD_BANK: ReadonlyArray<string> = [
  'MARE',
  'MONTE',
  'SOLE',
  'LUNA',
  'STELLA',
  'FIUME',
  'BOSCO',
  'CASTELLO',
  'PONTE',
  'CHIAVE',
  'DRAGO',
  'REGINA',
  'CAVALIERE',
  'SCUDO',
  'SPADA',
  'CORONA',
  'TESORO',
  'NAVE',
  'FARO',
  'ISOLA',
  'DESERTO',
  'PIRAMIDE',
  'FUOCO',
  'GHIACCIO',
  'VENTO',
  'TEMPESTA',
  'ORO',
  'ARGENTO',
  'FERRO',
  'PIETRA',
  'GATTO',
  'CANE',
  'LUPO',
  'VOLPE',
  'AQUILA',
  'SERPENTE',
  'RAGNO',
  'APE',
  'PESCE',
  'BALENA',
  'MELA',
  'PANE',
  'VINO',
  'MIELE',
  'SALE',
  'PEPE',
  'ZUCCHERO',
  'CAFFE',
  'LATTE',
  'FORMAGGIO',
  'ROBOT',
  'RAZZO',
  'PIANETA',
  'GALASSIA',
  'COMETA',
  'MOTORE',
  'CIRCUITO',
  'CODICE',
  'RETE',
  'SCHERMO',
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

export function generateCodenamesBoard(startingTeam?: CodenamesTeam): {
  board: CodenamesCell[];
  startingTeam: CodenamesTeam;
} {
  const start: CodenamesTeam = startingTeam ?? (Math.random() < 0.5 ? 'red' : 'blue');
  const other: CodenamesTeam = start === 'red' ? 'blue' : 'red';

  const keys: CodenamesKey[] = [
    ...Array<CodenamesKey>(CODENAMES_KEY_COUNTS.starting).fill(start),
    ...Array<CodenamesKey>(CODENAMES_KEY_COUNTS.other).fill(other),
    ...Array<CodenamesKey>(CODENAMES_KEY_COUNTS.neutral).fill('neutral'),
    ...Array<CodenamesKey>(CODENAMES_KEY_COUNTS.assassin).fill('assassin'),
  ];

  const words = shuffle(CODENAMES_WORD_BANK).slice(0, 25);
  const shuffledKeys = shuffle(keys);

  const board: CodenamesCell[] = words.map((word, i) => ({
    word,
    key: shuffledKeys[i],
    revealed: false,
  }));

  return { board, startingTeam: start };
}
