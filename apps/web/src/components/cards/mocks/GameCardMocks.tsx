'use client';

/**
 * Game Card Mock Designs - 5 Distinct Styles with 3 Variants Each
 *
 * Design Directions:
 * 1. Classic TCG - Ornate borders, gold accents, fantasy aesthetic
 * 2. Modern Minimalist - Clean lines, subtle card game elements
 * 3. Illustrated Playful - Hand-drawn, Exploding Kittens vibe
 * 4. Retro Arcade - Pixel art inspired, 8-bit nostalgia
 * 5. Premium Holographic - Foil effects, collector's edition feel
 *
 * Each has variants: A (Owned), B (Wishlist), C (New)
 */

import React, { useState } from 'react';

import { motion } from 'framer-motion';

// ============================================================================
// Types
// ============================================================================

type CardState = 'owned' | 'wishlist' | 'new';

interface GameCardData {
  id: string;
  title: string;
  publisher: string;
  imageUrl: string;
  playerCount: { min: number; max: number };
  playTime: { min: number; max: number };
  rating: number;
  complexity: number;
  description: string;
  categories: string[];
  mechanics: string[];
  personalNotes?: string;
  plays: number;
  wins: number;
  yearPublished: number;
}

interface GameCardProps {
  game: GameCardData;
  state: CardState;
  onFlip?: () => void;
}

// ============================================================================
// Sample Data
// ============================================================================

const sampleGame: GameCardData = {
  id: '1',
  title: 'Azul',
  publisher: 'Plan B Games',
  imageUrl: '/placeholder-game.jpg',
  playerCount: { min: 2, max: 4 },
  playTime: { min: 30, max: 45 },
  rating: 8.2,
  complexity: 2.5,
  description: 'Azul was designed by the world famous, award-winning game author Michael Kiesling. Players compete as tile-laying artists creating a mosaic.',
  categories: ['Abstract Strategy', 'Puzzle'],
  mechanics: ['Pattern Building', 'Tile Placement', 'Set Collection'],
  personalNotes: 'Great with family! Sarah always wins 😅',
  plays: 24,
  wins: 8,
  yearPublished: 2017,
};

// ============================================================================
// Design 1: Classic TCG Style
// ============================================================================

