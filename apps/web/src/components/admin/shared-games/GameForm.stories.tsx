/**
 * GameForm - Issue #2372
 *
 * Reusable form for creating and editing shared games.
 * Features:
 * - Create mode (empty form)
 * - Edit mode (pre-populated)
 * - Validation states
 * - Loading states
 * - Category/mechanic selection
 * - Rules editor toggle
 */

import { fn } from 'storybook/test';

import type { SharedGameDetail } from '@/lib/api';

import { GameForm } from './GameForm';

import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'Admin/SharedGames/GameForm',
  component: GameForm,
  parameters: {
    layout: 'padded',
    chromatic: {
      viewports: [768, 1024, 1280],
      delay: 500,
    },
  },
  tags: ['autodocs'],
  decorators: [
    Story => (
      <div className="max-w-4xl mx-auto p-6">
        <Story />
      </div>
    ),
  ],
  argTypes: {
    game: { control: 'object' },
    isLoading: { control: 'boolean' },
    onSubmit: { action: 'submitted' },
    onCancel: { action: 'cancelled' },
  },
  args: {
    onSubmit: fn(),
    onCancel: fn(),
    isLoading: false,
  },
} satisfies Meta<typeof GameForm>;

export default meta;
type Story = StoryObj<typeof meta>;

// Mock game data for edit mode
const mockGame: SharedGameDetail = {
  id: '550e8400-e29b-41d4-a716-446655440000',
  title: 'I Coloni di Catan',
  yearPublished: 1995,
  description:
    "I Coloni di Catan è un classico gioco da tavolo di strategia e commercio. I giocatori devono raccogliere risorse, costruire strade e insediamenti, e commerciare con gli altri giocatori per diventare il dominatore dell'isola di Catan.",
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 75,
  minAge: 10,
  complexityRating: 2.3,
  averageRating: 7.1,
  imageUrl:
    'https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__imagepage/img/M_3Vg1j2HlNgkv7PL2xl2BJE2bw=/fit-in/900x600/filters:no_upscale():strip_icc()/pic2419375.jpg',
  thumbnailUrl:
    'https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__thumb/img/7a7S0c3Sdd4dWKpbYzTkf1E96sw=/fit-in/200x150/filters:strip_icc()/pic2419375.jpg',
  bggId: 13,
  status: 1,
  categories: [
    { id: '1', name: 'Strategia' },
    { id: '2', name: 'Negoziazione' },
  ],
  mechanics: [
    { id: '1', name: 'Dice Rolling' },
    { id: '2', name: 'Trading' },
    { id: '3', name: 'Route Building' },
  ],
  rules: null,
  createdAt: '2024-01-15T10:00:00Z',
  updatedAt: '2024-06-20T14:30:00Z',
};

const mockGameWithRules: SharedGameDetail = {
  ...mockGame,
  rules: {
    id: '1',
    gameId: mockGame.id,
    content: `# Regole di Catan

## Preparazione
1. Disponi i tasselli esagonali per formare l'isola
2. Ogni giocatore sceglie un colore e riceve le relative pedine
3. Disponi le tessere numerate sui tasselli risorse

## Turno di gioco
1. Lancia i dadi per determinare la produzione di risorse
2. Commercia con gli altri giocatori o con la banca
3. Costruisci strade, insediamenti o città
4. Pesca o gioca carte sviluppo

## Vittoria
Il primo giocatore a raggiungere 10 punti vittoria vince la partita!`,
    language: 'it',
    version: '1.0',
    createdAt: '2024-01-15T10:00:00Z',
    updatedAt: '2024-01-15T10:00:00Z',
  },
};

/**
 * Create Mode - Empty form for new game
 */
export const CreateMode: Story = {
  args: {
    game: null,
  },
};

/**
 * Edit Mode - Pre-populated form with existing game
 */
export const EditMode: Story = {
  args: {
    game: mockGame,
  },
};

/**
 * Edit Mode with Rules - Game that has existing rules content
 */
export const EditModeWithRules: Story = {
  args: {
    game: mockGameWithRules,
  },
};

/**
 * Loading State - Form in loading state (disabled inputs)
 */
export const LoadingState: Story = {
  args: {
    game: null,
    isLoading: true,
  },
};

/**
 * Edit Loading - Edit mode with loading state
 */
