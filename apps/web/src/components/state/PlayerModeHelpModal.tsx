/**
 * Player Mode Help Modal - Issue #2475
 *
 * Comprehensive help dialog for Player Mode feature.
 *
 * Features:
 * - Multi-tab FAQ and guide
 * - Visual examples and explanations
 * - Confidence scoring guide
 * - How to interpret suggestions
 */

'use client';

import { BookOpen, Brain, HelpCircle, Lightbulb, Target } from 'lucide-react';

import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';

// ========== Component Props ==========

export interface PlayerModeHelpModalProps {
  /** Trigger button variant */
  variant?: 'default' | 'outline' | 'ghost';
  /** Button size */
  size?: 'default' | 'sm' | 'lg' | 'icon';
  /** Custom trigger element */
  children?: React.ReactNode;
}

// ========== Main Component ==========

export function PlayerModeHelpModal({
  variant = 'ghost',
  size = 'icon',
  children,
}: PlayerModeHelpModalProps) {
  return (
    <Dialog>
      <DialogTrigger asChild>
        {children || (
          <Button variant={variant} size={size} className="gap-2">
            <HelpCircle className="h-4 w-4" />
            {size !== 'icon' && <span>Aiuto</span>}
          </Button>
        )}
      </DialogTrigger>
      <DialogContent className="max-w-3xl max-h-[80vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Brain className="h-5 w-5 text-primary" />
            Guida Player Mode
          </DialogTitle>
          <DialogDescription>
            Impara a utilizzare al meglio i suggerimenti AI per migliorare il tuo gioco
          </DialogDescription>
        </DialogHeader>

        <Tabs defaultValue="overview" className="mt-4">
          <TabsList className="grid w-full grid-cols-4">
            <TabsTrigger value="overview">
              <Target className="h-4 w-4 mr-2" />
              Panoramica
            </TabsTrigger>
            <TabsTrigger value="confidence">
              <Brain className="h-4 w-4 mr-2" />
              Confidenza
            </TabsTrigger>
            <TabsTrigger value="interpretation">
              <Lightbulb className="h-4 w-4 mr-2" />
              Interpretazione
            </TabsTrigger>
            <TabsTrigger value="faq">
              <BookOpen className="h-4 w-4 mr-2" />
              FAQ
            </TabsTrigger>
          </TabsList>

          {/* Overview Tab */}
          <TabsContent value="overview" className="space-y-4">
            <div>
              <h3 className="text-lg font-semibold mb-2">Come funziona Player Mode</h3>
              <p className="text-sm text-muted-foreground mb-4">
                Player Mode è un assistente AI che analizza lo stato corrente della tua partita e
                suggerisce la mossa migliore da effettuare.
              </p>

              <div className="space-y-3">
                <div className="rounded-lg border p-4">
                  <h4 className="font-semibold mb-2">1. Analisi dello stato gioco</h4>
                  <p className="text-sm text-muted-foreground">
                    L&apos;AI esamina risorse, posizioni, turno corrente e obiettivi per
                    comprendere la situazione strategica.
                  </p>
                </div>

                <div className="rounded-lg border p-4">
                  <h4 className="font-semibold mb-2">2. Ricerca regole strategiche (RAG)</h4>
                  <p className="text-sm text-muted-foreground">
                    Il sistema cerca nella knowledge base le regole e strategie più rilevanti per
                    la tua situazione.
                  </p>
                </div>

                <div className="rounded-lg border p-4">
                  <h4 className="font-semibold mb-2">3. Generazione suggerimenti</h4>
                  <p className="text-sm text-muted-foreground">
                    L&apos;AI elabora una mossa primaria (confidenza alta) e 2-3 alternative
                    (confidenza media-bassa) con spiegazioni dettagliate.
                  </p>
                </div>

                <div className="rounded-lg border p-4">
                  <h4 className="font-semibold mb-2">4. Contesto strategico</h4>
                  <p className="text-sm text-muted-foreground">
                    Ogni suggerimento include il contesto strategico generale per aiutarti a
                    comprendere la fase di gioco e gli obiettivi a lungo termine.
                  </p>
                </div>
              </div>
            </div>
          </TabsContent>

          {/* Confidence Tab */}
          <TabsContent value="confidence" className="space-y-4">
            <div>
              <h3 className="text-lg font-semibold mb-2">Come interpretare la confidenza</h3>
              <p className="text-sm text-muted-foreground mb-4">
                Il punteggio di confidenza indica quanto l&apos;AI è sicura del suggerimento. È
                calcolato con tre fattori principali:
              </p>

              <div className="space-y-3">
                <div className="rounded-lg border p-4 bg-green-50 dark:bg-green-950/20">
                  <div className="flex items-center gap-2 mb-2">
                    <div className="h-2 w-16 bg-green-500 rounded-full" />
                    <span className="font-semibold text-green-700 dark:text-green-400">
                      Alta (80-100%)
                    </span>
                  </div>
                  <p className="text-sm text-muted-foreground">
                    Mossa fortemente consigliata. L&apos;AI ha trovato regole chiare e il contesto
                    è ben compreso. Probabilità alta di successo.
                  </p>
                </div>

                <div className="rounded-lg border p-4 bg-yellow-50 dark:bg-yellow-950/20">
                  <div className="flex items-center gap-2 mb-2">
                    <div className="h-2 w-16 bg-yellow-500 rounded-full" />
                    <span className="font-semibold text-yellow-700 dark:text-yellow-400">
                      Media (50-80%)
                    </span>
                  </div>
                  <p className="text-sm text-muted-foreground">
                    Mossa valida ma con rischi. Considera le alternative e valuta il contesto di
                    gioco prima di procedere.
                  </p>
                </div>

                <div className="rounded-lg border p-4 bg-red-50 dark:bg-red-950/20">
                  <div className="flex items-center gap-2 mb-2">
                    <div className="h-2 w-16 bg-red-500 rounded-full" />
                    <span className="font-semibold text-red-700 dark:text-red-400">
                      Bassa (&lt;50%)
                    </span>
                  </div>
                  <p className="text-sm text-muted-foreground">
                    Mossa incerta. L&apos;AI ha informazioni limitate o il contesto è ambiguo.
                    Valuta con cautela e considera alternative.
                  </p>
                </div>
              </div>

              <div className="rounded-lg border p-4 mt-4 bg-muted/50">
                <h4 className="font-semibold mb-2">Formula di calcolo:</h4>
                <ul className="text-sm space-y-1 text-muted-foreground">
                  <li>• <span className="font-medium">40%</span> - Qualità ricerca RAG (regole trovate rilevanti)</li>
                  <li>• <span className="font-medium">40%</span> - Coerenza risposta LLM (logica interna)</li>
                  <li>• <span className="font-medium">20%</span> - Completezza stato gioco (dati forniti)</li>
                </ul>
              </div>
            </div>
          </TabsContent>

          {/* Interpretation Tab */}
          <TabsContent value="interpretation" className="space-y-4">
            <div>
              <h3 className="text-lg font-semibold mb-2">Come leggere i suggerimenti</h3>
              <p className="text-sm text-muted-foreground mb-4">
                Ogni suggerimento AI contiene diverse sezioni per guidarti nella decisione:
              </p>

              <div className="space-y-3">
                <div className="rounded-lg border p-4">
                  <h4 className="font-semibold mb-2">Azione Suggerita</h4>
                  <p className="text-sm text-muted-foreground">
                    Descrizione chiara e specifica della mossa da effettuare (es: &quot;Raccogli 2
                    legno dallo spazio foresta&quot;).
                  </p>
                </div>

                <div className="rounded-lg border p-4">
                  <h4 className="font-semibold mb-2">Rationale</h4>
                  <p className="text-sm text-muted-foreground">
                    Spiegazione del perché questa mossa è consigliata in base allo stato attuale e
                    alle regole strategiche.
                  </p>
                </div>

                <div className="rounded-lg border p-4">
                  <h4 className="font-semibold mb-2">Risultato Atteso</h4>
                  <p className="text-sm text-muted-foreground">
                    Cosa succederà dopo aver effettuato la mossa (guadagni, perdite, cambiamenti di
                    stato).
                  </p>
                </div>

                <div className="rounded-lg border p-4">
                  <h4 className="font-semibold mb-2">Contesto Strategico</h4>
                  <p className="text-sm text-muted-foreground">
                    Visione d&apos;insieme della fase di gioco e obiettivi a lungo termine per
                    orientare le decisioni future.
                  </p>
                </div>

                <div className="rounded-lg border p-4">
                  <h4 className="font-semibold mb-2">Mosse Alternative</h4>
                  <p className="text-sm text-muted-foreground">
                    2-3 opzioni alternative con confidenza inferiore ma comunque valide. Utili se
                    la mossa primaria non è applicabile o vuoi esplorare strategie diverse.
                  </p>
                </div>
              </div>
            </div>
          </TabsContent>

          {/* FAQ Tab */}
          <TabsContent value="faq" className="space-y-4">
            <div>
              <h3 className="text-lg font-semibold mb-2">Domande Frequenti</h3>

              <div className="space-y-3">
                <div className="rounded-lg border p-4">
                  <h4 className="font-semibold mb-2">
                    ❓ Posso richiedere più suggerimenti per lo stesso stato?
                  </h4>
                  <p className="text-sm text-muted-foreground">
                    Sì! Ogni richiesta genera nuovi suggerimenti. Tuttavia, i risultati sono
                    cachati per 1 ora, quindi richiedere immediatamente di nuovo restituirà lo
                    stesso suggerimento.
                  </p>
                </div>

                <div className="rounded-lg border p-4">
                  <h4 className="font-semibold mb-2">
                    ❓ Cosa succede se ignoro un suggerimento?
                  </h4>
                  <p className="text-sm text-muted-foreground">
                    Nulla di negativo! Ignorare serve solo a nascondere il suggerimento corrente.
                    Puoi richiederne uno nuovo in qualsiasi momento.
                  </p>
                </div>

                <div className="rounded-lg border p-4">
                  <h4 className="font-semibold mb-2">
                    ❓ L&apos;AI tiene traccia delle mie scelte?
                  </h4>
                  <p className="text-sm text-muted-foreground">
                    Attualmente no. In futuro, il sistema registrerà feedback (thumbs up/down) per
                    migliorare i suggerimenti futuri.
                  </p>
                </div>

                <div className="rounded-lg border p-4">
                  <h4 className="font-semibold mb-2">
                    ❓ Perché la confidenza è bassa?
                  </h4>
                  <p className="text-sm text-muted-foreground">
                    Confidenza bassa indica: (1) Informazioni limitate sullo stato gioco, (2)
                    Regole strategiche non trovate, (3) Situazione ambigua. Prova a fornire più
                    dettagli sullo stato o considera le mosse alternative.
                  </p>
                </div>

                <div className="rounded-lg border p-4">
                  <h4 className="font-semibold mb-2">
                    ❓ I tempi di risposta sono normali?
                  </h4>
                  <p className="text-sm text-muted-foreground">
                    Sì. L&apos;AI impiega 1-3 secondi per analizzare (ricerca RAG + generazione
                    LLM). Tempi più lunghi possono indicare carico server elevato o stato gioco
                    complesso.
                  </p>
                </div>

                <div className="rounded-lg border p-4">
                  <h4 className="font-semibold mb-2">
                    ❓ Funziona con tutti i giochi da tavolo?
                  </h4>
                  <p className="text-sm text-muted-foreground">
                    Player Mode è ottimizzato per giochi con regole ben definite (Catan, Ticket to
                    Ride, etc.). Giochi con meccaniche complesse o fortemente narrative potrebbero
                    avere suggerimenti meno accurati.
                  </p>
                </div>
              </div>
            </div>
          </TabsContent>
        </Tabs>
      </DialogContent>
    </Dialog>
  );
}