const ClassicTCGCard: React.FC<GameCardProps> = ({ game, state }) => {
  const [isFlipped, setIsFlipped] = useState(false);

  const stateColors = {
    owned: {
      border: 'from-amber-600 via-yellow-400 to-amber-600',
      accent: 'text-amber-400',
      glow: 'shadow-amber-500/30',
      badge: 'bg-amber-500',
    },
    wishlist: {
      border: 'from-purple-600 via-violet-400 to-purple-600',
      accent: 'text-violet-400',
      glow: 'shadow-violet-500/30',
      badge: 'bg-violet-500',
    },
    new: {
      border: 'from-emerald-600 via-green-400 to-emerald-600',
      accent: 'text-emerald-400',
      glow: 'shadow-emerald-500/30',
      badge: 'bg-emerald-500',
    },
  };

  const colors = stateColors[state];

  return (
    <div className="w-[280px] h-[400px]" style={{ perspective: '1000px' }}>
      <motion.div
        className="relative w-full h-full cursor-pointer"
        style={{ transformStyle: 'preserve-3d' }}
        animate={{ rotateY: isFlipped ? 180 : 0 }}
        transition={{ duration: 0.6, ease: [0.4, 0, 0.2, 1] }}
        onClick={() => setIsFlipped(!isFlipped)}
      >
        {/* Front */}
        <div
          className={`absolute inset-0 rounded-xl bg-gradient-to-b ${colors.border} p-[3px] shadow-2xl ${colors.glow}`}
          style={{ backfaceVisibility: 'hidden' }}
        >
          <div className="h-full rounded-lg bg-gradient-to-b from-slate-900 via-slate-800 to-slate-900 p-3 relative overflow-hidden">
            {/* Ornate corner decorations */}
            <div className="absolute top-0 left-0 w-8 h-8 border-t-2 border-l-2 border-amber-500/50 rounded-tl-lg" />
            <div className="absolute top-0 right-0 w-8 h-8 border-t-2 border-r-2 border-amber-500/50 rounded-tr-lg" />
            <div className="absolute bottom-0 left-0 w-8 h-8 border-b-2 border-l-2 border-amber-500/50 rounded-bl-lg" />
            <div className="absolute bottom-0 right-0 w-8 h-8 border-b-2 border-r-2 border-amber-500/50 rounded-br-lg" />

            {/* State Badge */}
            <div className={`absolute top-4 right-4 ${colors.badge} px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider text-white z-10`}>
              {state}
            </div>

            {/* Image Frame */}
            <div className="relative h-[180px] rounded-lg overflow-hidden border-2 border-amber-600/30 mt-2">
              <div className="absolute inset-0 bg-gradient-to-br from-slate-700 to-slate-800 flex items-center justify-center">
                <span className="text-6xl">🎲</span>
              </div>
              {/* Vignette */}
              <div className="absolute inset-0 bg-gradient-to-t from-slate-900/60 to-transparent" />
            </div>

            {/* Title Bar */}
            <div className="mt-3 px-1">
              <div className="bg-gradient-to-r from-transparent via-amber-600/20 to-transparent h-px mb-2" />
              <h3 className="font-serif text-xl font-bold text-amber-100 text-center tracking-wide">
                {game.title}
              </h3>
              <p className={`text-xs ${colors.accent} text-center font-medium`}>
                {game.publisher} • {game.yearPublished}
              </p>
              <div className="bg-gradient-to-r from-transparent via-amber-600/20 to-transparent h-px mt-2" />
            </div>

            {/* Stats Grid */}
            <div className="grid grid-cols-2 gap-2 mt-3 text-xs">
              <div className="bg-slate-800/50 rounded px-2 py-1.5 border border-slate-700/50">
                <span className="text-slate-400">Players</span>
                <span className={`float-right font-bold ${colors.accent}`}>
                  {game.playerCount.min}-{game.playerCount.max}
                </span>
              </div>
              <div className="bg-slate-800/50 rounded px-2 py-1.5 border border-slate-700/50">
                <span className="text-slate-400">Time</span>
                <span className={`float-right font-bold ${colors.accent}`}>
                  {game.playTime.min}-{game.playTime.max}m
                </span>
              </div>
            </div>

            {/* Rating */}
            <div className="mt-3 flex justify-between items-center px-1">
              <div className="flex items-center gap-1">
                {[...Array(5)].map((_, i) => (
                  <span
                    key={i}
                    className={`text-lg ${i < Math.floor(game.rating / 2) ? 'text-amber-400' : 'text-slate-600'}`}
                  >
                    ★
                  </span>
                ))}
              </div>
              <span className={`font-bold text-lg ${colors.accent}`}>{game.rating.toFixed(1)}</span>
            </div>

            {/* Complexity Bar */}
            <div className="mt-2 px-1">
              <div className="flex justify-between text-[10px] text-slate-400 mb-1">
                <span>Complexity</span>
                <span>{game.complexity.toFixed(1)}/5</span>
              </div>
              <div className="h-1.5 bg-slate-700 rounded-full overflow-hidden">
                <motion.div
                  className={`h-full bg-gradient-to-r ${colors.border}`}
                  initial={{ width: 0 }}
                  animate={{ width: `${(game.complexity / 5) * 100}%` }}
                  transition={{ delay: 0.3, duration: 0.5 }}
                />
              </div>
            </div>

            {/* Flip Hint */}
            <p className="absolute bottom-2 left-1/2 -translate-x-1/2 text-[9px] text-slate-500">
              TAP TO FLIP
            </p>
          </div>
        </div>

        {/* Back */}
        <div
          className={`absolute inset-0 rounded-xl bg-gradient-to-b ${colors.border} p-[3px] shadow-2xl ${colors.glow}`}
          style={{ backfaceVisibility: 'hidden', transform: 'rotateY(180deg)' }}
        >
          <div className="h-full rounded-lg bg-gradient-to-b from-slate-900 via-slate-800 to-slate-900 p-4 relative overflow-hidden">
            {/* Decorative Pattern */}
            <div className="absolute inset-0 opacity-5">
              <div className="absolute inset-0" style={{
                backgroundImage: `url("data:image/svg+xml,%3Csvg width='60' height='60' viewBox='0 0 60 60' xmlns='http://www.w3.org/2000/svg'%3E%3Cpath d='M30 0L60 30L30 60L0 30z' fill='%23fbbf24' fill-opacity='0.4'/%3E%3C/svg%3E")`,
                backgroundSize: '30px 30px'
              }} />
            </div>

            <h4 className="font-serif text-lg font-bold text-amber-100 mb-2">{game.title}</h4>

            <p className="text-xs text-slate-300 leading-relaxed mb-3 line-clamp-3">
              {game.description}
            </p>

            {/* Categories */}
            <div className="mb-3">
              <span className="text-[10px] text-slate-400 uppercase tracking-wider">Categories</span>
              <div className="flex flex-wrap gap-1 mt-1">
                {game.categories.map((cat) => (
                  <span key={cat} className="text-[10px] bg-amber-600/20 text-amber-300 px-2 py-0.5 rounded">
                    {cat}
                  </span>
                ))}
              </div>
            </div>

            {/* Mechanics */}
            <div className="mb-3">
              <span className="text-[10px] text-slate-400 uppercase tracking-wider">Mechanics</span>
              <div className="flex flex-wrap gap-1 mt-1">
                {game.mechanics.map((mech) => (
                  <span key={mech} className="text-[10px] bg-slate-700/50 text-slate-300 px-2 py-0.5 rounded">
                    {mech}
                  </span>
                ))}
              </div>
            </div>

            {/* Personal Stats */}
            <div className="bg-slate-800/50 rounded-lg p-2 border border-slate-700/50">
              <div className="grid grid-cols-2 gap-2 text-center">
                <div>
                  <span className={`text-xl font-bold ${colors.accent}`}>{game.plays}</span>
                  <p className="text-[10px] text-slate-400">Plays</p>
                </div>
                <div>
                  <span className={`text-xl font-bold ${colors.accent}`}>{game.wins}</span>
                  <p className="text-[10px] text-slate-400">Wins</p>
                </div>
              </div>
            </div>

            {/* Notes */}
            {game.personalNotes && (
              <div className="mt-2 p-2 bg-slate-800/30 rounded border-l-2 border-amber-500/50">
                <p className="text-[10px] text-slate-300 italic">"{game.personalNotes}"</p>
              </div>
            )}
          </div>
        </div>
      </motion.div>
    </div>
  );
};

