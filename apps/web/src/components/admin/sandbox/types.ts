export interface RetrievedChunk {
  id: string;
  score: number;
  text: string;
  page: number;
  chunkIndex: number;
  pdfName: string;
  used: boolean;
}

export interface PipelineTraceStep {
  name: string;
  durationMs: number;
  details: Record<string, string | number>;
}

export interface PipelineTrace {
  steps: PipelineTraceStep[];
  totalDurationMs: number;
}

export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  metadata?: {
    latencyMs: number;
    chunkCount: number;
    avgConfidence: number;
    chunks: RetrievedChunk[];
    trace: PipelineTrace;
  };
}

export interface AutoTestResult {
  question: string;
  answer: string;
  confidence: number;
  passed: boolean;
  latencyMs: number;
}
