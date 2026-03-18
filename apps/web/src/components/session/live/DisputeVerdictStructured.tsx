/**
 * DisputeVerdictStructured
 *
 * Arbitro v2 — Task 15
 *
 * Shows a structured AI verdict with ruling party indicator,
 * confidence badge, citation quote block, and reasoning.
 */

'use client';

import { CheckCircle2, HelpCircle, Scale } from 'lucide-react';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface StructuredVerdict {
  rulingFor: 'Initiator' | 'Respondent' | 'Ambiguous';
  reasoning: string;
  citation: string | null;
  confidence: 'High' | 'Medium' | 'Low';
}

export interface DisputeVerdictStructuredProps {
  verdict: StructuredVerdict;
  initiatorName: string;
  respondentName: string | null;
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

const confidenceConfig = {
  High: { label: 'Alta', className: 'bg-green-100 text-green-800 border-green-300' },
  Medium: { label: 'Media', className: 'bg-amber-100 text-amber-800 border-amber-300' },
  Low: { label: 'Bassa', className: 'bg-red-100 text-red-800 border-red-300' },
} as const;

function getRulingLabel(
  rulingFor: StructuredVerdict['rulingFor'],
  initiatorName: string,
  respondentName: string | null
): string {
  switch (rulingFor) {
    case 'Initiator':
      return `A favore di ${initiatorName}`;
    case 'Respondent':
      return respondentName ? `A favore di ${respondentName}` : 'A favore del rispondente';
    case 'Ambiguous':
      return 'Ambiguo — nessuna parte prevale';
  }
}

function getRulingIcon(rulingFor: StructuredVerdict['rulingFor']) {
  switch (rulingFor) {
    case 'Initiator':
    case 'Respondent':
      return <CheckCircle2 className="h-5 w-5 text-green-600" />;
    case 'Ambiguous':
      return <HelpCircle className="h-5 w-5 text-amber-600" />;
  }
}

// ─── Component ────────────────────────────────────────────────────────────────

export function DisputeVerdictStructured({
  verdict,
  initiatorName,
  respondentName,
}: DisputeVerdictStructuredProps) {
  const conf = confidenceConfig[verdict.confidence];

  return (
    <div className="border-2 border-amber-400 bg-white/70 backdrop-blur-md rounded-xl p-4 space-y-3">
      {/* Header */}
      <div className="flex items-center gap-2">
        <Scale className="h-5 w-5 text-amber-600" />
        <h3 className="font-quicksand font-bold text-amber-900 text-base">Verdetto strutturato</h3>
      </div>

      {/* Ruling indicator */}
      <div className="flex items-center gap-2 bg-gray-50 rounded-lg px-3 py-2">
        {getRulingIcon(verdict.rulingFor)}
        <span className="text-sm font-nunito font-semibold text-gray-800">
          {getRulingLabel(verdict.rulingFor, initiatorName, respondentName)}
        </span>
      </div>

      {/* Confidence badge */}
      <div className="flex items-center gap-2">
        <span className="text-xs font-nunito text-gray-500">Confidenza:</span>
        <span
          className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-nunito font-medium border ${conf.className}`}
          data-testid="confidence-badge"
        >
          {conf.label}
        </span>
      </div>

      {/* Citation */}
      {verdict.citation && (
        <blockquote className="border-l-4 border-amber-300 bg-amber-50 px-3 py-2 rounded-r-lg">
          <p className="text-sm text-amber-900 font-nunito italic">{verdict.citation}</p>
        </blockquote>
      )}

      {/* Reasoning */}
      <div className="space-y-1">
        <p className="text-xs font-nunito font-medium text-gray-500">Ragionamento</p>
        <p className="text-sm text-gray-800 font-nunito leading-relaxed">{verdict.reasoning}</p>
      </div>
    </div>
  );
}
