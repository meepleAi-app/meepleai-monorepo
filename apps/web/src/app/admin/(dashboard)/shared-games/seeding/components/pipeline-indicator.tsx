'use client';

interface PipelineIndicatorProps {
  gameDataStatus: number;
  hasUploadedPdf: boolean;
  isRagReady: boolean;
}

const COMPLETE_STATUS = 5;

const stages = [
  { key: 'enrich', label: 'Enriched' },
  { key: 'pdf', label: 'PDF' },
  { key: 'rag', label: 'RAG' },
] as const;

export function PipelineIndicator({
  gameDataStatus,
  hasUploadedPdf,
  isRagReady,
}: PipelineIndicatorProps) {
  const enrichComplete = gameDataStatus === COMPLETE_STATUS;
  const pdfComplete = hasUploadedPdf;
  const ragComplete = isRagReady;

  const stageStatus = [enrichComplete, pdfComplete, ragComplete];

  return (
    <div
      className="flex items-center gap-1"
      title={`Enriched: ${enrichComplete ? 'Yes' : 'No'} | PDF: ${pdfComplete ? 'Yes' : 'No'} | RAG: ${ragComplete ? 'Yes' : 'No'}`}
    >
      {stages.map((stage, i) => (
        <div key={stage.key} className="flex items-center gap-1">
          <div
            className={`h-2 w-2 rounded-full ${
              stageStatus[i] ? 'bg-emerald-500' : 'bg-slate-300 dark:bg-slate-600'
            }`}
          />
          {i < stages.length - 1 && (
            <div
              className={`h-px w-3 ${
                stageStatus[i] ? 'bg-emerald-400' : 'bg-slate-300 dark:bg-slate-600'
              }`}
            />
          )}
        </div>
      ))}
    </div>
  );
}
