/**
 * Demo Chat Mock Responses
 * Issue #4781: Credible Q&A per demo game
 *
 * Each game has 5-10 Q&A pairs covering the game's key topics.
 * Follow-up questions are contextual to each game.
 */

import { DEMO_GAME_IDS } from './demo-games';

// ============================================================================
// Types
// ============================================================================

export interface DemoChatQA {
  question: string;
  answer: string;
  /** Keywords that should trigger this response in a mock matcher */
  keywords: string[];
}

export interface DemoGameChat {
  gameId: string;
  gameTitle: string;
  /** Q&A pairs for this game */
  responses: DemoChatQA[];
  /** Contextual follow-up questions */
  followUpQuestions: string[];
}

// ============================================================================
// Catan Q&A
// ============================================================================

const catanResponses: DemoChatQA[] = [
  {
    question: 'Come si prepara il tavolo per Catan?',
    answer:
      'Per preparare Catan:\n\n1. **Costruisci l\'isola**: Disponi i 19 esagoni terreno in modo casuale (o usa lo scenario consigliato per principianti)\n2. **Numeri**: Posiziona i gettoni numerati sugli esagoni in ordine alfabetico\n3. **Porto**: Posiziona i porti lungo la costa\n4. **Brigante**: Metti il brigante sul deserto\n5. **Risorse**: Ogni giocatore riceve risorse iniziali in base ai propri insediamenti\n6. **Primo turno**: Si piazzano 2 insediamenti e 2 strade ciascuno (ordine serpentina)',
    keywords: ['setup', 'preparare', 'tavolo', 'iniziare', 'preparazione'],
  },
  {
    question: 'Come funziona il commercio in Catan?',
    answer:
      'Il commercio in Catan ha due forme:\n\n**Commercio tra giocatori**: Puoi proporre scambi di risorse con qualsiasi giocatore durante il tuo turno. Non c\'è limite al rapporto di scambio, ma entrambi devono essere d\'accordo.\n\n**Commercio marittimo**: Puoi scambiare risorse con la "banca":\n- **Porto generico (3:1)**: 3 risorse uguali per 1 qualsiasi\n- **Porto specifico (2:1)**: 2 risorse del tipo indicato per 1 qualsiasi\n- **Senza porto (4:1)**: 4 risorse uguali per 1 qualsiasi',
    keywords: ['commercio', 'scambio', 'scambiare', 'porto', 'trading'],
  },
  {
    question: 'Come funziona il brigante?',
    answer:
      'Il brigante si attiva in due casi:\n\n1. **Tiro di 7**: Chi tira 7 deve:\n   - Tutti i giocatori con più di 7 carte ne scartano metà (arrotondando per difetto)\n   - Il giocatore che ha tirato sposta il brigante su un esagono qualsiasi\n   - Ruba 1 carta a caso da un giocatore adiacente a quell\'esagono\n\n2. **Carta Cavaliere**: Giocando una carta Cavaliere si sposta il brigante con le stesse regole\n\nL\'esagono con il brigante **non produce risorse** quando il suo numero esce.',
    keywords: ['brigante', 'ladro', 'robber', 'sette', '7'],
  },
  {
    question: 'Come si vince a Catan?',
    answer:
      'Vince il primo giocatore a raggiungere **10 punti vittoria** durante il proprio turno:\n\n- **Insediamento**: 1 punto\n- **Città**: 2 punti (sostituisce l\'insediamento)\n- **Strada più lunga**: 2 punti (minimo 5 strade consecutive)\n- **Esercito più grande**: 2 punti (minimo 3 carte Cavaliere giocate)\n- **Carte punto vittoria**: 1 punto ciascuna (dalle carte sviluppo)',
    keywords: ['vincere', 'vittoria', 'punti', 'punteggio', 'fine'],
  },
  {
    question: 'Quali risorse servono per costruire?',
    answer:
      'Costi di costruzione in Catan:\n\n| Costruzione | Legno | Mattoni | Grano | Lana | Minerale |\n|-------------|-------|---------|-------|------|----------|\n| **Strada** | 1 | 1 | - | - | - |\n| **Insediamento** | 1 | 1 | 1 | 1 | - |\n| **Città** | - | - | 2 | - | 3 |\n| **Carta Sviluppo** | - | - | 1 | 1 | 1 |',
    keywords: ['costruire', 'costo', 'risorse', 'strada', 'insediamento', 'città', 'carta'],
  },
  {
    question: 'Dove posso piazzare un insediamento?',
    answer:
      'Un insediamento può essere piazzato solo:\n\n1. **Su un incrocio libero** (dove si incontrano 2-3 esagoni)\n2. **Collegato** a una tua strada esistente\n3. **Regola della distanza**: Deve essere almeno 2 incroci lontano da qualsiasi altro insediamento/città (anche tuoi)\n\nNota: Nel piazzamento iniziale non serve la strada, ma la regola della distanza vale sempre.',
    keywords: ['piazzare', 'posizionare', 'insediamento', 'regola distanza', 'dove'],
  },
];