export const EditLoadingState: Story = {
  args: {
    game: mockGame,
    isLoading: true,
  },
};

/**
 * Simple Game - Game with minimal data (no ratings, no BGG ID)
 */
export const SimpleGame: Story = {
  args: {
    game: {
      ...mockGame,
      id: '550e8400-e29b-41d4-a716-446655440001',
      title: 'Uno',
      yearPublished: 1971,
      description: 'Gioco di carte classico per tutta la famiglia.',
      minPlayers: 2,
      maxPlayers: 10,
      playingTimeMinutes: 30,
      minAge: 7,
      complexityRating: null,
      averageRating: null,
      bggId: null,
      categories: [{ id: '3', name: 'Famiglia' }],
      mechanics: [{ id: '4', name: 'Hand Management' }],
    },
  },
};

/**
 * Complex Game - Heavy strategy game with high complexity
 */
export const ComplexGame: Story = {
  args: {
    game: {
      ...mockGame,
      id: '550e8400-e29b-41d4-a716-446655440002',
      title: 'Twilight Imperium (Quarta Edizione)',
      yearPublished: 2017,
      description:
        'Twilight Imperium è un epico gioco di conquista galattica. I giocatori rappresentano fazioni aliene che lottano per il controllo della galassia attraverso guerra, diplomazia, commercio e ricerca tecnologica.',
      minPlayers: 3,
      maxPlayers: 6,
      playingTimeMinutes: 480,
      minAge: 14,
      complexityRating: 4.2,
      averageRating: 8.6,
      bggId: 233078,
      categories: [
        { id: '1', name: 'Strategia' },
        { id: '4', name: 'Fantascienza' },
        { id: '5', name: 'Wargame' },
      ],
      mechanics: [
        { id: '5', name: 'Area Control' },
        { id: '6', name: 'Variable Player Powers' },
        { id: '7', name: 'Tech Trees' },
        { id: '8', name: 'Voting' },
      ],
    },
  },
};

/**
 * Party Game - Light party game with many players
 */
export const PartyGame: Story = {
  args: {
    game: {
      ...mockGame,
      id: '550e8400-e29b-41d4-a716-446655440003',
      title: 'Codenames',
      yearPublished: 2015,
      description:
        'Un gioco di parole a squadre dove gli spymaster devono dare indizi alla loro squadra per indovinare le parole corrette sul tabellone.',
      minPlayers: 2,
      maxPlayers: 8,
      playingTimeMinutes: 15,
      minAge: 14,
      complexityRating: 1.3,
      averageRating: 7.6,
      bggId: 178900,
      categories: [
        { id: '6', name: 'Party' },
        { id: '7', name: 'Parole' },
      ],
      mechanics: [
        { id: '9', name: 'Team-Based' },
        { id: '10', name: 'Memory' },
      ],
    },
  },
};

/**
 * Long Description - Game with very long description text
 */
export const LongDescription: Story = {
  args: {
    game: {
      ...mockGame,
      description: `Gloomhaven è un gioco da tavolo dungeon crawler cooperativo. In un mondo di incertezza e oscurità, i giocatori assumeranno il ruolo di un mercenario vagabondo con le proprie ragioni per viaggiare in questo angolo oscuro del mondo.

I giocatori devono lavorare insieme attraverso centinaia di scenari unici, combattendo mostri, scoprendo tesori e affrontando decisioni morali che influenzeranno il destino del mondo.

## Caratteristiche principali:
- Sistema di combattimento tattico basato su carte
- Campagna narrativa con oltre 95 scenari
- 17 classi di personaggi sbloccabili
- Sistema legacy con adesivi permanenti
- Centinaia di oggetti e abilità da scoprire

Il gioco utilizza un innovativo sistema di gestione delle carte dove ogni personaggio ha un mazzo unico di abilità. Durante ogni round, i giocatori scelgono due carte dalla loro mano, usando la metà superiore di una e la metà inferiore dell'altra. Questo crea un puzzle tattico profondo e gratificante.`,
    },
  },
};

/**
 * All form sections visible - Comprehensive view
 */
export const AllSectionsVisible: Story = {
  args: {
    game: mockGameWithRules,
  },
  parameters: {
    chromatic: {
      delay: 1000,
    },
  },
};
