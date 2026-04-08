'use client';

/**
 * DiceRollerTab — Main dice tab composing preset rows, pool builder,
 * and result display. Logs every roll to the session diary.
 */

import React, { useCallback, useState } from 'react';

import { cn } from '@/lib/utils';

import { useDiceRoller } from '../hooks/useDiceRoller';
import { useToolkitDrawer } from '../ToolkitDrawerProvider';
import { UNIVERSAL_DICE_PRESETS } from '../types';
import { DicePoolBuilder } from './DicePoolBuilder';
import { DicePresetRow } from './DicePresetRow';
import { DiceResultDisplay } from './DiceResultDisplay';

import type { DicePreset, DiceResult } from '../types';

// ============================================================================
// Component
// ============================================================================

export function DiceRollerTab() {
  const { store, logEvent } = useToolkitDrawer();
  const customPresets = store(s => s.customDicePresets);
  const { roll, lastResult, isRolling } = useDiceRoller();

  // Collapsible sections
  const [showAiPresets, setShowAiPresets] = useState(false);
  const [showCustomPresets, setShowCustomPresets] = useState(true);

  // Save-preset inline form
  const [saveName, setSaveName] = useState('');
  const [lastFormula, setLastFormula] = useState('');

  // AI presets placeholder — will be populated from toolkit config in the future
  const aiPresets: DicePreset[] = [];

  const handleRoll = useCallback(
    (formula: string) => {
      const result: DiceResult = roll(formula);
      setLastFormula(formula);

      logEvent('dice_roll', {
        formula: result.formula,
        rolls: result.rolls,
        modifier: result.modifier,
        total: result.total,
      });
    },
    [roll, logEvent]
  );

  const handleSavePreset = () => {
    const name = saveName.trim();
    if (!name || !lastFormula || lastFormula === '0') return;

    store.getState().addCustomPreset({
      name,
      formula: lastFormula,
      source: 'custom',
    });
    setSaveName('');
  };

  return (
    <div className="flex flex-col gap-4" data-testid="dice-roller-tab">
      {/* Universal presets */}
      <section>
        <h3 className="mb-1.5 text-xs font-semibold uppercase tracking-wider text-gray-400">
          Preset
        </h3>
        <DicePresetRow presets={UNIVERSAL_DICE_PRESETS} onRoll={handleRoll} variant="universal" />
      </section>

      {/* AI presets (collapsible) */}
      {aiPresets.length > 0 && (
        <section>
          <button
            type="button"
            onClick={() => setShowAiPresets(v => !v)}
            className="mb-1.5 flex items-center gap-1 text-xs font-semibold uppercase tracking-wider text-purple-400"
          >
            <span className={cn('transition-transform', showAiPresets && 'rotate-90')}>
              &#9654;
            </span>
            Suggeriti AI
          </button>
          {showAiPresets && <DicePresetRow presets={aiPresets} onRoll={handleRoll} variant="ai" />}
        </section>
      )}

      {/* Custom presets (collapsible) */}
      <section>
        <button
          type="button"
          onClick={() => setShowCustomPresets(v => !v)}
          className="mb-1.5 flex items-center gap-1 text-xs font-semibold uppercase tracking-wider text-[hsl(142,70%,40%)]"
        >
          <span className={cn('transition-transform', showCustomPresets && 'rotate-90')}>
            &#9654;
          </span>
          I miei preset
        </button>
        {showCustomPresets && (
          <>
            <DicePresetRow presets={customPresets} onRoll={handleRoll} variant="custom" />

            {/* Save preset form */}
            {lastFormula && lastFormula !== '0' && (
              <div className="mt-2 flex items-center gap-2">
                <input
                  type="text"
                  placeholder="Nome preset"
                  value={saveName}
                  onChange={e => setSaveName(e.target.value)}
                  onKeyDown={e => {
                    if (e.key === 'Enter') handleSavePreset();
                  }}
                  className="flex-1 rounded-lg border border-gray-300 px-2 py-1 text-xs outline-none focus:border-[hsl(142,70%,45%)]"
                  data-testid="save-preset-input"
                />
                <button
                  type="button"
                  onClick={handleSavePreset}
                  disabled={!saveName.trim()}
                  className={cn(
                    'rounded-lg px-2 py-1 text-xs font-medium transition-colors',
                    saveName.trim()
                      ? 'bg-[hsl(142,70%,45%)] text-white hover:bg-[hsl(142,70%,40%)]'
                      : 'cursor-not-allowed bg-gray-200 text-gray-400'
                  )}
                  data-testid="save-preset-btn"
                >
                  Salva
                </button>
              </div>
            )}
          </>
        )}
      </section>

      {/* Pool builder */}
      <section>
        <h3 className="mb-1.5 text-xs font-semibold uppercase tracking-wider text-gray-400">
          Pool dadi
        </h3>
        <DicePoolBuilder onRoll={handleRoll} />
      </section>

      {/* Result display */}
      <DiceResultDisplay result={lastResult} isRolling={isRolling} />
    </div>
  );
}