// ============================================================================
// Descent Q&A
// ============================================================================

const descentResponses: DemoChatQA[] = [
  {
    question: 'Come funziona il combattimento in Descent?',
    answer:
      'Il combattimento in Descent: Leggende delle Tenebre funziona così:\n\n1. **Scegli un\'arma**: Ogni eroe può equipaggiare armi con portata e danni diversi\n2. **Tira i dadi**: L\'attaccante tira i dadi indicati dall\'arma\n3. **Calcola i danni**: Somma i simboli di danno e sottrai la difesa del bersaglio\n4. **Applica effetti**: Alcuni attacchi hanno effetti speciali (stordimento, veleno, ecc.)\n5. **L\'app gestisce**: L\'app companion calcola e applica i danni automaticamente\n\nI nemici attaccano durante il turno gestito dall\'app.',
    keywords: ['combattimento', 'attacco', 'dadi', 'danno', 'difesa', 'arma'],
  },
  {
    question: 'Come si muovono gli eroi?',
    answer:
      'Il movimento in Descent segue queste regole:\n\n- Ogni eroe ha un valore di **velocità** che indica quante caselle può muoversi\n- Il movimento è ortogonale (non diagonale) sulla griglia\n- **Terreno difficile**: Costa 1 punto movimento extra per entrarci\n- **Nemici**: Non puoi attraversare caselle con nemici\n- Puoi dividere il movimento: muovere, fare un\'azione, poi continuare a muovere\n- L\'app indica il terreno e gli ostacoli sulla mappa',
    keywords: ['movimento', 'muovere', 'velocità', 'caselle', 'terreno', 'spostare'],
  },
  {
    question: 'Come funzionano le abilità degli eroi?',
    answer:
      'Ogni eroe in Descent ha:\n\n- **Abilità unica**: Un potere speciale legato al personaggio\n- **Carte abilità**: Si sbloccano durante la campagna e danno poteri aggiuntivi\n- **Fatica**: Alcune abilità costano fatica. Se esaurisci la fatica, subisci danni extra\n- **Pozioni e oggetti**: Consumabili che possono curare, potenziare o dare vantaggi\n- **Progressione**: Tra le missioni puoi migliorare le abilità e l\'equipaggiamento',
    keywords: ['abilità', 'potere', 'eroe', 'fatica', 'skill', 'personaggio'],
  },
  {
    question: 'Come funziona la campagna?',
    answer:
      'La campagna in Descent: Leggende delle Tenebre è una serie di missioni narrative:\n\n1. **Missioni collegate**: Ogni missione fa parte di una storia più ampia\n2. **Scelte narrative**: Le tue decisioni influenzano la storia e le missioni future\n3. **Progressione**: Tra le missioni guadagni esperienza e equipaggiamento\n4. **App companion**: L\'app gestisce la narrazione, i nemici e gli eventi\n5. **Salvataggio**: Puoi salvare e riprendere in qualsiasi momento\n6. **Difficoltà adattiva**: Il gioco si adatta in base al numero di giocatori',
    keywords: ['campagna', 'missione', 'storia', 'narrativa', 'app', 'salvataggio'],
  },
  {
    question: 'Qual è il ruolo dell\'Overlord?',
    answer:
      'In Descent: Leggende delle Tenebre **non c\'è un Overlord umano**!\n\nA differenza delle versioni precedenti:\n- L\'**app companion** gestisce i nemici, gli eventi e la narrazione\n- Il gioco è **completamente cooperativo**: tutti i giocatori sono eroi\n- L\'app decide quando e come i mostri attaccano e si muovono\n- Questo permette di giocare anche in **solitario** con un singolo eroe\n\nNella versione classica (Descent 2nd Edition) c\'era un giocatore Overlord, ma nella versione Leggende delle Tenebre è stato sostituito dall\'app.',
    keywords: ['overlord', 'nemici', 'mostri', 'app', 'cooperativo', 'solitario'],
  },
];

