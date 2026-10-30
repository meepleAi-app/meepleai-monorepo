/**
 * I nomi dei giocatori nei wizard di creazione sessione (#4750).
 *
 * `LiveGameSession.AddPlayer` rifiuta due giocatori con lo stesso nome, senza badare alle
 * maiuscole né agli spazi ai bordi. Se il controllo lo fa solo il server, la sessione esiste già
 * quando l'aggiunta fallisce, e resta a metà. I wizard lo controllano prima di crearla.
 */

/** Come il server confronta i nomi: senza spazi ai bordi, senza badare alle maiuscole. */
export function playerNameKey(name: string): string {
  return name.trim().toLowerCase();
}

/** Il primo nome scritto due volte, come l'ha scritto l'utente; `null` se non ce ne sono. */
export function findDuplicatePlayerName(names: readonly string[]): string | null {
  const seen = new Set<string>();
  for (const raw of names) {
    const name = raw.trim();
    if (!name) continue;
    const key = playerNameKey(name);
    if (seen.has(key)) return name;
    seen.add(key);
  }
  return null;
}

export function duplicatePlayerNameMessage(name: string): string {
  return `Due giocatori si chiamano «${name}»: cambia o togli uno dei due.`;
}

/**
 * «Giocatore N» col primo N libero, a partire da quanti giocatori ci sono già. Contare i
 * giocatori non basta: tolto qualcuno dalla lista (per esempio con «Io non gioco»), il numero
 * dopo l'ultimo può essere già preso.
 */
export function nextDefaultPlayerName(names: readonly string[]): string {
  const taken = new Set(names.map(name => name.trim().toLowerCase()));
  let n = names.length + 1;
  while (taken.has(`giocatore ${n}`)) n++;
  return `Giocatore ${n}`;
}
