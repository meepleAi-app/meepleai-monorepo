'use client';

/**
 * RagHero — Animated 6-layer pipeline hero section
 *
 * Shows the TOMAC-RAG system at a glance:
 * - L1→L2→L3→L4→L5→L6 horizontal pipeline with color-coded nodes and flowing particles
 * - Key metrics (variants, accuracy, token savings, cache hit rate)
 * - Technical / Business ROI view toggle
 */

import React from 'react';

import { motion } from 'framer-motion';
import { Bot, Briefcase, Code2, Dices } from 'lucide-react';

import { cn } from '@/lib/utils';

import { DEFAULT_STATS } from './types';

import type { ViewMode } from './types';

// =============================================================================
// Pipeline Layer Data
// =============================================================================

const PIPELINE_LAYERS = [
  {
    emoji: '🧠',
    label: 'Routing',
    short: 'L1',
    bg: 'hsla(221,83%,53%,0.15)',
    border: 'hsla(221,83%,53%,0.5)',
    particle: 'hsl(221,83%,53%)',
    glow: 'hsla(221,83%,53%,0.35)',
  },
  {
    emoji: '💾',
    label: 'Cache',
    short: 'L2',
    bg: 'hsla(262,83%,62%,0.15)',
    border: 'hsla(262,83%,62%,0.5)',
    particle: 'hsl(262,83%,62%)',
    glow: 'hsla(262,83%,62%,0.35)',
  },
  {
    emoji: '📚',
    label: 'Retrieval',
    short: 'L3',
    bg: 'hsla(142,76%,36%,0.15)',
    border: 'hsla(142,76%,36%,0.5)',
    particle: 'hsl(142,76%,36%)',
    glow: 'hsla(142,76%,36%,0.35)',
  },
  {
    emoji: '✅',
    label: 'CRAG',
    short: 'L4',
    bg: 'hsla(45,93%,47%,0.15)',
    border: 'hsla(45,93%,47%,0.5)',
    particle: 'hsl(45,93%,47%)',
    glow: 'hsla(45,93%,47%,0.35)',
  },
  {
    emoji: '✨',
    label: 'Generation',
    short: 'L5',
    bg: 'hsla(25,95%,53%,0.15)',
    border: 'hsla(25,95%,53%,0.5)',
    particle: 'hsl(25,95%,53%)',
    glow: 'hsla(25,95%,53%,0.35)',
  },
  {
    emoji: '🔍',
    label: 'Validation',
    short: 'L6',
    bg: 'hsla(0,72%,51%,0.15)',
    border: 'hsla(0,72%,51%,0.5)',
    particle: 'hsl(0,72%,51%)',
    glow: 'hsla(0,72%,51%,0.35)',
  },
] as const;

// =============================================================================
// Hero Stats
// =============================================================================

const HERO_STATS = [
  {
    value: `${DEFAULT_STATS.ragVariants}`,
    label: 'Variants',
    gradient: 'linear-gradient(135deg, hsl(221,83%,53%), hsl(262,83%,62%))',
  },
  {
    value: `${DEFAULT_STATS.targetAccuracy}%`,
    label: 'Accuracy',
    gradient: 'linear-gradient(135deg, hsl(142,76%,36%), hsl(142,76%,50%))',
  },
  {
    value: `${Math.abs(DEFAULT_STATS.tokenReduction)}%`,
    label: 'Tokens Saved',
    gradient: 'linear-gradient(135deg, hsl(262,83%,62%), hsl(25,95%,53%))',
  },
  {
    value: `${DEFAULT_STATS.cacheHitRate}%`,
    label: 'Cache Hit',
    gradient: 'linear-gradient(135deg, hsl(25,95%,53%), hsl(45,93%,47%))',
  },
];
// =============================================================================
// Pipeline Connector with particle animation
// =============================================================================

interface PipelineConnectorProps {
  fromColor: string;
  toColor: string;
  index: number;
}

function PipelineConnector({ fromColor, toColor, index }: PipelineConnectorProps) {
  return (
    <div className={cn('rag-pipeline-connector', `rag-connector-${index}`)}>
      {/* Track */}
      <div
        className="rag-pipeline-track"
        style={{
          background: `linear-gradient(90deg, ${fromColor}70, ${toColor}70)`,
        }}
      />
      {/* Arrow */}
      <svg
        className="rag-pipeline-arrow"
        viewBox="0 0 9 14"
        fill={toColor}
        xmlns="http://www.w3.org/2000/svg"
      >
        <path d="M0 0 L9 7 L0 14 Z" />
      </svg>
      {/* Flowing particle */}
      <span
        className="rag-pipeline-particle"
        style={{
          backgroundColor: fromColor,
          boxShadow: `0 0 8px 2px ${fromColor}`,
        }}
      />
    </div>
  );
}

