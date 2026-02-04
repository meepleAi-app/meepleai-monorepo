'use client';

import { useState } from 'react';

import { AlertCircle, Clock, DollarSign, Loader2, Zap } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { Label } from '@/components/ui/primitives/label';
import { RadioGroup, RadioGroupItem } from '@/components/ui/primitives/radio-group';
import { Textarea } from '@/components/ui/primitives/textarea';



type AgentSearchStrategy = 'RetrievalOnly' | 'SingleModel' | 'MultiModelConsensus';

interface TokenUsage {
  promptTokens: number;
  completionTokens: number;
  totalTokens: number;
  embeddingTokens: number;
}

interface CostBreakdown {
  embeddingCost: number;
  vectorSearchCost: number;
  llmCost: number;
  totalCost: number;
  provider: string;
  modelUsed?: string;
}

interface CodeChunk {
  filePath: string;
  startLine: number;
  endLine: number;
  codePreview: string;
  relevanceScore: number;
  boundedContext: string;
  chunkIndex: number;
}

interface AgentChatResponse {
  strategy: AgentSearchStrategy;
  strategyDescription: string;
  answer?: string;
  retrievedChunks: CodeChunk[];
  tokenUsage: TokenUsage;
  costBreakdown: CostBreakdown;
  latencyMs: number;
  sessionId: string;
  timestamp: string;
}