// ============================================================================
// Ticket to Ride Q&A
// ============================================================================

const ticketToRideResponses: DemoChatQA[] = [
  {
    question: 'Quali azioni posso fare nel mio turno?',
    answer:
      'Nel tuo turno puoi fare **una sola** di queste azioni:\n\n1. **Pescare carte vagone**: Prendi 2 carte dal mazzo (coperte) o dalle 5 scoperte. Se prendi una locomotiva scoperta, conta come unica carta\n2. **Reclamare una tratta**: Gioca carte vagone del colore corrispondente per costruire una tratta sulla mappa\n3. **Pescare carte destinazione**: Prendi 3 carte destinazione e ne tieni almeno 1\n\nAttenzione: non puoi combinare azioni diverse nello stesso turno!',
    keywords: ['turno', 'azione', 'azioni', 'fare', 'cosa posso'],
  },
  {
    question: 'Come funzionano le carte destinazione?',
    answer:
      'Le carte destinazione mostrano due città da collegare:\n\n- **Inizio partita**: Ricevi 3 carte e ne tieni almeno 2\n- **Durante il gioco**: Puoi pescarne 3 e tenerne almeno 1\n- **Punti**: Se colleghi le due città, guadagni i punti indicati sulla carta\n- **Penalità**: Se NON le colleghi, perdi quei punti a fine partita\n- **Segrete**: Le tieni segrete fino alla fine del gioco\n\nStrategia: bilancia il rischio! Troppe destinazioni non completate possono costarti la vittoria.',
    keywords: ['destinazione', 'destinazioni', 'città', 'collegare', 'obiettivo'],
  },
  {
    question: 'Come funzionano le carte jolly (locomotive)?',
    answer:
      'Le **locomotive** (carte arcobaleno) sono carte jolly:\n\n- Possono sostituire qualsiasi colore quando reclami una tratta\n- Se ne prendi una **scoperta**, quella è l\'unica carta del turno\n- Se ne peschi una dal **mazzo coperto**, puoi comunque pescare la seconda carta\n- Alcune tratte grigie o i tunnel possono richiedere locomotive\n- Le tratte ferry richiedono un numero minimo di locomotive\n\nSono molto potenti ma costose se prese scoperte!',
    keywords: ['jolly', 'locomotiva', 'locomotive', 'arcobaleno', 'wild'],
  },
  {
    question: 'Come si calcolano i punti?',
    answer:
      'Il punteggio in Ticket to Ride si calcola così:\n\n**Punti positivi:**\n- Tratte costruite: da 1 punto (1 vagone) a 15 punti (6 vagoni)\n- Destinazioni completate: punti indicati sulla carta\n- Tratta più lunga: 10 punti bonus (se applicabile)\n\n**Punti negativi:**\n- Destinazioni non completate: sottrai i punti indicati\n\n| Vagoni | Punti |\n|--------|-------|\n| 1 | 1 |\n| 2 | 2 |\n| 3 | 4 |\n| 4 | 7 |\n| 5 | 10 |\n| 6 | 15 |',
    keywords: ['punti', 'punteggio', 'calcolo', 'vittoria', 'fine partita'],
  },
  {
    question: 'Quando finisce la partita?',
    answer:
      'La partita finisce quando un giocatore ha **2 o meno vagoni** rimanenti alla fine del suo turno.\n\n1. Quel giocatore annuncia l\'ultimo giro\n2. Ogni altro giocatore (incluso lui) fa ancora **un ultimo turno**\n3. Si contano i punti finali:\n   - Punti tratte (già segnati)\n   - + Destinazioni completate\n   - - Destinazioni non completate\n   - + Bonus tratta più lunga (10 punti)\n\nVince chi ha più punti!',
    keywords: ['fine', 'finisce', 'ultimo', 'vagoni', 'rimanenti', 'termine'],
  },
  {
    question: 'Come funzionano le tratte doppie?',
    answer:
      'Alcune città sono collegate da **tratte doppie** (due percorsi paralleli):\n\n- In partite a **2-3 giocatori**: solo UNA delle due tratte può essere usata\n- In partite a **4-5 giocatori**: entrambe le tratte possono essere reclamate\n- Lo stesso giocatore **non può** reclamare entrambe le tratte tra le stesse città\n- Le tratte doppie offrono percorsi alternativi importanti nelle partite con più giocatori',
    keywords: ['doppia', 'doppie', 'parallele', 'tratta', 'percorso'],
  },
];