// =============================================================================
// Main RagHero Component
// =============================================================================

export interface RagHeroProps {
  viewMode: ViewMode;
  onViewModeChange: (mode: ViewMode) => void;
}
export function RagHero({ viewMode, onViewModeChange }: RagHeroProps) {
  return (
    <motion.div
      className="rag-hero"
      initial={{ opacity: 0, y: -12 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4 }}
    >
      {/* Ambient background glow */}
      <div className="rag-hero-bg" aria-hidden="true" />

      <div className="rag-hero-content">
        {/* ===== LEFT: Title + Stats + Toggle ===== */}
        <div className="rag-hero-left space-y-4">
          {/* Title row */}
          <div className="flex items-center gap-3">
            <div className="bg-gradient-to-br from-primary to-purple-600 p-2.5 rounded-xl shadow-lg shrink-0">
              <Dices className="h-6 w-6 text-white" />
            </div>
            <div>
              <h1 className="text-2xl font-bold font-quicksand leading-tight">
                <span className="text-foreground">MeepleAI </span>
                <span className="bg-gradient-to-r from-primary via-purple-500 to-orange-500 bg-clip-text text-transparent">
                  RAG
                </span>
              </h1>
              <p className="text-xs text-muted-foreground font-mono tracking-wide">
                TOMAC-RAG · Token-Optimized Modular Adaptive Corrective
              </p>
            </div>
          </div>

          {/* Key stats row */}
          <div className="flex flex-wrap gap-2">
            {HERO_STATS.map(stat => (
              <div key={stat.label} className="rag-hero-stat">
                <span className="rag-hero-stat-value" style={{ backgroundImage: stat.gradient }}>
                  {stat.value}
                </span>
                <span className="rag-hero-stat-label">{stat.label}</span>
              </div>
            ))}
          </div>

          {/* View Toggle */}
          <div className="rag-hero-toggle">
            <button
              className="rag-hero-toggle-btn"
              data-active={viewMode === 'technical' ? 'true' : 'false'}
              onClick={() => onViewModeChange('technical')}
              aria-pressed={viewMode === 'technical'}
            >
              <Code2 className="h-3.5 w-3.5" />
              Technical
            </button>
            <button
              className="rag-hero-toggle-btn"
              data-active={viewMode === 'business' ? 'true' : 'false'}
              onClick={() => onViewModeChange('business')}
              aria-pressed={viewMode === 'business'}
            >
              <Briefcase className="h-3.5 w-3.5" />
              Business ROI
            </button>
          </div>
        </div>

        {/* ===== RIGHT: Pipeline Visualization ===== */}
        <div className="rag-hero-pipeline-container">
          <div className="rag-hero-pipeline" role="img" aria-label="TOMAC-RAG 6-layer pipeline visualization">
            {PIPELINE_LAYERS.map((layer, i) => (
              <React.Fragment key={layer.short}>
                {/* Layer node */}
                <motion.div
                  className="rag-pipeline-node"
                  initial={{ opacity: 0, y: 8 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: 0.15 + i * 0.07, duration: 0.3 }}
                >
                  <div
                    className="rag-pipeline-icon-wrap"
                    style={{
                      backgroundColor: layer.bg,
                      borderColor: layer.border,
                      boxShadow: viewMode === 'technical' ? `0 0 12px ${layer.glow}` : 'none',
                    }}
                    title={`${layer.short}: ${layer.label}`}
                  >
                    <span role="img" aria-label={layer.label}>
                      {layer.emoji}
                    </span>
                  </div>
                  <span className="rag-pipeline-label">{layer.short}</span>
                  <span className="rag-pipeline-sublabel">{layer.label}</span>
                </motion.div>

                {/* Connector + particle */}
                {i < PIPELINE_LAYERS.length - 1 && (
                  <PipelineConnector
                    fromColor={layer.particle}
                    toColor={PIPELINE_LAYERS[i + 1].particle}
                    index={i}
                  />
                )}
              </React.Fragment>
            ))}
          </div>

          {/* Pipeline caption */}
          <p className="text-xs text-muted-foreground text-center mt-2 opacity-70">
            <Bot className="inline h-3 w-3 mr-1 -mt-0.5" />
            Real-time query flow through 6 AI processing layers
          </p>
        </div>
      </div>
    </motion.div>
  );
}
