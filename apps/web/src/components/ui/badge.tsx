// Re-export from canonical location.
//
// #4513 — questo modulo aveva una sua copia del Badge con colori Tailwind
// fissi: la variante `outline` aveva testo scuro senza sfondo, illeggibile su
// fondo scuro. Ora c'è un solo primitivo, quello con i token semantici.
// Per il codice nuovo importa direttamente `@/components/ui/data-display/badge`.
export { Badge, badgeVariants, type BadgeProps } from '@/components/ui/data-display/badge';