export function AgentChatPOC() {
  const [question, setQuestion] = useState('');
  const [strategy, setStrategy] = useState<AgentSearchStrategy>('RetrievalOnly');
  const [response, setResponse] = useState<AgentChatResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async () => {
    if (!question.trim()) {
      setError('Please enter a question');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const res = await fetch('/api/v1/agents/chat/ask', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          question,
          strategy,
        }),
      });

      if (!res.ok) {
        throw new Error(`HTTP ${res.status}: ${res.statusText}`);
      }

      const data: AgentChatResponse = await res.json();
      setResponse(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to get response');
    } finally {
      setLoading(false);
    }
  };

  const getStrategyBadge = (strat: AgentSearchStrategy) => {
    switch (strat) {
      case 'RetrievalOnly':
        return <Badge variant="outline" className="bg-green-50 text-green-700 border-green-200">$0.00 • ~300ms</Badge>;
      case 'SingleModel':
        return <Badge variant="outline" className="bg-blue-50 text-blue-700 border-blue-200">$0-0.0009 • ~2-5s</Badge>;
      case 'MultiModelConsensus':
        return <Badge variant="outline" className="bg-orange-50 text-orange-700 border-orange-200">~$0.027 • ~5-10s</Badge>;
      default:
        return null;
    }
  };

  return (
    <div className="w-full max-w-5xl mx-auto space-y-6 p-6">
      <Card>
        <CardHeader>
          <CardTitle>Agent Chat POC - Strategy Testing</CardTitle>
          <CardDescription>
            Test 3 RAG strategies with full token and cost tracking
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          {/* Question Input */}
          <div className="space-y-2">
            <Label htmlFor="question">Question</Label>
            <Textarea
              id="question"
              placeholder="Ask a question about the game rules..."
              value={question}
              onChange={(e: React.ChangeEvent<HTMLTextAreaElement>) => setQuestion(e.target.value)}
              rows={3}
              disabled={loading}
            />
          </div>

          {/* Strategy Selection */}
          <div className="space-y-3">
            <Label>Search Strategy</Label>
            <RadioGroup value={strategy} onValueChange={(v: string) => setStrategy(v as AgentSearchStrategy)}>
              <div className="flex items-center justify-between space-x-2 p-3 border rounded-lg hover:bg-gray-50">
                <div className="flex items-center space-x-2">
                  <RadioGroupItem value="RetrievalOnly" id="retrieval" />
                  <Label htmlFor="retrieval" className="cursor-pointer">
                    <div className="font-semibold">Retrieval Only</div>
                    <div className="text-xs text-gray-500">No LLM, raw chunks</div>
                  </Label>
                </div>
                {getStrategyBadge('RetrievalOnly')}
              </div>

              <div className="flex items-center justify-between space-x-2 p-3 border rounded-lg hover:bg-gray-50">
                <div className="flex items-center space-x-2">
                  <RadioGroupItem value="SingleModel" id="single" />
                  <Label htmlFor="single" className="cursor-pointer">
                    <div className="font-semibold">Single Model</div>
                    <div className="text-xs text-gray-500">RAG + LLM (80% Ollama free)</div>
                  </Label>
                </div>
                {getStrategyBadge('SingleModel')}
              </div>

              <div className="flex items-center justify-between space-x-2 p-3 border rounded-lg hover:bg-gray-50">
                <div className="flex items-center space-x-2">
                  <RadioGroupItem value="MultiModelConsensus" id="multi" />
                  <Label htmlFor="multi" className="cursor-pointer">
                    <div className="font-semibold">Multi-Model Consensus</div>
                    <div className="text-xs text-gray-500">GPT-4 + Claude validation</div>
                  </Label>
                </div>
                {getStrategyBadge('MultiModelConsensus')}
              </div>
            </RadioGroup>
          </div>

          {/* Submit Button */}
          <Button onClick={handleSubmit} disabled={loading} className="w-full">
            {loading ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Processing...
              </>
            ) : (
              <>
                <Zap className="mr-2 h-4 w-4" />
                Ask Agent
              </>
            )}
          </Button>

          {/* Error Display */}
          {error && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}
        </CardContent>
      </Card>

      {/* Response Display */}
      {response && (
        <Card>
          <CardHeader>
            <CardTitle>Response</CardTitle>
            <CardDescription>{response.strategyDescription}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-6">
            {/* Metrics Grid */}
            <div className="grid grid-cols-3 gap-4 p-4 bg-gray-50 rounded-lg">
              <div className="text-center">
                <div className="flex items-center justify-center text-gray-600 mb-1">
                  <Zap className="h-4 w-4 mr-1" />
                  <span className="text-sm">Tokens</span>
                </div>
                <p className="text-2xl font-bold">{response.tokenUsage.totalTokens}</p>
                <p className="text-xs text-gray-500">
                  {response.tokenUsage.promptTokens} prompt + {response.tokenUsage.completionTokens} completion
                </p>
              </div>

              <div className="text-center">
                <div className="flex items-center justify-center text-gray-600 mb-1">
                  <DollarSign className="h-4 w-4 mr-1" />
                  <span className="text-sm">Cost</span>
                </div>
                <p className="text-2xl font-bold">${response.costBreakdown.totalCost.toFixed(4)}</p>
                <p className="text-xs text-gray-500">{response.costBreakdown.provider}</p>
              </div>

              <div className="text-center">
                <div className="flex items-center justify-center text-gray-600 mb-1">
                  <Clock className="h-4 w-4 mr-1" />
                  <span className="text-sm">Latency</span>
                </div>
                <p className="text-2xl font-bold">{response.latencyMs}ms</p>
                <p className="text-xs text-gray-500">{(response.latencyMs / 1000).toFixed(2)}s</p>
              </div>
            </div>

            {/* Answer (if generated) */}
            {response.answer && (
              <div className="space-y-2">
                <h3 className="font-semibold text-lg">Answer</h3>
                <div className="p-4 bg-blue-50 border border-blue-200 rounded-lg">
                  <p className="text-sm whitespace-pre-wrap">{response.answer}</p>
                </div>
              </div>
            )}

            {/* Retrieved Chunks */}
            <div className="space-y-3">
              <h3 className="font-semibold text-lg">
                Retrieved Chunks ({response.retrievedChunks.length})
              </h3>
              {response.retrievedChunks.length === 0 ? (
                <p className="text-sm text-gray-500 italic">No chunks retrieved</p>
              ) : (
                response.retrievedChunks.map((chunk, i) => (
                  <Card key={i} className="border-l-4 border-l-blue-500">
                    <CardContent className="pt-4">
                      <div className="flex justify-between items-start mb-2">
                        <div className="flex-1">
                          <p className="font-mono text-xs text-gray-600">{chunk.filePath}</p>
                          <p className="text-xs text-gray-500">
                            Page {chunk.startLine} • Chunk {chunk.chunkIndex} • {chunk.boundedContext}
                          </p>
                        </div>
                        <Badge variant="secondary">
                          Score: {chunk.relevanceScore.toFixed(3)}
                        </Badge>
                      </div>
                      <pre className="text-xs bg-gray-100 p-3 rounded overflow-x-auto whitespace-pre-wrap">
                        {chunk.codePreview}
                      </pre>
                    </CardContent>
                  </Card>
                ))
              )}
            </div>

            {/* Provider Info */}
            <div className="pt-4 border-t text-xs text-gray-600">
              <p>
                <span className="font-semibold">Provider:</span> {response.costBreakdown.provider}
                {response.costBreakdown.modelUsed && (
                  <> • <span className="font-semibold">Model:</span> {response.costBreakdown.modelUsed}</>
                )}
              </p>
              <p>
                <span className="font-semibold">Session ID:</span> {response.sessionId}
              </p>
              <p>
                <span className="font-semibold">Timestamp:</span>{' '}
                {new Date(response.timestamp).toLocaleString()}
              </p>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
