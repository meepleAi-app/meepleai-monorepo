/**
 * Prefisso delle chiavi i18n con cui la pagina KB dice all'utente che un PDF NON e' stato cancellato
 * perche' altri giochi lo usano ancora (#4683): `<prefisso>unlinked` e `<prefisso>kept`.
 *
 * Sta in un file a parte perche' lo leggono sia `_content.tsx` sia il test sui cataloghi reali: i test
 * di pagina risolvono `t()` con un dizionario finto, e senza una definizione unica una chiave rinominata
 * in un posto solo farebbe mostrare all'utente la chiave grezza senza che nessun test lo veda.
 */
export const DELETE_OUTCOME_MESSAGE_PREFIX = 'pages.library.gameDetail.kb.deleteOutcome.';
