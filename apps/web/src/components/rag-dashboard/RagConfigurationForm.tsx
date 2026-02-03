'use client';

/**
 * RAG Configuration Admin Form
 *
 * Allows admins to configure RAG parameters:
 * - Override estimated values with measured production data
 * - Update model pricing
 * - Configure strategy tokens, costs, accuracy, and latency
 *
 * @see docs/03-api/rag/appendix/F-calculation-formulas.md
 */

import { useState, useMemo } from 'react';

import {
  type ConfigurableValue,
  type RagConfigurationState,
  type ConfigurableStrategyConfig,
  type ModelPricing,
  DEFAULT_CONFIGURATION,
  getEffectiveValue,
  hasMeasuredData,
  formatAccuracy,
  formatLatency,
  calculateMonthlyCost,
} from './types-configurable';

import type { RagStrategy } from './types';

// =============================================================================
// Sub-Components
// =============================================================================

interface ConfigurableInputProps<T extends number | string> {
  label: string;
  value: ConfigurableValue<T>;
  onChange: (value: ConfigurableValue<T>) => void;
  type?: 'number' | 'text';
  suffix?: string;
  helpText?: string;
  min?: number;
  max?: number;
  step?: number;
}

function ConfigurableInput<T extends number | string>({
  label,
  value,
  onChange,
  type = 'number',
  suffix,
  helpText,
  min,
  max,
  step,
}: ConfigurableInputProps<T>) {
  const hasMeasured = hasMeasuredData(value);
  const effectiveValue = getEffectiveValue(value);

  const handleEstimatedChange = (newValue: T) => {
    onChange({ ...value, estimated: newValue });
  };

  const handleMeasuredChange = (newValue: T | undefined) => {
    const updated = { ...value };
    if (newValue === undefined || newValue === '') {
      delete updated.measured;
      delete updated.measuredAt;
      delete updated.confidence;
    } else {
      updated.measured = newValue as T;
      updated.measuredAt = new Date().toISOString();
    }
    onChange(updated);
  };

  return (
    <div className="space-y-2">
      <label className="block text-sm font-medium text-gray-700">{label}</label>

      <div className="grid grid-cols-2 gap-4">
        {/* Estimated Value */}
        <div>
          <label className="block text-xs text-gray-500 mb-1">
            Stimato (teorico)
          </label>
          <div className="relative">
            <input
              type={type}
              value={value.estimated as string | number}
              onChange={(e) =>
                handleEstimatedChange(
                  type === 'number' ? (Number(e.target.value) as T) : (e.target.value as T)
                )
              }
              min={min}
              max={max}
              step={step}
              className={`w-full px-3 py-2 border rounded-md text-sm ${
                !hasMeasured
                  ? 'border-blue-300 bg-blue-50'
                  : 'border-gray-300 bg-gray-50'
              }`}
            />
            {suffix && (
              <span className="absolute right-3 top-2 text-gray-400 text-sm">
                {suffix}
              </span>
            )}
          </div>
        </div>

        {/* Measured Value */}
        <div>
          <label className="block text-xs text-gray-500 mb-1">
            Misurato (produzione)
          </label>
          <div className="relative">
            <input
              type={type}
              value={(value.measured as string | number) ?? ''}
              onChange={(e) =>
                handleMeasuredChange(
                  e.target.value === ''
                    ? undefined
                    : type === 'number'
                    ? (Number(e.target.value) as T)
                    : (e.target.value as T)
                )
              }
              min={min}
              max={max}
              step={step}
              placeholder="Non misurato"
              className={`w-full px-3 py-2 border rounded-md text-sm ${
                hasMeasured
                  ? 'border-green-300 bg-green-50'
                  : 'border-gray-200 bg-white'
              }`}
            />
            {suffix && (
              <span className="absolute right-3 top-2 text-gray-400 text-sm">
                {suffix}
              </span>
            )}
          </div>
          {value.measuredAt && (
            <span className="text-xs text-gray-400">
              Aggiornato: {new Date(value.measuredAt).toLocaleDateString()}
            </span>
          )}
        </div>
      </div>

      {/* Effective Value Display */}
      <div className="flex items-center gap-2 text-sm">
        <span className="text-gray-500">Valore effettivo:</span>
        <span
          className={`font-semibold ${
            hasMeasured ? 'text-green-600' : 'text-blue-600'
          }`}
        >
          {typeof effectiveValue === 'number'
            ? effectiveValue.toLocaleString()
            : effectiveValue}
          {suffix && ` ${suffix}`}
        </span>
        {hasMeasured && (
          <span className="px-2 py-0.5 bg-green-100 text-green-700 text-xs rounded-full">
            Misurato
          </span>
        )}
      </div>

      {helpText && <p className="text-xs text-gray-400">{helpText}</p>}
    </div>
  );
}

