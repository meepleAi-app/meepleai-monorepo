/**
 * Il nome più lungo di un giocatore, dal posto al tavolo all'avvio della partita (#5150).
 *
 * È il limite del server (`PlayerNameLimit.MaxLength` nell'API): il posto della partita dal vivo,
 * il giocatore della partita correlata che l'avvio riempie, il vincitore e il partecipante del
 * tracciamento ne prendono al più 50. Il conteggio è in unità UTF-16, come `string.Length` in .NET.
 */
export const PLAYER_NAME_MAX_LENGTH = 50;

/**
 * Taglia un nome al limite di un giocatore, senza spazi in coda e senza lasciare a metà un
 * carattere fuori dal piano base (un'emoji occupa due unità).
 */
export function clampPlayerName(name: string): string {
  if (name.length <= PLAYER_NAME_MAX_LENGTH) return name;

  let cut = name.slice(0, PLAYER_NAME_MAX_LENGTH);
  const last = cut.charCodeAt(cut.length - 1);
  if (last >= 0xd800 && last <= 0xdbff) cut = cut.slice(0, -1);

  return cut.trimEnd();
}
