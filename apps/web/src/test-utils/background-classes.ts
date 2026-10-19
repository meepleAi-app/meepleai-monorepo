/**
 * #4539 — le classi di fondo rimaste su un elemento dopo il merge di tailwind-merge.
 *
 * jsdom non applica il CSS di Tailwind, quindi un test non legge il colore calcolato. Può però
 * leggere quali classi `bg-*` sono sopravvissute, con qualsiasi modificatore (`dark:`, `hover:`,
 * `dark:hover:`): quando un consumatore passa un suo fondo a un primitivo con varianti, devono
 * restare solo le sue. Una classe col modificatore lasciata dalla variante batte il fondo del
 * consumatore in quel tema o in quello stato, per specificità, qualunque sia l'ordine.
 */
export function backgroundClasses(element: Element): string[] {
  return Array.from(element.classList)
    .filter(token => /(?:^|:)bg-/.test(token))
    .sort();
}