// ============================================================================
// Design 2: Modern Minimalist
// ============================================================================

const ModernMinimalCard: React.FC<GameCardProps> = ({ game, state }) => {
  const [isFlipped, setIsFlipped] = useState(false);

  const stateStyles = {
    owned: {
      accent: '#10b981',
      bg: 'bg-emerald-50',
      text: 'text-emerald-600',
      border: 'border-emerald-200',
    },
    wishlist: {
      accent: '#f59e0b',
      bg: 'bg-amber-50',
      text: 'text-amber-600',
      border: 'border-amber-200',
    },
    new: {
      accent: '#3b82f6',
      bg: 'bg-blue-50',
      text: 'text-blue-600',
      border: 'border-blue-200',
    },
  };

  const style = stateStyles[state];

  return (
    <div className="w-[280px] h-[400px]" style={{ perspective: '1000px' }}>
      <motion.div
        className="relative w-full h-full cursor-pointer"
        style={{ transformStyle: 'preserve-3d' }}
        animate={{ rotateY: isFlipped ? 180 : 0 }}
        transition={{ duration: 0.5, ease: 'easeInOut' }}
        onClick={() => setIsFlipped(!isFlipped)}
      >
        {/* Front */}
        <div
          className={`absolute inset-0 rounded-2xl bg-white border ${style.border} shadow-lg overflow-hidden`}
          style={{ backfaceVisibility: 'hidden' }}
        >
          {/* Top accent line */}
          <div className="h-1 w-full" style={{ backgroundColor: style.accent }} />

          {/* Image */}
          <div className="h-[200px] bg-gradient-to-br from-slate-100 to-slate-200 flex items-center justify-center relative">
            <span className="text-7xl opacity-80">🎲</span>

            {/* State indicator */}
            <div className={`absolute top-3 left-3 ${style.bg} ${style.text} px-2 py-1 rounded-full text-[10px] font-semibold uppercase tracking-wider`}>
              {state}
            </div>
          </div>

          {/* Content */}
          <div className="p-5">
            <h3 className="font-sans text-xl font-semibold text-slate-800 tracking-tight">
              {game.title}
            </h3>
            <p className="text-sm text-slate-500 mt-0.5">
              {game.publisher}
            </p>

            {/* Stats Row */}
            <div className="flex items-center gap-4 mt-4 text-sm text-slate-600">
              <div className="flex items-center gap-1.5">
                <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
                </svg>
                <span>{game.playerCount.min}-{game.playerCount.max}</span>
              </div>
              <div className="flex items-center gap-1.5">
                <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                <span>{game.playTime.min}-{game.playTime.max}m</span>
              </div>
            </div>

            {/* Rating */}
            <div className="flex items-center justify-between mt-4 pt-4 border-t border-slate-100">
              <div className="flex items-center gap-2">
                <span className="text-2xl font-bold" style={{ color: style.accent }}>
                  {game.rating.toFixed(1)}
                </span>
                <span className="text-xs text-slate-400">BGG</span>
              </div>

              {/* Complexity dots */}
              <div className="flex items-center gap-1">
                {[...Array(5)].map((_, i) => (
                  <div
                    key={i}
                    className="w-2 h-2 rounded-full"
                    style={{
                      backgroundColor: i < Math.round(game.complexity) ? style.accent : '#e2e8f0'
                    }}
                  />
                ))}
              </div>
            </div>
          </div>
        </div>

        {/* Back */}
        <div
          className={`absolute inset-0 rounded-2xl bg-white border ${style.border} shadow-lg overflow-hidden`}
          style={{ backfaceVisibility: 'hidden', transform: 'rotateY(180deg)' }}
        >
          <div className="h-1 w-full" style={{ backgroundColor: style.accent }} />

          <div className="p-5 h-[calc(100%-4px)] flex flex-col">
            <h4 className="font-sans text-lg font-semibold text-slate-800">{game.title}</h4>

            <p className="text-sm text-slate-600 mt-2 leading-relaxed line-clamp-3">
              {game.description}
            </p>

            {/* Tags */}
            <div className="mt-4">
              <p className="text-xs font-medium text-slate-400 uppercase tracking-wider mb-2">Categories</p>
              <div className="flex flex-wrap gap-1.5">
                {game.categories.map((cat) => (
                  <span
                    key={cat}
                    className={`text-xs px-2 py-1 rounded-full ${style.bg} ${style.text}`}
                  >
                    {cat}
                  </span>
                ))}
              </div>
            </div>

            <div className="mt-3">
              <p className="text-xs font-medium text-slate-400 uppercase tracking-wider mb-2">Mechanics</p>
              <div className="flex flex-wrap gap-1.5">
                {game.mechanics.slice(0, 3).map((mech) => (
                  <span
                    key={mech}
                    className="text-xs px-2 py-1 rounded-full bg-slate-100 text-slate-600"
                  >
                    {mech}
                  </span>
                ))}
              </div>
            </div>

            {/* Stats */}
            <div className="mt-auto pt-4 border-t border-slate-100">
              <div className="grid grid-cols-2 gap-4">
                <div className="text-center">
                  <span className="text-2xl font-bold" style={{ color: style.accent }}>{game.plays}</span>
                  <p className="text-xs text-slate-400">Total Plays</p>
                </div>
                <div className="text-center">
                  <span className="text-2xl font-bold" style={{ color: style.accent }}>{game.wins}</span>
                  <p className="text-xs text-slate-400">Wins</p>
                </div>
              </div>
            </div>

            {game.personalNotes && (
              <div className={`mt-3 p-2 rounded-lg ${style.bg}`}>
                <p className="text-xs text-slate-600 italic">📝 {game.personalNotes}</p>
              </div>
            )}
          </div>
        </div>
      </motion.div>
    </div>
  );
};

