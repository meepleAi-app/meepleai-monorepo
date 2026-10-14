/**
 * #4169 (ADR-095 D5): come la pagina admin legge il prezzo di un modello del catalogo. La usano
 * «Models & prices» (6b) e la pagina «Agente» (6c), che avverte prima di pubblicare un modello senza
 * prezzo.
 */
import type { AiModelDto } from '@/lib/api/schemas';

/** Providers that cost nothing: models served in house. */
export const FREE_PROVIDERS = new Set(['ollama']);

/**
 * - `priced`: a non-zero price in USD;
 * - `free`: zero, on a provider that serves the model in house;
 * - `unpriced`: zero on a paid provider: counted as free, so it escapes the budget cap. The backend
 *   still publishes it (it is in the price list), the page warns;
 * - `foreign-currency`: not in USD, so it is not in the price list at all.
 */
export type PriceStatus = 'priced' | 'free' | 'unpriced' | 'foreign-currency';

export function priceStatus(model: AiModelDto): PriceStatus {
  const { pricing } = model.settings;
  if (pricing.currency.toUpperCase() !== 'USD') return 'foreign-currency';
  if (pricing.inputPricePerMillion > 0 || pricing.outputPricePerMillion > 0) return 'priced';
  return FREE_PROVIDERS.has(model.provider.toLowerCase()) ? 'free' : 'unpriced';
}