// =============================================================================
// Strategy Configuration Panel
// =============================================================================

interface StrategyConfigPanelProps {
  strategy: RagStrategy;
  config: ConfigurableStrategyConfig;
  onChange: (config: ConfigurableStrategyConfig) => void;
}

function StrategyConfigPanel({
  strategy: _strategy,
  config,
  onChange,
}: StrategyConfigPanelProps) {
  const [isExpanded, setIsExpanded] = useState(false);

  return (
    <div className="border rounded-lg overflow-hidden">
      {/* Header */}
      <button
        onClick={() => setIsExpanded(!isExpanded)}
        className="w-full px-4 py-3 bg-gray-50 flex items-center justify-between hover:bg-gray-100 transition-colors"
      >
        <div className="flex items-center gap-3">
          <span className="font-semibold text-lg">{config.displayName}</span>
          <span className="text-gray-500 text-sm">{config.description}</span>
        </div>
        <div className="flex items-center gap-4">
          <div className="text-right text-sm">
            <div className="text-gray-600">
              {getEffectiveValue(config.tokens).toLocaleString()} tokens
            </div>
            <div className="text-gray-500">${getEffectiveValue(config.cost).toFixed(4)}/query</div>
          </div>
          <span className="text-gray-400">{isExpanded ? '▼' : '▶'}</span>
        </div>
      </button>

      {/* Expanded Content */}
      {isExpanded && (
        <div className="p-4 space-y-6 bg-white">
          {/* Tokens & Cost */}
          <div className="grid grid-cols-2 gap-6">
            <ConfigurableInput
              label="Token totali"
              value={config.tokens}
              onChange={(tokens) => onChange({ ...config, tokens })}
              suffix="tokens"
              helpText="Consumo totale token per strategia"
            />

            <ConfigurableInput
              label="Costo per query"
              value={config.cost}
              onChange={(cost) => onChange({ ...config, cost })}
              suffix="USD"
              step={0.0001}
              helpText="Costo medio per singola query"
            />
          </div>

          {/* Latency */}
          <div>
            <h4 className="font-medium text-gray-700 mb-3">Latenza</h4>
            <div className="grid grid-cols-2 gap-6">
              <ConfigurableInput
                label="Latenza minima"
                value={config.latency.minMs}
                onChange={(minMs) =>
                  onChange({
                    ...config,
                    latency: { ...config.latency, minMs },
                  })
                }
                suffix="ms"
                min={0}
              />

              <ConfigurableInput
                label="Latenza massima"
                value={config.latency.maxMs}
                onChange={(maxMs) =>
                  onChange({
                    ...config,
                    latency: { ...config.latency, maxMs },
                  })
                }
                suffix="ms"
                min={0}
              />
            </div>
            <div className="mt-2 text-sm text-gray-500">
              Visualizzato come: <span className="font-mono">{formatLatency(config)}</span>
            </div>
          </div>

          {/* Accuracy */}
          <div>
            <h4 className="font-medium text-gray-700 mb-3">Accuratezza</h4>
            <div className="grid grid-cols-2 gap-6">
              <ConfigurableInput
                label="Accuratezza minima"
                value={config.accuracy.min}
                onChange={(min) =>
                  onChange({
                    ...config,
                    accuracy: { ...config.accuracy, min },
                  })
                }
                suffix="%"
                min={0}
                max={1}
                step={0.01}
                helpText="Inserire come decimale (0.85 = 85%)"
              />

              <ConfigurableInput
                label="Accuratezza massima"
                value={config.accuracy.max}
                onChange={(max) =>
                  onChange({
                    ...config,
                    accuracy: { ...config.accuracy, max },
                  })
                }
                suffix="%"
                min={0}
                max={1}
                step={0.01}
              />
            </div>
            <div className="mt-2 text-sm text-gray-500">
              Visualizzato come: <span className="font-mono">{formatAccuracy(config)}</span>
            </div>
          </div>

          {/* Usage Distribution */}
          <div>
            <h4 className="font-medium text-gray-700 mb-3">Distribuzione utilizzo</h4>
            <div className="grid grid-cols-2 gap-6">
              <div>
                <label className="block text-xs text-gray-500 mb-1">Min %</label>
                <input
                  type="number"
                  value={getEffectiveValue(config.usagePercent).min}
                  onChange={(e) =>
                    onChange({
                      ...config,
                      usagePercent: {
                        ...config.usagePercent,
                        estimated: {
                          ...config.usagePercent.estimated,
                          min: Number(e.target.value),
                        },
                      },
                    })
                  }
                  min={0}
                  max={100}
                  className="w-full px-3 py-2 border rounded-md text-sm"
                />
              </div>
              <div>
                <label className="block text-xs text-gray-500 mb-1">Max %</label>
                <input
                  type="number"
                  value={getEffectiveValue(config.usagePercent).max}
                  onChange={(e) =>
                    onChange({
                      ...config,
                      usagePercent: {
                        ...config.usagePercent,
                        estimated: {
                          ...config.usagePercent.estimated,
                          max: Number(e.target.value),
                        },
                      },
                    })
                  }
                  min={0}
                  max={100}
                  className="w-full px-3 py-2 border rounded-md text-sm"
                />
              </div>
            </div>
          </div>

          {/* Models */}
          <div>
            <h4 className="font-medium text-gray-700 mb-2">Modelli primari</h4>
            <div className="flex flex-wrap gap-2">
              {config.primaryModels.map((model) => (
                <span
                  key={model}
                  className="px-2 py-1 bg-gray-100 text-gray-700 text-sm rounded"
                >
                  {model}
                </span>
              ))}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

// =============================================================================
// Model Pricing Panel
// =============================================================================

interface ModelPricingPanelProps {
  pricing: ModelPricing[];
  onChange: (pricing: ModelPricing[]) => void;
}

function ModelPricingPanel({ pricing, onChange }: ModelPricingPanelProps) {
  const [isExpanded, setIsExpanded] = useState(false);

  const updateModel = (index: number, updated: ModelPricing) => {
    const newPricing = [...pricing];
    newPricing[index] = updated;
    onChange(newPricing);
  };

  return (
    <div className="border rounded-lg overflow-hidden">
      <button
        onClick={() => setIsExpanded(!isExpanded)}
        className="w-full px-4 py-3 bg-gray-50 flex items-center justify-between hover:bg-gray-100"
      >
        <div className="flex items-center gap-3">
          <span className="font-semibold text-lg">Prezzi Modelli LLM</span>
          <span className="text-gray-500 text-sm">{pricing.length} modelli configurati</span>
        </div>
        <span className="text-gray-400">{isExpanded ? '▼' : '▶'}</span>
      </button>

      {isExpanded && (
        <div className="p-4 bg-white">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b">
                  <th className="text-left py-2 px-2">Modello</th>
                  <th className="text-left py-2 px-2">Provider</th>
                  <th className="text-right py-2 px-2">Input ($/1M)</th>
                  <th className="text-right py-2 px-2">Output ($/1M)</th>
                  <th className="text-right py-2 px-2">Cache ($/1M)</th>
                  <th className="text-center py-2 px-2">Free</th>
                </tr>
              </thead>
              <tbody>
                {pricing.map((model, index) => (
                  <tr key={model.modelId} className="border-b hover:bg-gray-50">
                    <td className="py-2 px-2 font-medium">{model.modelName}</td>
                    <td className="py-2 px-2 text-gray-500">{model.provider}</td>
                    <td className="py-2 px-2">
                      <input
                        type="number"
                        value={getEffectiveValue(model.inputCost)}
                        onChange={(e) =>
                          updateModel(index, {
                            ...model,
                            inputCost: { estimated: Number(e.target.value) },
                            lastUpdated: new Date().toISOString(),
                          })
                        }
                        step={0.01}
                        min={0}
                        className="w-20 px-2 py-1 border rounded text-right"
                        disabled={model.isFree}
                      />
                    </td>
                    <td className="py-2 px-2">
                      <input
                        type="number"
                        value={getEffectiveValue(model.outputCost)}
                        onChange={(e) =>
                          updateModel(index, {
                            ...model,
                            outputCost: { estimated: Number(e.target.value) },
                            lastUpdated: new Date().toISOString(),
                          })
                        }
                        step={0.01}
                        min={0}
                        className="w-20 px-2 py-1 border rounded text-right"
                        disabled={model.isFree}
                      />
                    </td>
                    <td className="py-2 px-2">
                      <input
                        type="number"
                        value={model.cacheCost ? getEffectiveValue(model.cacheCost) : 0}
                        onChange={(e) =>
                          updateModel(index, {
                            ...model,
                            cacheCost: { estimated: Number(e.target.value) },
                            lastUpdated: new Date().toISOString(),
                          })
                        }
                        step={0.001}
                        min={0}
                        className="w-20 px-2 py-1 border rounded text-right"
                        disabled={model.isFree}
                      />
                    </td>
                    <td className="py-2 px-2 text-center">
                      <input
                        type="checkbox"
                        checked={model.isFree}
                        onChange={(e) =>
                          updateModel(index, {
                            ...model,
                            isFree: e.target.checked,
                            inputCost: { estimated: e.target.checked ? 0 : model.inputCost.estimated },
                            outputCost: { estimated: e.target.checked ? 0 : model.outputCost.estimated },
                            lastUpdated: new Date().toISOString(),
                          })
                        }
                        className="h-4 w-4"
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="mt-4 text-xs text-gray-500">
            Ultimo aggiornamento prezzi:{' '}
            {pricing.length > 0 && new Date(pricing[0].lastUpdated).toLocaleDateString()}
          </div>
        </div>
      )}
    </div>
  );
}

// =============================================================================
// Cost Preview Panel
// =============================================================================

interface CostPreviewPanelProps {
  config: RagConfigurationState;
}

// Default strategy distribution (outside component to avoid recreating on every render)
const DEFAULT_STRATEGY_DISTRIBUTION: Record<RagStrategy, number> = {
  FAST: 0.6,
  BALANCED: 0.25,
  PRECISE: 0.1,
  EXPERT: 0.03,
  CONSENSUS: 0.02,
  CUSTOM: 0,
};

function CostPreviewPanel({ config }: CostPreviewPanelProps) {
  const [queriesPerMonth, setQueriesPerMonth] = useState(100000);

  const costProjection = useMemo(() => {
    return calculateMonthlyCost(queriesPerMonth, DEFAULT_STRATEGY_DISTRIBUTION, config);
  }, [queriesPerMonth, config]);

  return (
    <div className="border rounded-lg p-4 bg-gradient-to-br from-blue-50 to-indigo-50">
      <h3 className="font-semibold text-lg mb-4">Anteprima Costi Mensili</h3>

      <div className="mb-4">
        <label className="block text-sm text-gray-600 mb-1">Query al mese</label>
        <input
          type="number"
          value={queriesPerMonth}
          onChange={(e) => setQueriesPerMonth(Number(e.target.value))}
          step={10000}
          min={0}
          className="w-full px-3 py-2 border rounded-md"
        />
      </div>

      <div className="space-y-3">
        {/* Total Cost */}
        <div className="flex justify-between items-center py-2 border-b">
          <span className="font-medium">Costo totale</span>
          <span className="text-2xl font-bold text-blue-600">
            ${costProjection.totalCost.toFixed(2)}
          </span>
        </div>

        {/* By Strategy */}
        <div>
          <span className="text-sm text-gray-600">Per strategia:</span>
          <div className="mt-2 space-y-1">
            {(Object.entries(costProjection.byStrategy) as [RagStrategy, number][])
              .filter(([, cost]) => cost > 0)
              .sort(([, a], [, b]) => b - a)
              .map(([strategy, cost]) => (
                <div key={strategy} className="flex justify-between text-sm">
                  <span>{strategy}</span>
                  <span className="font-mono">${cost.toFixed(2)}</span>
                </div>
              ))}
          </div>
        </div>

        {/* Savings */}
        <div className="pt-2 border-t">
          <div className="flex justify-between text-sm text-green-600">
            <span>Risparmio da cache</span>
            <span>-${costProjection.savings.fromCache.toFixed(2)}</span>
          </div>
        </div>

        {/* Per Query */}
        <div className="pt-2 border-t">
          <div className="flex justify-between">
            <span className="text-gray-600">Costo medio per query</span>
            <span className="font-mono">
              ${(costProjection.totalCost / queriesPerMonth).toFixed(6)}
            </span>
          </div>
        </div>
      </div>
    </div>
  );
}

// =============================================================================
// Main Component
// =============================================================================

export interface RagConfigurationFormProps {
  initialConfig?: RagConfigurationState;
  onSave?: (config: RagConfigurationState) => void;
  onExport?: (config: RagConfigurationState) => void;
}

export function RagConfigurationForm({
  initialConfig = DEFAULT_CONFIGURATION,
  onSave,
  onExport,
}: RagConfigurationFormProps) {
  const [config, setConfig] = useState<RagConfigurationState>(initialConfig);
  const [isDirty, setIsDirty] = useState(false);

  const handleStrategyChange = (strategy: RagStrategy, updated: ConfigurableStrategyConfig) => {
    setConfig({
      ...config,
      strategies: { ...config.strategies, [strategy]: updated },
      lastUpdated: new Date().toISOString(),
    });
    setIsDirty(true);
  };

  const handleModelPricingChange = (pricing: ModelPricing[]) => {
    setConfig({
      ...config,
      modelPricing: pricing,
      lastUpdated: new Date().toISOString(),
    });
    setIsDirty(true);
  };

  const handleSave = () => {
    onSave?.(config);
    setIsDirty(false);
  };

  const handleExport = () => {
    onExport?.(config);
  };

  const handleReset = () => {
    if (confirm('Vuoi resettare tutte le configurazioni ai valori predefiniti?')) {
      setConfig(DEFAULT_CONFIGURATION);
      setIsDirty(true);
    }
  };

  return (
    <div className="max-w-4xl mx-auto p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Configurazione RAG</h1>
          <p className="text-gray-500">
            Configura parametri di token, costi, accuratezza e latenza
          </p>
        </div>
        <div className="flex items-center gap-3">
          {isDirty && (
            <span className="px-2 py-1 bg-yellow-100 text-yellow-700 text-sm rounded">
              Modifiche non salvate
            </span>
          )}
          <button
            onClick={handleReset}
            className="px-4 py-2 border rounded-md hover:bg-gray-50"
          >
            Reset
          </button>
          <button
            onClick={handleExport}
            className="px-4 py-2 border rounded-md hover:bg-gray-50"
          >
            Esporta JSON
          </button>
          <button
            onClick={handleSave}
            disabled={!isDirty}
            className={`px-4 py-2 rounded-md text-white ${
              isDirty
                ? 'bg-blue-600 hover:bg-blue-700'
                : 'bg-gray-300 cursor-not-allowed'
            }`}
          >
            Salva
          </button>
        </div>
      </div>

      {/* Legend */}
      <div className="flex items-center gap-6 p-3 bg-gray-50 rounded-lg text-sm">
        <div className="flex items-center gap-2">
          <div className="w-4 h-4 bg-blue-100 border border-blue-300 rounded" />
          <span>Valore stimato (attivo)</span>
        </div>
        <div className="flex items-center gap-2">
          <div className="w-4 h-4 bg-green-100 border border-green-300 rounded" />
          <span>Valore misurato (attivo)</span>
        </div>
        <div className="flex items-center gap-2">
          <div className="w-4 h-4 bg-gray-100 border border-gray-300 rounded" />
          <span>Non configurato</span>
        </div>
      </div>

      {/* Model Pricing */}
      <ModelPricingPanel
        pricing={config.modelPricing}
        onChange={handleModelPricingChange}
      />

      {/* Strategy Configurations */}
      <div className="space-y-4">
        <h2 className="text-lg font-semibold">Strategie RAG</h2>
        {(Object.keys(config.strategies) as RagStrategy[]).map((strategy) => (
          <StrategyConfigPanel
            key={strategy}
            strategy={strategy}
            config={config.strategies[strategy]}
            onChange={(updated) => handleStrategyChange(strategy, updated)}
          />
        ))}
      </div>

      {/* Cost Preview */}
      <CostPreviewPanel config={config} />

      {/* Metadata */}
      <div className="text-sm text-gray-500 flex justify-between border-t pt-4">
        <span>Schema version: {config.schemaVersion}</span>
        <span>Ultimo aggiornamento: {new Date(config.lastUpdated).toLocaleString()}</span>
      </div>
    </div>
  );
}

export default RagConfigurationForm;