// ============================================================================
// Design 3: Illustrated Playful (Exploding Kittens style)
// ============================================================================

const PlayfulCard: React.FC<GameCardProps> = ({ game, state }) => {
  const [isFlipped, setIsFlipped] = useState(false);

  const stateThemes = {
    owned: {
      bg: 'bg-gradient-to-br from-orange-100 via-yellow-50 to-orange-100',
      border: 'border-orange-300',
      accent: 'text-orange-600',
      accentBg: 'bg-orange-500',
      shadow: 'shadow-orange-200',
    },
    wishlist: {
      bg: 'bg-gradient-to-br from-pink-100 via-rose-50 to-pink-100',
      border: 'border-pink-300',
      accent: 'text-pink-600',
      accentBg: 'bg-pink-500',
      shadow: 'shadow-pink-200',
    },
    new: {
      bg: 'bg-gradient-to-br from-cyan-100 via-sky-50 to-cyan-100',
      border: 'border-cyan-300',
      accent: 'text-cyan-600',
      accentBg: 'bg-cyan-500',
      shadow: 'shadow-cyan-200',
    },
  };

  const theme = stateThemes[state];

  return (
    <div className="w-[280px] h-[400px]" style={{ perspective: '1000px' }}>
      <motion.div
        className="relative w-full h-full cursor-pointer"
        style={{ transformStyle: 'preserve-3d' }}
        animate={{ rotateY: isFlipped ? 180 : 0 }}
        transition={{ duration: 0.5, type: 'spring', stiffness: 100 }}
        onClick={() => setIsFlipped(!isFlipped)}
        whileHover={{ scale: 1.02, rotate: isFlipped ? 180 : 2 }}
      >
        {/* Front */}
        <div
          className={`absolute inset-0 rounded-[24px] ${theme.bg} border-4 ${theme.border} shadow-xl ${theme.shadow} overflow-hidden`}
          style={{ backfaceVisibility: 'hidden' }}
        >
          {/* Squiggly top decoration */}
          <svg className="absolute top-0 left-0 w-full h-6" viewBox="0 0 280 24" preserveAspectRatio="none">
            <path
              d="M0,12 Q35,0 70,12 T140,12 T210,12 T280,12 V24 H0 Z"
              fill="currentColor"
              className={theme.accent}
              opacity="0.2"
            />
          </svg>

          {/* State Badge - Playful style */}
          <div className={`absolute top-4 right-4 ${theme.accentBg} px-3 py-1 rounded-full text-white text-xs font-bold uppercase rotate-3 shadow-md`}>
            {state === 'owned' ? '✓ GOT IT!' : state === 'wishlist' ? '★ WANT!' : '✨ NEW!'}
          </div>

          {/* Image area with doodle frame */}
          <div className="mt-6 mx-4 h-[170px] rounded-2xl bg-white border-3 border-dashed border-current relative overflow-hidden" style={{ borderColor: 'currentColor' }}>
            <div className="absolute inset-0 flex items-center justify-center bg-gradient-to-br from-white to-slate-50">
              <span className="text-7xl transform -rotate-6">🎲</span>
            </div>
            {/* Corner doodles */}
            <span className="absolute top-1 left-1 text-lg">⭐</span>
            <span className="absolute top-1 right-1 text-lg">✦</span>
            <span className="absolute bottom-1 left-1 text-lg">♦</span>
            <span className="absolute bottom-1 right-1 text-lg">●</span>
          </div>

          {/* Title - Hand-drawn feel */}
          <div className="mt-4 px-4 text-center">
            <h3 className={`font-black text-2xl ${theme.accent} tracking-tight transform -rotate-1`}
                style={{ fontFamily: 'Comic Sans MS, cursive, sans-serif' }}>
              {game.title}
            </h3>
            <p className="text-sm text-slate-500 mt-1">{game.publisher}</p>
          </div>

          {/* Fun stats */}
          <div className="mt-4 mx-4 flex justify-around">
            <div className="text-center transform rotate-2">
              <div className={`${theme.accentBg} text-white w-12 h-12 rounded-full flex items-center justify-center text-lg font-bold shadow-md`}>
                {game.playerCount.min}-{game.playerCount.max}
              </div>
              <p className="text-xs text-slate-500 mt-1">players</p>
            </div>
            <div className="text-center transform -rotate-2">
              <div className={`${theme.accentBg} text-white w-12 h-12 rounded-full flex items-center justify-center text-lg font-bold shadow-md`}>
                {game.playTime.max}
              </div>
              <p className="text-xs text-slate-500 mt-1">minutes</p>
            </div>
            <div className="text-center transform rotate-1">
              <div className={`${theme.accentBg} text-white w-12 h-12 rounded-full flex items-center justify-center text-lg font-bold shadow-md`}>
                {game.rating.toFixed(1)}
              </div>
              <p className="text-xs text-slate-500 mt-1">rating</p>
            </div>
          </div>

          {/* Flip hint */}
          <p className={`absolute bottom-3 left-1/2 -translate-x-1/2 text-xs ${theme.accent} font-bold animate-bounce`}>
            ↻ FLIP ME!
          </p>
        </div>

        {/* Back */}
        <div
          className={`absolute inset-0 rounded-[24px] ${theme.bg} border-4 ${theme.border} shadow-xl ${theme.shadow} overflow-hidden`}
          style={{ backfaceVisibility: 'hidden', transform: 'rotateY(180deg)' }}
        >
          <div className="p-5 h-full flex flex-col">
            <h4 className={`font-black text-xl ${theme.accent} transform -rotate-1`}
                style={{ fontFamily: 'Comic Sans MS, cursive, sans-serif' }}>
              {game.title}
            </h4>

            <p className="text-sm text-slate-600 mt-2 leading-relaxed line-clamp-3"
               style={{ fontFamily: 'Comic Sans MS, cursive, sans-serif' }}>
              {game.description}
            </p>

            {/* Categories as fun bubbles */}
            <div className="mt-3 flex flex-wrap gap-2">
              {game.categories.map((cat, i) => (
                <span
                  key={cat}
                  className={`text-xs px-3 py-1 rounded-full ${theme.accentBg} text-white font-bold transform ${i % 2 === 0 ? 'rotate-2' : '-rotate-2'}`}
                >
                  {cat}
                </span>
              ))}
            </div>

            {/* Play stats - Trophy style */}
            <div className="mt-auto">
              <div className="flex items-center justify-center gap-6 py-3">
                <div className="text-center">
                  <span className="text-4xl">🎮</span>
                  <p className={`text-2xl font-black ${theme.accent}`}>{game.plays}</p>
                  <p className="text-xs text-slate-500">plays</p>
                </div>
                <div className="text-center">
                  <span className="text-4xl">🏆</span>
                  <p className={`text-2xl font-black ${theme.accent}`}>{game.wins}</p>
                  <p className="text-xs text-slate-500">wins</p>
                </div>
              </div>
            </div>

            {/* Notes speech bubble */}
            {game.personalNotes && (
              <div className="relative bg-white rounded-2xl p-3 mt-2 border-2 border-slate-200">
                <div className="absolute -top-2 left-4 w-4 h-4 bg-white border-l-2 border-t-2 border-slate-200 transform rotate-45" />
                <p className="text-xs text-slate-600" style={{ fontFamily: 'Comic Sans MS, cursive, sans-serif' }}>
                  💭 {game.personalNotes}
                </p>
              </div>
            )}
          </div>
        </div>
      </motion.div>
    </div>
  );
};

