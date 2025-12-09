/**
 * GameFAQTab Component - FAQ Tab Content
 *
 * Displays frequently asked questions using Shadcn Accordion.
 * Currently uses placeholder/mock data.
 *
 * TODO: Issue #2028 - Backend FAQ system implementation
 * via GameManagement.Application.Queries.GetGameFAQsQuery
 *
 * Issue #1841 (PAGE-005)
 */

'use client';

import React from 'react';
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/accordion';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { HelpCircle } from 'lucide-react';

// ============================================================================
// Types
// ============================================================================

interface FAQ {
  id: string;
  question: string;
  answer: string;
}

export interface GameFAQTabProps {
  /** Game ID for future backend query */
  gameId: string;
  /** Game title for context */
  gameTitle: string;
}

// ============================================================================
// Mock Data (Placeholder)
// ============================================================================

function getMockFAQs(gameTitle: string): FAQ[] {
  return [
    {
      id: 'faq-1',
      question: 'Come si vince al gioco?',
      answer: `Le condizioni di vittoria per ${gameTitle} verranno caricate dal sistema FAQ. Attualmente questo è un placeholder. In futuro, il backend fornirà FAQ specifiche per ogni gioco tramite query dedicate.`,
    },
    {
      id: 'faq-2',
      question: 'Quali sono le fasi di gioco?',
      answer:
        'Le fasi di gioco saranno specificate nel sistema FAQ. Per ora, questa è una risposta placeholder che verrà sostituita con contenuti reali caricati dal backend.',
    },
    {
      id: 'faq-3',
      question: 'Come si svolgono i turni?',
      answer:
        'La spiegazione dei turni di gioco verrà fornita dal sistema FAQ una volta implementato. Attualmente questo è un esempio di come verranno visualizzate le risposte.',
    },
    {
      id: 'faq-4',
      question: 'Ci sono espansioni disponibili?',
      answer:
        'Le informazioni sulle espansioni saranno integrate dal backend. Per ora, questa sezione mostra come verranno organizzate le FAQ nel layout accordion.',
    },
  ];
}

// ============================================================================
// Component
// ============================================================================

export function GameFAQTab({ gameId, gameTitle }: GameFAQTabProps) {
  const faqs = getMockFAQs(gameTitle);

  return (
    <div className="space-y-6" data-testid="faq-tab">
      {/* Development Notice */}
      <Alert>
        <HelpCircle className="h-4 w-4" />
        <AlertDescription>
          <strong>Nota:</strong> Le FAQ sono attualmente placeholder. In futuro, verranno caricate
          dinamicamente dal backend per ogni gioco.
        </AlertDescription>
      </Alert>

      {/* FAQ Accordion */}
      <Accordion type="single" collapsible className="w-full">
        {faqs.map(faq => (
          <AccordionItem key={faq.id} value={faq.id}>
            <AccordionTrigger className="text-left font-medium hover:no-underline">
              {faq.question}
            </AccordionTrigger>
            <AccordionContent className="text-muted-foreground leading-relaxed">
              {faq.answer}
            </AccordionContent>
          </AccordionItem>
        ))}
      </Accordion>

      {/* Backend TODO Comment */}
      {/* TODO: Issue #2028 - Replace mock FAQ data with backend query:
          const { data: faqs } = useGameFAQs(gameId);
          Backend Query: GameManagement.Application.Queries.GetGameFAQsQuery
          Expected response: Array<{ id: string; question: string; answer: string; }>
      */}
    </div>
  );
}
