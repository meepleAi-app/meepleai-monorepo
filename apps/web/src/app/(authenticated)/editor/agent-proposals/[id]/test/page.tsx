/**
 * Test Sandbox Page - Issue #3182
 *
 * Page for testing Draft typology proposals before submission.
 * Route: /editor/agent-proposals/[id]/test
 * Features:
 * - Chat interface for testing typology
 * - Real-time responses using Draft prompt
 * - Submit for approval from sandbox
 */

'use client';

import { useState, useRef, useEffect } from 'react';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Send, ArrowLeft, Edit, CheckCircle, Loader2 } from 'lucide-react';
import { useRouter, useParams } from 'next/navigation';
import { toast } from 'sonner';

import { useAuthUser } from '@/components/auth/AuthProvider';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Separator } from '@/components/ui/navigation/separator';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import { agentTypologiesApi } from '@/lib/api/agent-typologies.api';

interface TestMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  confidenceScore?: number;
  timestamp: Date;
}

/**
 * EditorAuthGuard placeholder
 * TODO: Extract to shared component
 */
function EditorAuthGuard({ children, loading, user }: {
  children: React.ReactNode;
  loading: boolean;
  user: { role: string; id: string } | null;
}) {
  if (loading) {
    return <div className="container mx-auto p-6">Loading...</div>;
  }

  if (!user || (user.role !== 'Editor' && user.role !== 'Admin')) {
    return (
      <div className="container mx-auto p-6">
        <div className="text-center p-12">
          <h2 className="text-2xl font-bold mb-4">Access Denied</h2>
          <p className="text-muted-foreground">
            This page is only accessible to Editors and Administrators.
          </p>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}

function TestSandboxClient() {
  const router = useRouter();
  const params = useParams();
  const { user, loading: authLoading } = useAuthUser();
  const typologyId = params?.id as string;
  const queryClient = useQueryClient();

  const [messages, setMessages] = useState<TestMessage[]>([]);
  const [inputValue, setInputValue] = useState('');
  const messagesEndRef = useRef<HTMLDivElement>(null);

  // Auto-scroll to bottom on new messages
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  // Fetch typology
  const { data: typology, isLoading, error } = useQuery({
    queryKey: ['typology', typologyId],
    queryFn: () => agentTypologiesApi.getById(typologyId),
    enabled: !!typologyId,
  });

  // Test typology mutation
  const testMutation = useMutation({
    mutationFn: async (query: string) => {
      const result = await agentTypologiesApi.test(typologyId, query);
      return result;
    },
    onSuccess: (result, query) => {
      // Add user message
      const userMessage: TestMessage = {
        id: `user-${Date.now()}`,
        role: 'user',
        content: query,
        timestamp: new Date(),
      };

      // Add assistant response
      const assistantMessage: TestMessage = {
        id: `assistant-${Date.now()}`,
        role: 'assistant',
        content: result.response,
        confidenceScore: result.confidenceScore,
        timestamp: new Date(),
      };

      setMessages(prev => [...prev, userMessage, assistantMessage]);
      setInputValue('');
    },
    onError: (error) => {
      toast.error('Test failed', {
        description: error instanceof Error ? error.message : 'Unknown error',
      });
    },
  });

  // Submit for approval mutation
  const submitMutation = useMutation({
    mutationFn: async () => {
      if (!typology) throw new Error('Typology not loaded');

      // Submit for approval (Draft → PendingReview)
      // Note: Backend domain has SubmitForApproval() method
      // Using update endpoint as workaround until dedicated /submit endpoint exists
      await agentTypologiesApi.submitForApproval(typologyId, typology);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['editor-proposals'] });
      queryClient.invalidateQueries({ queryKey: ['typology', typologyId] });
      toast.success('Submitted for approval');
      router.push('/editor/agent-proposals');
    },
    onError: (error) => {
      toast.error('Submission failed', {
        description: error instanceof Error ? error.message : 'Unknown error',
      });
    },
  });

  const handleSendMessage = (e: React.FormEvent) => {
    e.preventDefault();
    if (!inputValue.trim() || testMutation.isPending) return;

    testMutation.mutate(inputValue.trim());
  };

  const handleSubmitForApproval = () => {
    if (confirm(`Submit "${typology?.name}" for admin approval?\n\nOnce submitted, you won't be able to edit or test this proposal until it's reviewed.`)) {
      submitMutation.mutate();
    }
  };

  const handleBack = () => {
    router.push('/editor/agent-proposals');
  };

  const handleEdit = () => {
    router.push(`/editor/agent-proposals/${typologyId}/edit`);
  };

  // Sample questions
  const sampleQuestions = [
    'What are the basic rules of the game?',
    'How does scoring work?',
    'What is the setup process?',
    'Can you explain the turn sequence?',
  ];

  const handleSampleQuestion = (question: string) => {
    setInputValue(question);
  };

  // Loading state
  if (authLoading || isLoading) {
    return (
      <div className="container mx-auto p-6">
        <div className="text-center p-12">Loading...</div>
      </div>
    );
  }

  // Error state
  if (error || !typology) {
    return (
      <div className="container mx-auto p-6">
        <div className="text-center p-12">
          <h2 className="text-2xl font-bold mb-4">Proposal Not Found</h2>
          <p className="text-muted-foreground">
            {error instanceof Error ? error.message : 'The proposal you are looking for does not exist.'}
          </p>
        </div>
      </div>
    );
  }

  // Authorization check: only creator can test
  if (user && typology.createdBy !== user.id) {
    return (
      <div className="container mx-auto p-6">
        <div className="text-center p-12">
          <h2 className="text-2xl font-bold mb-4">Not Authorized</h2>
          <p className="text-muted-foreground">
            You can only test your own proposals.
          </p>
        </div>
      </div>
    );
  }

  // Status check: only Draft can be tested
  if (typology.status !== 'Draft') {
    return (
      <div className="container mx-auto p-6">
        <div className="text-center p-12">
          <h2 className="text-2xl font-bold mb-4">Cannot Test</h2>
          <p className="text-muted-foreground">
            Only Draft proposals can be tested. This proposal is currently: {typology.status}
          </p>
        </div>
      </div>
    );
  }

  return (
    <EditorAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto p-6 max-w-7xl">
        {/* Header */}
        <div className="mb-6 flex items-start justify-between">
          <div>
            <div className="flex items-center gap-3 mb-2">
              <Button variant="ghost" size="sm" onClick={handleBack}>
                <ArrowLeft className="h-4 w-4 mr-1" />
                Back
              </Button>
            </div>
            <h1 className="text-3xl font-bold tracking-tight">Test Sandbox: {typology.name}</h1>
            <p className="text-muted-foreground mt-2">
              Test your typology with sample questions before submitting for approval
            </p>
          </div>
          <div className="flex gap-2">
            <Button variant="outline" onClick={handleEdit}>
              <Edit className="h-4 w-4 mr-2" />
              Edit Proposal
            </Button>
            <Button
              onClick={handleSubmitForApproval}
              disabled={submitMutation.isPending}
            >
              <CheckCircle className="h-4 w-4 mr-2" />
              Submit for Approval
            </Button>
          </div>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Left: Typology Info */}
          <div className="lg:col-span-1 space-y-4">
            <Card>
              <CardHeader>
                <CardTitle>Typology Details</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                <div>
                  <p className="text-xs font-medium text-muted-foreground mb-1">Name</p>
                  <p className="text-sm">{typology.name}</p>
                </div>
                <div>
                  <p className="text-xs font-medium text-muted-foreground mb-1">Description</p>
                  <p className="text-sm">{typology.description}</p>
                </div>
                <div>
                  <p className="text-xs font-medium text-muted-foreground mb-1">Strategy</p>
                  <Badge variant="secondary">{typology.defaultStrategyName}</Badge>
                </div>
                <div>
                  <p className="text-xs font-medium text-muted-foreground mb-1">Status</p>
                  <Badge variant="secondary">Draft</Badge>
                </div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Sample Questions</CardTitle>
                <CardDescription>Click to test</CardDescription>
              </CardHeader>
              <CardContent className="space-y-2">
                {sampleQuestions.map((question, idx) => (
                  <Button
                    key={idx}
                    variant="outline"
                    size="sm"
                    className="w-full justify-start text-left h-auto py-2"
                    onClick={() => handleSampleQuestion(question)}
                    disabled={testMutation.isPending}
                  >
                    {question}
                  </Button>
                ))}
              </CardContent>
            </Card>
          </div>

          {/* Right: Chat Interface */}
          <div className="lg:col-span-2">
            <Card className="h-[600px] flex flex-col">
              <CardHeader>
                <CardTitle>Test Chat</CardTitle>
                <CardDescription>
                  Ask questions to see how this typology responds
                </CardDescription>
              </CardHeader>
              <Separator />

              {/* Messages Area */}
              <CardContent className="flex-1 overflow-hidden p-0">
                <ScrollArea className="h-full px-4 py-4">
                  {messages.length === 0 ? (
                    <div className="text-center py-12 text-muted-foreground">
                      <p className="mb-2">No messages yet</p>
                      <p className="text-sm">Send a test question to start</p>
                    </div>
                  ) : (
                    <div className="space-y-4">
                      {messages.map((message) => (
                        <div
                          key={message.id}
                          className={`flex ${message.role === 'user' ? 'justify-end' : 'justify-start'}`}
                        >
                          <div
                            className={`max-w-[80%] rounded-lg px-4 py-2 ${
                              message.role === 'user'
                                ? 'bg-primary text-primary-foreground'
                                : 'bg-muted'
                            }`}
                          >
                            <p className="text-sm whitespace-pre-wrap">{message.content}</p>
                            {message.confidenceScore !== undefined && (
                              <div className="mt-2 text-xs opacity-70">
                                Confidence: {(message.confidenceScore * 100).toFixed(0)}%
                              </div>
                            )}
                          </div>
                        </div>
                      ))}

                      {testMutation.isPending && (
                        <div className="flex justify-start">
                          <div className="bg-muted rounded-lg px-4 py-2">
                            <Loader2 className="h-4 w-4 animate-spin" />
                          </div>
                        </div>
                      )}

                      <div ref={messagesEndRef} />
                    </div>
                  )}
                </ScrollArea>
              </CardContent>

              <Separator />

              {/* Input Area */}
              <div className="p-4">
                <form onSubmit={handleSendMessage} className="flex gap-2">
                  <Input
                    value={inputValue}
                    onChange={(e) => setInputValue(e.target.value)}
                    placeholder="Ask a test question..."
                    disabled={testMutation.isPending}
                    className="flex-1"
                  />
                  <Button
                    type="submit"
                    disabled={!inputValue.trim() || testMutation.isPending}
                  >
                    {testMutation.isPending ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <Send className="h-4 w-4" />
                    )}
                  </Button>
                </form>
              </div>
            </Card>
          </div>
        </div>
      </div>
    </EditorAuthGuard>
  );
}

export default function TestSandboxPage() {
  return <TestSandboxClient />;
}
