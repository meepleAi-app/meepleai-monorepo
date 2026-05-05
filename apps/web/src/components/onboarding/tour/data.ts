export interface TourGame {
  readonly id: string;
  readonly title: string;
  readonly year: number;
  readonly players: string;
  readonly emoji: string;
  /** HSL gradient stops used for the tile cover (["h s% l%", "h s% l%"]). */
  readonly gradient: readonly [string, string];
}

export interface TourAgent {
  readonly id: string;
  readonly emoji: string;
  readonly name: string;
  readonly desc: string;
  readonly defaultOn: boolean;
}

export type TourActionEntity = 'event' | 'game' | 'agent';

export interface TourAction {
  readonly id: string;
  readonly emoji: string;
  readonly title: string;
  readonly desc: string;
  readonly entity: TourActionEntity;
  readonly href: string;
}

export const GAMES: readonly TourGame[] = [
  {
    id: 'catan',
    title: 'Catan',
    year: 1995,
    players: '3–4',
    emoji: '🌾',
    gradient: ['25 88% 50%', '15 80% 38%'],
  },
  {
    id: 'carcassonne',
    title: 'Carcassonne',
    year: 2000,
    players: '2–5',
    emoji: '🏰',
    gradient: ['142 52% 38%', '158 50% 28%'],
  },
  {
    id: 'ticket',
    title: 'Ticket to Ride',
    year: 2004,
    players: '2–5',
    emoji: '🚂',
    gradient: ['220 68% 50%', '235 62% 40%'],
  },
  {
    id: 'wingspan',
    title: 'Wingspan',
    year: 2019,
    players: '1–5',
    emoji: '🦜',
    gradient: ['262 65% 55%', '278 60% 42%'],
  },
  {
    id: '7wonders',
    title: '7 Wonders',
    year: 2010,
    players: '2–7',
    emoji: '🏛️',
    gradient: ['38 85% 50%', '22 78% 40%'],
  },
  {
    id: 'terraforming',
    title: 'Terraforming Mars',
    year: 2016,
    players: '1–5',
    emoji: '🚀',
    gradient: ['350 72% 52%', '8 65% 40%'],
  },
  {
    id: 'azul',
    title: 'Azul',
    year: 2017,
    players: '2–4',
    emoji: '🔷',
    gradient: ['195 78% 48%', '210 68% 38%'],
  },
  {
    id: 'splendor',
    title: 'Splendor',
    year: 2014,
    players: '2–4',
    emoji: '💎',
    gradient: ['174 62% 38%', '188 56% 28%'],
  },
] as const;

export const AGENTS: readonly TourAgent[] = [
  {
    id: 'rules',
    emoji: '🎲',
    name: 'Agente Regole',
    desc: 'Risposte precise con citazione pagina PDF',
    defaultOn: true,
  },
  {
    id: 'strategy',
    emoji: '🎯',
    name: 'Agente Strategia',
    desc: 'Consigli tattici durante la partita',
    defaultOn: true,
  },
  {
    id: 'setup',
    emoji: '🔧',
    name: 'Agente Setup',
    desc: 'Ti guida nel preparare il tavolo',
    defaultOn: true,
  },
  {
    id: 'narrator',
    emoji: '📚',
    name: 'Agente Cronista',
    desc: 'Narra la partita in tempo reale',
    defaultOn: false,
  },
] as const;

export const ACTIONS: readonly TourAction[] = [
  {
    id: 'event',
    emoji: '🎉',
    title: 'Crea la prima serata',
    desc: 'Pianifica con amici',
    entity: 'event',
    href: '/game-nights',
  },
  {
    id: 'library',
    emoji: '🎲',
    title: 'Esplora la library',
    desc: 'Vedi i tuoi giochi',
    entity: 'game',
    href: '/library',
  },
  {
    id: 'chat',
    emoji: '💬',
    title: 'Chatta con un agente',
    desc: 'Prova una domanda',
    entity: 'agent',
    href: '/agents',
  },
] as const;

export const MIN_SELECTED = 3;
export const STEP_LABELS: readonly ['Giochi', 'Agenti', 'Sessione'] = [
  'Giochi',
  'Agenti',
  'Sessione',
];
export const STEP_ENTITIES: readonly ['game', 'agent', 'session'] = ['game', 'agent', 'session'];
