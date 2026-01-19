/**
 * GameFAQTab Component - FAQ Tab Content
 *
 * Displays frequently asked questions using Shadcn Accordion.
 * Issue #2028: Backend FAQ system implementation complete.
 *
 * Issue #1841 (PAGE-005)
 */

'use client';

import React, { useEffect, useState } from 'react';

import { HelpCircle, ThumbsUp, Info } from 'lucide-react';

import { Spinner } from '@/components/loading';
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/data-display/accordion';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { api, type GameFAQ } from '@/lib/api';

// ============================================================================
// Types
// ============================================================================

export interface GameFAQTabProps {
  /** Game ID for backend query */
  gameId: string;
  /** Game title for context */
  gameTitle: string;
}

// ============================================================================
// Component
// ============================================================================

export function GameFAQTab({ gameId, gameTitle }: GameFAQTabProps) {
  const [faqs, setFaqs] = useState<GameFAQ[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [upvoting, setUpvoting] = useState<string | null>(null);

  // Fetch FAQs from backend
  useEffect(() => {
    const fetchFAQs = async () => {
      try {
        setLoading(true);
        setError(null);
        const result = await api.games.getFAQs(gameId, 10, 0);
        setFaqs(result.faqs);
      } catch (err) {
        console.error('Failed to fetch FAQs:', err);
        setError('Errore nel caricamento delle FAQ. Riprova più tardi.');
      } finally {
        setLoading(false);
      }
    };

    fetchFAQs();
  }, [gameId]);

  // Handle upvote
  const handleUpvote = async (faqId: string) => {
    try {
      setUpvoting(faqId);
      const updatedFAQ = await api.games.upvoteFAQ(faqId);

      // Update local state
      setFaqs(prev => prev.map(faq => (faq.id === faqId ? updatedFAQ : faq)));
    } catch (err) {
      console.error('Failed to upvote FAQ:', err);
    } finally {
      setUpvoting(null);
    }
  };

  return (
    <div className="space-y-6" data-testid="faq-tab">
      {/* Loading State */}
      {loading && (
        <div className="flex justify-center py-12">
          <Spinner size="lg" />
        </div>
      )}

      {/* Error State */}
      {error && !loading && (
        <Alert variant="destructive">
          <HelpCircle className="h-4 w-4" />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {/* Empty State */}
      {!loading && !error && faqs.length === 0 && (
        <Alert>
          <Info className="h-4 w-4" />
          <AlertDescription>
            <strong>Nessuna FAQ disponibile</strong> per {gameTitle}.
            <br />
            Le FAQ verranno aggiunte dagli amministratori nel tempo.
          </AlertDescription>
        </Alert>
      )}

      {/* FAQ List */}
      {!loading && !error && faqs.length > 0 && (
        <Accordion type="single" collapsible className="w-full">
          {faqs.map(faq => (
            <AccordionItem key={faq.id} value={faq.id}>
              <AccordionTrigger className="text-left font-medium hover:no-underline">
                <div className="flex items-center gap-3 w-full">
                  <span className="flex-1">{faq.question}</span>
                  <Button
                    variant="ghost"
                    size="sm"
                    className="shrink-0"
                    onClick={e => {
                      e.stopPropagation();
                      handleUpvote(faq.id);
                    }}
                    disabled={upvoting === faq.id}
                  >
                    <ThumbsUp className="h-4 w-4 mr-1" />
                    {faq.upvotes}
                  </Button>
                </div>
              </AccordionTrigger>
              <AccordionContent className="text-muted-foreground leading-relaxed">
                {faq.answer}
              </AccordionContent>
            </AccordionItem>
          ))}
        </Accordion>
      )}
    </div>
  );
}
