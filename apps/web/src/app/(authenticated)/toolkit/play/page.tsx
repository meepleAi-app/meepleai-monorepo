'use client';

import { useState, useEffect } from 'react';

import { CounterTool } from '@/components/toolkit/CounterTool';
import { DiceRoller } from '@/components/toolkit/DiceRoller';
import { Randomizer } from '@/components/toolkit/Randomizer';
import { Timer } from '@/components/toolkit/Timer';
import { DEFAULT_TOOLKIT } from '@/lib/config/default-toolkit';
import type { ToolLogEntry } from '@/lib/types/standalone-toolkit';
import { appendToolLog, generateLogId, clearOldEntries } from '@/lib/utils/toolkit-log';

export default function ToolkitPlayPage() {
  const [log, setLog] = useState<ToolLogEntry[]>([]);
  const [actorLabel, setActorLabel] = useState('');

  useEffect(() => {
    clearOldEntries();
  }, []);

  const addLog = (entry: Omit<ToolLogEntry, 'id' | 'timestamp'>) => {
    const full: ToolLogEntry = {
      ...entry,
      id: generateLogId(),
      timestamp: new Date().toISOString(),
      actorLabel: actorLabel || undefined,
    };
    appendToolLog(full);
    setLog(prev => [...prev.slice(-49), full]);
  };

  return (
    <div className="mx-auto max-w-2xl space-y-6 p-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold text-slate-800">Toolkit</h1>
        <input
          type="text"
          value={actorLabel}
          onChange={e => setActorLabel(e.target.value)}
          placeholder="Chi gioca?"
          className="rounded-md border border-slate-200 px-3 py-1.5 text-sm placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-amber-400"
          maxLength={30}
        />
      </div>

      {/* Dadi */}
      <section>
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-400">Dadi</h2>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
          {DEFAULT_TOOLKIT.dice.map(config => (
            <DiceRoller
              key={config.name}
              config={config}
              actorLabel={actorLabel}
              onRoll={result =>
                addLog({
                  toolType: 'dice',
                  action: 'roll',
                  result: `${config.name} → ${result.total}`,
                })
              }
            />
          ))}
        </div>
      </section>

      {/* Timer */}
      {DEFAULT_TOOLKIT.timers.length > 0 && (
        <section>
          <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-400">
            Timer
          </h2>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            {DEFAULT_TOOLKIT.timers.map(config => (
              <Timer
                key={config.name}
                name={config.name}
                defaultSeconds={config.defaultSeconds}
                type={config.type}
                onAction={(action, seconds) =>
                  addLog({
                    toolType: 'timer',
                    action,
                    result: `${config.name}: ${action} @ ${seconds}s`,
                  })
                }
              />
            ))}
          </div>
        </section>
      )}

      {/* Contatori */}
      <section>
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-400">
          Contatori
        </h2>
        <div className="grid grid-cols-2 gap-3">
          {DEFAULT_TOOLKIT.counters.map(config => (
            <CounterTool
              key={config.id}
              id={config.id}
              name={config.name}
              initialValue={config.initialValue}
              min={config.min}
              max={config.max}
              onAction={(action, value) =>
                addLog({ toolType: 'counter', action, result: `${config.name}: ${value}` })
              }
            />
          ))}
        </div>
      </section>

      {/* Randomizzatore */}
      <section>
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-400">
          Randomizzatore
        </h2>
        <Randomizer
          onAction={extracted =>
            addLog({ toolType: 'randomizer', action: 'extract', result: extracted })
          }
        />
      </section>

      {/* Log */}
      {log.length > 0 && (
        <section>
          <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-400">
            Cronologia
          </h2>
          <div className="space-y-1 rounded-lg border border-slate-100 bg-slate-50 p-3">
            {[...log].reverse().map(entry => (
              <div key={entry.id} className="flex items-center gap-2 text-xs text-slate-600">
                <span className="text-slate-300">
                  {new Date(entry.timestamp).toLocaleTimeString('it-IT', {
                    hour: '2-digit',
                    minute: '2-digit',
                  })}
                </span>
                {entry.actorLabel && (
                  <span className="font-medium text-slate-500">{entry.actorLabel}</span>
                )}
                <span>{entry.result}</span>
              </div>
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