// ============================================================================
// Design 4: Retro Arcade / Pixel Art
// ============================================================================

const RetroArcadeCard: React.FC<GameCardProps> = ({ game, state }) => {
  const [isFlipped, setIsFlipped] = useState(false);

  const stateColors = {
    owned: { primary: '#22c55e', secondary: '#16a34a', glow: '#4ade80' },
    wishlist: { primary: '#f59e0b', secondary: '#d97706', glow: '#fbbf24' },
    new: { primary: '#ec4899', secondary: '#db2777', glow: '#f472b6' },
  };

  const colors = stateColors[state];

  return (
    <div className="w-[280px] h-[400px]" style={{ perspective: '1000px' }}>
      <motion.div
        className="relative w-full h-full cursor-pointer"
        style={{ transformStyle: 'preserve-3d' }}
        animate={{ rotateY: isFlipped ? 180 : 0 }}
        transition={{ duration: 0.4, ease: 'linear' }}
        onClick={() => setIsFlipped(!isFlipped)}
      >
        {/* Front */}
        <div
          className="absolute inset-0 bg-slate-900 border-4 overflow-hidden"
          style={{
            backfaceVisibility: 'hidden',
            borderColor: colors.primary,
            boxShadow: `0 0 20px ${colors.glow}40, inset 0 0 20px ${colors.glow}10`,
            imageRendering: 'pixelated',
          }}
        >
          {/* Scanlines overlay */}
          <div className="absolute inset-0 pointer-events-none opacity-10"
               style={{
                 background: 'repeating-linear-gradient(0deg, transparent, transparent 2px, rgba(0,0,0,0.3) 2px, rgba(0,0,0,0.3) 4px)'
               }} />

          {/* Pixel corner decorations */}
          <div className="absolute top-0 left-0 w-4 h-4" style={{ backgroundColor: colors.primary }} />
          <div className="absolute top-0 right-0 w-4 h-4" style={{ backgroundColor: colors.primary }} />
          <div className="absolute bottom-0 left-0 w-4 h-4" style={{ backgroundColor: colors.primary }} />
          <div className="absolute bottom-0 right-0 w-4 h-4" style={{ backgroundColor: colors.primary }} />

          {/* State badge - pixel style */}
          <div className="absolute top-6 right-6 px-2 py-1 text-black text-[10px] font-bold uppercase"
               style={{ backgroundColor: colors.primary, fontFamily: '"Press Start 2P", monospace' }}>
            {state}
          </div>

          {/* Game "screen" */}
          <div className="mx-6 mt-8 h-[160px] bg-slate-800 border-2 relative"
               style={{ borderColor: colors.secondary }}>
            <div className="absolute inset-2 bg-gradient-to-br from-slate-700 to-slate-900 flex items-center justify-center">
              <span className="text-6xl" style={{ filter: 'contrast(1.5)' }}>🎲</span>
            </div>
            {/* CRT effect corners */}
            <div className="absolute inset-0 bg-gradient-to-br from-white/5 to-transparent" />
          </div>

          {/* Title - Pixel font style */}
          <div className="mt-4 px-6">
            <h3 className="text-lg font-bold text-center uppercase tracking-wider"
                style={{ color: colors.primary, fontFamily: '"Press Start 2P", monospace', fontSize: '14px', lineHeight: '1.5' }}>
              {game.title}
            </h3>
            <p className="text-center mt-2" style={{ color: colors.glow, fontFamily: 'monospace', fontSize: '10px' }}>
              {game.publisher}
            </p>
          </div>

          {/* Stats - Arcade HUD style */}
          <div className="mx-6 mt-4 bg-slate-800/80 border p-3" style={{ borderColor: colors.secondary }}>
            <div className="grid grid-cols-2 gap-2 text-xs" style={{ fontFamily: 'monospace' }}>
              <div className="flex justify-between">
                <span className="text-slate-400">PLAYERS:</span>
                <span style={{ color: colors.glow }}>{game.playerCount.min}-{game.playerCount.max}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-400">TIME:</span>
                <span style={{ color: colors.glow }}>{game.playTime.max}M</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-400">RATING:</span>
                <span style={{ color: colors.glow }}>{game.rating.toFixed(1)}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-400">LEVEL:</span>
                <span style={{ color: colors.glow }}>{game.complexity.toFixed(1)}</span>
              </div>
            </div>
          </div>

          {/* Blinking prompt */}
          <p className="absolute bottom-4 left-1/2 -translate-x-1/2 text-xs animate-pulse"
             style={{ color: colors.primary, fontFamily: 'monospace' }}>
            [ PRESS TO FLIP ]
          </p>
        </div>

        {/* Back */}
        <div
          className="absolute inset-0 bg-slate-900 border-4 overflow-hidden"
          style={{
            backfaceVisibility: 'hidden',
            transform: 'rotateY(180deg)',
            borderColor: colors.primary,
            boxShadow: `0 0 20px ${colors.glow}40, inset 0 0 20px ${colors.glow}10`,
          }}
        >
          {/* Scanlines */}
          <div className="absolute inset-0 pointer-events-none opacity-10"
               style={{
                 background: 'repeating-linear-gradient(0deg, transparent, transparent 2px, rgba(0,0,0,0.3) 2px, rgba(0,0,0,0.3) 4px)'
               }} />

          <div className="p-4 h-full flex flex-col" style={{ fontFamily: 'monospace' }}>
            <h4 className="text-sm font-bold uppercase mb-2" style={{ color: colors.primary }}>
              // {game.title}
            </h4>

            <div className="bg-slate-800 border p-2 mb-3 text-xs text-slate-300" style={{ borderColor: colors.secondary }}>
              <span style={{ color: colors.glow }}>&gt;</span> {game.description.substring(0, 100)}...
            </div>

            {/* Categories as pixel tags */}
            <div className="flex flex-wrap gap-1 mb-3">
              {game.categories.map((cat) => (
                <span key={cat} className="text-[10px] px-2 py-1 bg-slate-800 border"
                      style={{ borderColor: colors.secondary, color: colors.glow }}>
                  [{cat.toUpperCase()}]
                </span>
              ))}
            </div>

            {/* High score style stats */}
            <div className="mt-auto">
              <div className="text-center mb-2" style={{ color: colors.primary }}>
                ═══ HIGH SCORES ═══
              </div>
              <div className="bg-slate-800 border p-3" style={{ borderColor: colors.secondary }}>
                <div className="flex justify-between items-center mb-2">
                  <span className="text-slate-400">TOTAL PLAYS:</span>
                  <span className="text-xl font-bold" style={{ color: colors.glow }}>
                    {String(game.plays).padStart(4, '0')}
                  </span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-slate-400">VICTORIES:</span>
                  <span className="text-xl font-bold" style={{ color: colors.glow }}>
                    {String(game.wins).padStart(4, '0')}
                  </span>
                </div>
              </div>
            </div>

            {game.personalNotes && (
              <div className="mt-2 text-[10px] text-slate-400 border-t border-slate-700 pt-2">
                <span style={{ color: colors.primary }}>NOTE:</span> {game.personalNotes}
              </div>
            )}
          </div>
        </div>
      </motion.div>
    </div>
  );
};