// ============================================================================
// Pandemic Q&A
// ============================================================================

const pandemicResponses: DemoChatQA[] = [
  {
    question: 'Come funzionano le epidemie?',
    answer:
      'Quando peschi una carta **Epidemia** succedono 3 cose in ordine:\n\n1. **Intensificazione**: Sposta l\'indicatore velocità infezione di 1 posizione\n2. **Infezione**: Pesca la carta in fondo al mazzo Infezione → metti 3 cubi malattia su quella città\n3. **Rimescola**: Prendi tutte le carte Infezione scartate, mischiale e rimettile IN CIMA al mazzo\n\nQuesto significa che le città già infettate verranno colpite di nuovo! Il numero di epidemie nel mazzo determina la difficoltà (4 = facile, 5 = medio, 6 = difficile).',
    keywords: ['epidemia', 'epidemie', 'infezione', 'intensificazione'],
  },
  {
    question: 'Quali sono i ruoli disponibili?',
    answer:
      'Pandemic ha 7 ruoli, ognuno con un\'abilità unica:\n\n- **Medico** 🏥: Cura TUTTI i cubi di un colore in una città con 1 sola azione. Se la cura è scoperta, rimuove automaticamente\n- **Scienziato** 🔬: Scopre una cura con solo 4 carte (invece di 5)\n- **Ricercatore** 📚: Può dare qualsiasi carta a un giocatore nella stessa città\n- **Esperto Operativo** 🏗️: Costruisce stazioni di ricerca senza scartare carte\n- **Coordinatore** 📋: Può muovere le pedine degli altri giocatori\n- **Specialista Quarantena** 🛡️: Previene piazzamento cubi nella sua città e adiacenti\n- **Pianificatore** 📅: Può tenere una carta Evento extra',
    keywords: ['ruolo', 'ruoli', 'personaggio', 'abilità', 'medico', 'scienziato'],
  },
  {
    question: 'Come si scopre una cura?',
    answer:
      'Per scoprire una cura:\n\n1. Vai in una **stazione di ricerca**\n2. Scarta **5 carte città** dello stesso colore della malattia\n3. Posiziona il marcatore cura sul tabellone\n\n**Effetti della cura:**\n- L\'azione "Cura malattia" rimuove TUTTI i cubi di quel colore dalla città (invece di 1)\n- Il Medico li rimuove automaticamente senza usare azioni\n\n**Eradicazione**: Se tutti i cubi di un colore curato vengono rimossi dal tabellone, la malattia è eradicata e non si piazzano più cubi di quel colore.',
    keywords: ['cura', 'curare', 'scoprire', 'eradicazione', 'guarire'],
  },
  {
    question: 'Quante azioni ho per turno?',
    answer:
      'Ogni giocatore ha **4 azioni** per turno, scelte tra:\n\n**Movimento:**\n- **Guida/Traghetto**: Muovi in una città adiacente\n- **Volo diretto**: Scarta una carta per volare in quella città\n- **Volo charter**: Scarta la carta della tua città per volare ovunque\n- **Volo navetta**: Muovi tra stazioni di ricerca\n\n**Altre azioni:**\n- **Cura malattia**: Rimuovi 1 cubo dalla tua città\n- **Costruisci stazione**: Scarta la carta della tua città\n- **Condividi conoscenza**: Dai/ricevi la carta della città in cui ti trovi\n- **Scopri cura**: Scarta 5 carte dello stesso colore in una stazione',
    keywords: ['azione', 'azioni', 'turno', 'movimento', 'cosa posso fare'],
  },
  {
    question: 'Quando si perde a Pandemic?',
    answer:
      'Si perde in **3 modi**:\n\n1. **Focolai**: Se l\'indicatore focolai raggiunge 8. Un focolaio avviene quando devi aggiungere un cubo a una città che ne ha già 3 (si diffonde alle città adiacenti)\n\n2. **Cubi esauriti**: Se devi piazzare un cubo malattia ma non ce ne sono più di quel colore nella riserva\n\n3. **Carte esaurite**: Se un giocatore non può pescare 2 carte dal mazzo Giocatore\n\n**Si vince** SOLO scoprendo le cure di tutte e 4 le malattie. Non serve eradicarle!',
    keywords: ['perdere', 'sconfitta', 'focolaio', 'fine', 'vincere', 'condizione'],
  },
  {
    question: 'Come funzionano i focolai?',
    answer:
      'Un **focolaio** (outbreak) avviene quando una città con 3 cubi ne riceve un altro:\n\n1. L\'indicatore focolai avanza di 1 (8 = game over!)\n2. Ogni città adiacente riceve 1 cubo del colore in questione\n3. Se una città adiacente ha già 3 cubi → **focolaio a catena**!\n4. Una città può subire focolaio solo 1 volta per catena\n\nI focolai a catena sono devastanti e possono far avanzare l\'indicatore di molte posizioni in un colpo solo. La prevenzione è fondamentale!',
    keywords: ['focolaio', 'focolai', 'outbreak', 'catena', 'esplosione', 'diffusione'],
  },
];

