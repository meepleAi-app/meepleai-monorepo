import { api } from '@/lib/api';

/** La risposta completa di un turno di QA, così come va salvata nel thread. */
export interface AssistantAnswerToPersist {
  content: string;
  /** #4168: la versione del profilo che ha risposto, se l'evento Complete l'ha data. */
  profileVersionId: string | null;
  /** #4168: la lingua della risposta, se l'evento Complete l'ha data. */
  language: string | null;
}

/**
 * Salva nel thread la risposta dell'assistente alla fine di uno stream di QA (#4838).
 *
 * `/chat/[threadId]` monta due viste, una per larghezza (`ChatThreadView` da `lg`, `ChatMobile` sotto), e
 * ciascuna aveva il proprio ciclo di `qaStream`: solo il desktop salvava la risposta, quindi su telefono
 * sparivano ricaricando il thread. Il salvataggio sta qui perché non nasca una terza copia.
 *
 * Restituisce l'id che il backend ha dato alla risposta, o `null` se la risposta non lo riporta. Il
 * feedback sulla risposta parte con quell'id (#4168): con l'id provvisorio della bolla il backend non
 * trova il messaggio da valutare. Un rifiuto della richiesta propaga a chi chiama.
 */
export async function persistAssistantAnswer(
  threadId: string,
  answer: AssistantAnswerToPersist
): Promise<string | null> {
  const saved = await api.chat.addMessage(threadId, {
    content: answer.content,
    role: 'assistant',
    profileVersionId: answer.profileVersionId,
    language: answer.language,
  });

  return saved?.messages?.filter(m => m.role === 'assistant').at(-1)?.backendMessageId ?? null;
}