// ============================================================================
// Design 5: Premium Holographic
// ============================================================================

const HolographicCard: React.FC<GameCardProps> = ({ game, state }) => {
  const [isFlipped, setIsFlipped] = useState(false);
  const [mousePos, setMousePos] = useState({ x: 0.5, y: 0.5 });

  const handleMouseMove = (e: React.MouseEvent<HTMLDivElement>) => {
    const rect = e.currentTarget.getBoundingClientRect();
    setMousePos({
      x: (e.clientX - rect.left) / rect.width,
      y: (e.clientY - rect.top) / rect.height,
    });
  };

  const stateGradients = {
    owned: 'from-emerald-400 via-cyan-400 to-blue-500',
    wishlist: 'from-violet-400 via-purple-400 to-fuchsia-500',
    new: 'from-amber-400 via-orange-400 to-rose-500',
  };

  return (
    <div className="w-[280px] h-[400px]" style={{ perspective: '1000px' }}>
      <motion.div
        className="relative w-full h-full cursor-pointer"
        style={{ transformStyle: 'preserve-3d' }}
        animate={{
          rotateY: isFlipped ? 180 : 0,
          rotateX: (mousePos.y - 0.5) * -10,
          rotateZ: (mousePos.x - 0.5) * 5,
        }}
        transition={{ duration: isFlipped ? 0.6 : 0.1 }}
        onClick={() => setIsFlipped(!isFlipped)}
        onMouseMove={handleMouseMove}
        onMouseLeave={() => setMousePos({ x: 0.5, y: 0.5 })}
      >
        {/* Front */}
        <div
          className="absolute inset-0 rounded-2xl overflow-hidden"
          style={{ backfaceVisibility: 'hidden' }}
        >
          {/* Holographic background */}
          <div
            className={`absolute inset-0 bg-gradient-to-br ${stateGradients[state]} opacity-90`}
            style={{
              backgroundImage: `
                linear-gradient(${mousePos.x * 360}deg,
                  rgba(255,255,255,0.3) 0%,
                  transparent 50%,
                  rgba(255,255,255,0.3) 100%
                )
              `,
            }}
          />

          {/* Foil shimmer effect */}
          <div
            className="absolute inset-0"
            style={{
              background: `
                radial-gradient(
                  circle at ${mousePos.x * 100}% ${mousePos.y * 100}%,
                  rgba(255,255,255,0.4) 0%,
                  transparent 50%
                )
              `,
            }}
          />

          {/* Rainbow refraction lines */}
          <div
            className="absolute inset-0 opacity-30"
            style={{
              backgroundImage: `
                repeating-linear-gradient(
                  ${45 + mousePos.x * 90}deg,
                  transparent 0px,
                  rgba(255,0,0,0.1) 2px,
                  rgba(255,255,0,0.1) 4px,
                  rgba(0,255,0,0.1) 6px,
                  rgba(0,255,255,0.1) 8px,
                  rgba(0,0,255,0.1) 10px,
                  rgba(255,0,255,0.1) 12px,
                  transparent 14px
                )
              `,
            }}
          />

          {/* Content overlay */}
          <div className="relative h-full p-4 flex flex-col">
            {/* State indicator */}
            <div className="flex justify-between items-start">
              <span className="text-white/80 text-xs font-medium uppercase tracking-widest">
                Collector's Edition
              </span>
              <span className="bg-white/20 backdrop-blur-sm px-2 py-0.5 rounded text-white text-[10px] font-bold uppercase">
                {state}
              </span>
            </div>

            {/* Image with holographic frame */}
            <div className="mt-3 h-[180px] rounded-xl overflow-hidden relative">
              <div className="absolute inset-0 bg-gradient-to-br from-white/20 to-black/20 flex items-center justify-center">
                <span className="text-7xl drop-shadow-2xl">🎲</span>
              </div>
              {/* Inner glow frame */}
              <div className="absolute inset-0 border-2 border-white/30 rounded-xl" />
              <div className="absolute inset-1 border border-white/20 rounded-lg" />
            </div>

            {/* Title */}
            <div className="mt-4 text-center">
              <h3 className="text-2xl font-bold text-white drop-shadow-lg tracking-tight">
                {game.title}
              </h3>
              <p className="text-white/70 text-sm mt-1">{game.publisher}</p>
            </div>

            {/* Holographic stats bar */}
            <div className="mt-auto">
              <div className="bg-white/10 backdrop-blur-sm rounded-xl p-3 border border-white/20">
                <div className="flex justify-around text-center">
                  <div>
                    <span className="text-xl font-bold text-white">{game.playerCount.min}-{game.playerCount.max}</span>
                    <p className="text-[10px] text-white/60 uppercase">Players</p>
                  </div>
                  <div className="border-l border-white/20 pl-4">
                    <span className="text-xl font-bold text-white">{game.playTime.max}m</span>
                    <p className="text-[10px] text-white/60 uppercase">Time</p>
                  </div>
                  <div className="border-l border-white/20 pl-4">
                    <span className="text-xl font-bold text-white">{game.rating.toFixed(1)}</span>
                    <p className="text-[10px] text-white/60 uppercase">Rating</p>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Back */}
        <div
          className="absolute inset-0 rounded-2xl overflow-hidden"
          style={{ backfaceVisibility: 'hidden', transform: 'rotateY(180deg)' }}
        >
          {/* Holographic background */}
          <div className={`absolute inset-0 bg-gradient-to-br ${stateGradients[state]} opacity-90`} />

          {/* Pattern overlay */}
          <div className="absolute inset-0 opacity-20"
               style={{
                 backgroundImage: `url("data:image/svg+xml,%3Csvg width='20' height='20' viewBox='0 0 20 20' xmlns='http://www.w3.org/2000/svg'%3E%3Cpolygon points='10,0 20,10 10,20 0,10' fill='white' fill-opacity='0.3'/%3E%3C/svg%3E")`,
                 backgroundSize: '20px 20px'
               }} />

          <div className="relative h-full p-5 flex flex-col text-white">
            <h4 className="text-xl font-bold drop-shadow-lg">{game.title}</h4>

            <p className="text-sm text-white/80 mt-2 leading-relaxed line-clamp-3">
              {game.description}
            </p>

            {/* Categories */}
            <div className="mt-4">
              <p className="text-[10px] uppercase tracking-wider text-white/60 mb-2">Categories</p>
              <div className="flex flex-wrap gap-1.5">
                {game.categories.map((cat) => (
                  <span key={cat} className="text-xs px-2 py-1 rounded-full bg-white/20 backdrop-blur-sm">
                    {cat}
                  </span>
                ))}
              </div>
            </div>

            {/* Mechanics */}
            <div className="mt-3">
              <p className="text-[10px] uppercase tracking-wider text-white/60 mb-2">Mechanics</p>
              <div className="flex flex-wrap gap-1.5">
                {game.mechanics.slice(0, 3).map((mech) => (
                  <span key={mech} className="text-xs px-2 py-1 rounded-full bg-black/20 backdrop-blur-sm">
                    {mech}
                  </span>
                ))}
              </div>
            </div>

            {/* Stats */}
            <div className="mt-auto bg-white/10 backdrop-blur-sm rounded-xl p-3 border border-white/20">
              <div className="flex justify-around text-center">
                <div>
                  <span className="text-2xl font-bold">{game.plays}</span>
                  <p className="text-[10px] text-white/60 uppercase">Plays</p>
                </div>
                <div>
                  <span className="text-2xl font-bold">{game.wins}</span>
                  <p className="text-[10px] text-white/60 uppercase">Wins</p>
                </div>
                <div>
                  <span className="text-2xl font-bold">{Math.round((game.wins / game.plays) * 100)}%</span>
                  <p className="text-[10px] text-white/60 uppercase">Win Rate</p>
                </div>
              </div>
            </div>

            {game.personalNotes && (
              <div className="mt-2 p-2 rounded-lg bg-black/20 backdrop-blur-sm">
                <p className="text-xs text-white/80 italic">📝 {game.personalNotes}</p>
              </div>
            )}
          </div>
        </div>
      </motion.div>
    </div>
  );
};