// ============================================================================
// All Game Chats
// ============================================================================

export const DEMO_GAME_CHATS: DemoGameChat[] = [
  {
    gameId: DEMO_GAME_IDS.catan,
    gameTitle: 'Catan',
    responses: catanResponses,
    followUpQuestions: [
      'Come si prepara il tavolo per Catan?',
      'Come funziona il commercio?',
      'Quando si attiva il brigante?',
      'Quali risorse servono per costruire?',
      'Come si vince a Catan?',
    ],
  },
  {
    gameId: DEMO_GAME_IDS.descent,
    gameTitle: 'Descent: Leggende delle Tenebre',
    responses: descentResponses,
    followUpQuestions: [
      'Come funziona il combattimento?',
      'Come si muovono gli eroi?',
      'Quali abilità hanno gli eroi?',
      'Come funziona la campagna?',
      'C\'è un Overlord nel gioco?',
    ],
  },
  {
    gameId: DEMO_GAME_IDS.ticketToRide,
    gameTitle: 'Ticket to Ride',
    responses: ticketToRideResponses,
    followUpQuestions: [
      'Cosa posso fare nel mio turno?',
      'Come funzionano le destinazioni?',
      'A cosa servono le locomotive?',
      'Come si calcolano i punti?',
      'Quando finisce la partita?',
    ],
  },
  {
    gameId: DEMO_GAME_IDS.pandemic,
    gameTitle: 'Pandemic',
    responses: pandemicResponses,
    followUpQuestions: [
      'Come funzionano le epidemie?',
      'Quali ruoli ci sono?',
      'Come si scopre una cura?',
      'Quante azioni ho per turno?',
      'Quando si perde?',
    ],
  },
];

// ============================================================================
// Helpers
// ============================================================================

/** Get chat data for a specific game */
export function getDemoGameChat(gameId: string): DemoGameChat | undefined {
  return DEMO_GAME_CHATS.find(c => c.gameId === gameId);
}

/** Find a matching response by keyword search */
export function findDemoResponse(gameId: string, query: string): DemoChatQA | undefined {
  const chat = getDemoGameChat(gameId);
  if (!chat) return undefined;

  const normalizedQuery = query.toLowerCase();
  return chat.responses.find(r =>
    r.keywords.some(kw => normalizedQuery.includes(kw))
  );
}