// ============================================================================
// Showcase Component
// ============================================================================

export function GameCardShowcase() {
  const states: CardState[] = ['owned', 'wishlist', 'new'];
  const stateLabels = { owned: 'A: Owned', wishlist: 'B: Wishlist', new: 'C: New' };

  const designs = [
    { name: 'Classic TCG', Component: ClassicTCGCard, description: 'Ornate borders, gold accents, fantasy aesthetic' },
    { name: 'Modern Minimal', Component: ModernMinimalCard, description: 'Clean lines, subtle card game elements' },
    { name: 'Playful Illustrated', Component: PlayfulCard, description: 'Hand-drawn, Exploding Kittens vibe' },
    { name: 'Retro Arcade', Component: RetroArcadeCard, description: 'Pixel art inspired, 8-bit nostalgia' },
    { name: 'Premium Holographic', Component: HolographicCard, description: 'Foil effects, collector\'s edition feel' },
  ];

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 p-8">
      {/* Font import for Retro Arcade card */}
      <link href="https://fonts.googleapis.com/css2?family=Press+Start+2P&display=swap" rel="stylesheet" />

      <div className="max-w-7xl mx-auto">
        <header className="text-center mb-12">
          <h1 className="text-4xl font-bold text-white mb-2">Game Card Design Mocks</h1>
          <p className="text-slate-400">5 Designs × 3 Variants = 15 Card Styles</p>
          <p className="text-sm text-slate-500 mt-2">Click any card to flip and see the back</p>
        </header>

        {designs.map((design, designIndex) => (
          <section key={design.name} className="mb-16">
            <div className="flex items-baseline gap-4 mb-6">
              <h2 className="text-2xl font-bold text-white">
                {designIndex + 1}. {design.name}
              </h2>
              <p className="text-slate-400 text-sm">{design.description}</p>
            </div>

            <div className="flex flex-wrap gap-8 justify-center lg:justify-start">
              {states.map((state) => (
                <div key={state} className="flex flex-col items-center gap-3">
                  <span className="text-xs font-medium text-slate-400 uppercase tracking-wider">
                    {stateLabels[state]}
                  </span>
                  <design.Component game={sampleGame} state={state} />
                </div>
              ))}
            </div>
          </section>
        ))}
      </div>
    </div>
  );
}

// Export individual components for use
export { ClassicTCGCard, ModernMinimalCard, PlayfulCard, RetroArcadeCard, HolographicCard };
export type { GameCardData, GameCardProps, CardState };
