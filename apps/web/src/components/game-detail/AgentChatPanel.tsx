/**
 * Agent Chat Panel Component (Issue #3156)
 *
 * AI agent chat interface with:
 * - Real-time message streaming (SSE)
 * - Agent mode selection (Tutor, Q&A, Strategia, etc.)
 * - PDF selector (multi-PDF support)
 * - Inline PDF reference cards
 * - Typing indicator
 * - Message history with auto-scroll
 */

'use client';

import { useState, useRef, useEffect, useCallback } from 'react';

import { Send, Paperclip, Mic, Bot, User, FileText } from 'lucide-react';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Textarea } from '@/components/ui/primitives/textarea';
import { logger } from '@/lib/logger';
import { cn } from '@/lib/utils';

import { MeeplePdfReferenceCard, type PdfReference } from './MeeplePdfReferenceCard';
import { TypingIndicator } from './TypingIndicator';

export interface ChatMessage {
  id: string;
  role: 'user' | 'agent';
  content: string;
  timestamp: Date;
  pdfReferences?: PdfReference[];
  agentMode?: string;
  modelUsed?: string;
}

export interface AgentMode {
  id: string;
  name: string;
  description: string;
  model: string;
}

export interface GamePdf {
  id: string;
  name: string;
  pageCount: number;
}

export interface AgentChatPanelProps {
  /** Game ID for chat context */
  gameId: string;
  /** Game title for display */
  gameTitle: string;
  /** Available agent modes */
  agentModes: AgentMode[];
  /** Available PDFs for this game */
  availablePdfs: GamePdf[];
  /** Initial selected agent mode */
  initialAgentMode?: string;
  /** Initial selected PDF IDs */
  initialPdfIds?: string[];
  /** Callback when PDF reference is clicked */
  onPdfReferenceClick: (pageNumber: number, pdfId: string) => void;
  /** Additional CSS classes */
  className?: string;
}

export function AgentChatPanel({
  gameId,
  gameTitle,
  agentModes,
  availablePdfs,
  initialAgentMode,
  initialPdfIds = [],
  onPdfReferenceClick,
  className = '',
}: AgentChatPanelProps) {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [inputValue, setInputValue] = useState('');
  const [isTyping, setIsTyping] = useState(false);
  const [selectedAgentMode, setSelectedAgentMode] = useState<string>(
    initialAgentMode || agentModes[0]?.id || ''
  );
  const [selectedPdfIds, setSelectedPdfIds] = useState<string[]>(initialPdfIds);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const [_error, _setError] = useState<string | null>(null);

  // Auto-scroll to bottom on new messages
  const scrollToBottom = useCallback(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, []);

  useEffect(() => {
    scrollToBottom();
  }, [messages, scrollToBottom]);

  // Send message to agent
  const sendMessage = useCallback(async () => {
    if (!inputValue.trim() || isTyping) return;

    const userMessage: ChatMessage = {
      id: `user-${Date.now()}`,
      role: 'user',
      content: inputValue.trim(),
      timestamp: new Date(),
    };

    setMessages(prev => [...prev, userMessage]);
    setInputValue('');
    setIsTyping(true);

    try {
      // FUTURE: Implement full SSE streaming (Issue #3152)
      // For now, use simple fetch for Q&A endpoint
      const response = await fetch(
        `${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080'}/api/v1/agents/qa/stream`,
        {
          method: 'POST',
          credentials: 'include',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            gameId,
            query: inputValue.trim(),
            documentIds: selectedPdfIds,
          }),
        }
      );

      if (!response.ok) {
        throw new Error('Failed to get agent response');
      }

      // FUTURE: Implement proper SSE streaming for progressive token delivery
      const data = await response.json();

      const agentMessage: ChatMessage = {
        id: `agent-${Date.now()}`,
        role: 'agent',
        content: data.answer || 'Nessuna risposta disponibile',
        timestamp: new Date(),
        agentMode: selectedAgentMode,
        pdfReferences: data.citations?.map(
          (c: {
            documentId?: string;
            documentTitle?: string;
            pageNumber?: number;
            excerpt?: string;
            score?: number;
          }) => ({
            pdfId: c.documentId || selectedPdfIds[0] || 'pdf-1',
            pdfName: c.documentTitle || 'Regolamento',
            pageNumber: c.pageNumber || 1,
            excerpt: c.excerpt || '',
            confidence: c.score || 0.8,
          })
        ),
      };

      setMessages(prev => [...prev, agentMessage]);
      setIsTyping(false);
    } catch (error) {
      logger.error('Error calling agent:', error);
      const errorMessage: ChatMessage = {
        id: `error-${Date.now()}`,
        role: 'agent',
        content: "Errore nella comunicazione con l'agente. Riprova.",
        timestamp: new Date(),
      };
      setMessages(prev => [...prev, errorMessage]);
      setIsTyping(false);
    }
  }, [inputValue, isTyping, selectedAgentMode, selectedPdfIds, gameId]);

  // Handle Enter key (send) and Shift+Enter (newline)
  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      sendMessage();
    }
  };

  // Auto-resize textarea
  const handleInputChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    setInputValue(e.target.value);
    if (textareaRef.current) {
      textareaRef.current.style.height = 'auto';
      textareaRef.current.style.height = `${textareaRef.current.scrollHeight}px`;
    }
  };

  // Welcome message
  useEffect(() => {
    const selectedMode = agentModes.find(m => m.id === selectedAgentMode);
    const welcomeMessage: ChatMessage = {
      id: 'welcome',
      role: 'agent',
      content: `Ciao! Sono il tuo agente ${selectedMode?.name || 'AI'} per **${gameTitle}**. Ho accesso al regolamento completo e posso aiutarti a comprendere le regole, chiarire dubbi e suggerirti strategie. Come posso aiutarti oggi? 🎲`,
      timestamp: new Date(),
      agentMode: selectedAgentMode,
      modelUsed: selectedMode?.model,
    };
    setMessages([welcomeMessage]);
  }, [gameTitle, selectedAgentMode, agentModes]);

  const selectedMode = agentModes.find(m => m.id === selectedAgentMode);

  return (
    <div className={cn('flex flex-col h-full bg-white', className)}>
      {/* Agent & PDF Selectors Header */}
      <div className="border-b border-gray-200 p-4 bg-gray-50/50">
        <div className="flex items-center gap-3 flex-wrap">
          {/* Agent Mode Selector */}
          <div className="flex items-center gap-2 px-3 py-2 rounded-lg bg-green-50 border border-green-200">
            <Bot className="w-4 h-4 text-green-700" />
            <Select value={selectedAgentMode} onValueChange={setSelectedAgentMode}>
              <SelectTrigger className="h-auto border-none bg-transparent text-sm font-medium text-green-700 focus:ring-0 w-auto min-w-[150px]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {agentModes.map(mode => (
                  <SelectItem key={mode.id} value={mode.id}>
                    <div className="flex flex-col items-start">
                      <span className="font-medium">{mode.name}</span>
                      <span className="text-xs text-muted-foreground">{mode.model}</span>
                    </div>
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* PDF Selector */}
          <div className="flex items-center gap-2 px-3 py-2 rounded-lg bg-blue-50 border border-blue-200">
            <FileText className="w-4 h-4 text-blue-700" />
            <Select
              value={selectedPdfIds[0] || ''}
              onValueChange={value => setSelectedPdfIds([value])}
            >
              <SelectTrigger className="h-auto border-none bg-transparent text-sm font-medium text-blue-700 focus:ring-0 w-auto min-w-[150px]">
                <SelectValue placeholder="Seleziona PDF" />
              </SelectTrigger>
              <SelectContent>
                {availablePdfs.map(pdf => (
                  <SelectItem key={pdf.id} value={pdf.id}>
                    <div className="flex flex-col items-start">
                      <span className="font-medium">{pdf.name}</span>
                      <span className="text-xs text-muted-foreground">{pdf.pageCount} pagine</span>
                    </div>
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>
      </div>

      {/* Chat Messages Area */}
      <div className="flex-1 overflow-y-auto chat-scroll p-6 space-y-4">
        {messages.map(message => (
          <div
            key={message.id}
            className={cn(
              'flex items-start gap-3',
              message.role === 'user' ? 'justify-end' : 'justify-start'
            )}
          >
            {/* Agent Avatar */}
            {message.role === 'agent' && (
              <div className="flex-shrink-0 w-8 h-8 rounded-full bg-gradient-to-br from-blue-500 to-purple-600 flex items-center justify-center">
                <Bot className="w-4 h-4 text-white" />
              </div>
            )}

            {/* Message Content */}
            <div
              className={cn('flex-1 max-w-[80%]', message.role === 'user' && 'flex justify-end')}
            >
              {/* Agent Name & Model */}
              {message.role === 'agent' && (
                <div className="flex items-center gap-2 mb-1">
                  <span className="font-semibold text-sm">{selectedMode?.name || 'Agente AI'}</span>
                  {message.modelUsed && (
                    <span className="text-xs text-muted-foreground">{message.modelUsed}</span>
                  )}
                </div>
              )}

              {/* Message Bubble */}
              <div
                className={cn(
                  'rounded-2xl px-4 py-3',
                  message.role === 'agent'
                    ? 'bg-gray-100 rounded-tl-sm'
                    : 'bg-blue-600 text-white rounded-tr-sm'
                )}
              >
                <p className="text-sm whitespace-pre-wrap">{message.content}</p>

                {/* PDF References */}
                {message.pdfReferences?.map((ref, idx) => (
                  <MeeplePdfReferenceCard
                    key={`${ref.pdfId}-${ref.pageNumber}-${idx}`}
                    reference={ref}
                    onJumpToPage={onPdfReferenceClick}
                  />
                ))}
              </div>

              {/* Timestamp */}
              <p className="text-xs text-muted-foreground mt-1 px-1">
                {message.timestamp.toLocaleTimeString('it-IT', {
                  hour: '2-digit',
                  minute: '2-digit',
                })}
              </p>
            </div>

            {/* User Avatar */}
            {message.role === 'user' && (
              <div className="flex-shrink-0 w-8 h-8 rounded-full bg-gradient-to-br from-amber-400 to-orange-500 flex items-center justify-center">
                <User className="w-4 h-4 text-white" />
              </div>
            )}
          </div>
        ))}

        {/* Typing Indicator */}
        {isTyping && (
          <div className="flex items-start gap-3">
            <div className="flex-shrink-0 w-8 h-8 rounded-full bg-gradient-to-br from-blue-500 to-purple-600 flex items-center justify-center">
              <Bot className="w-4 h-4 text-white" />
            </div>
            <div className="bg-gray-100 rounded-2xl rounded-tl-sm">
              <TypingIndicator />
            </div>
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      {/* Input Area */}
      <div className="border-t border-gray-200 p-4 bg-white">
        <div className="flex items-end gap-3">
          <div className="flex-1">
            <Textarea
              ref={textareaRef}
              value={inputValue}
              onChange={handleInputChange}
              onKeyDown={handleKeyDown}
              placeholder="Fai una domanda sul gioco..."
              disabled={isTyping}
              className="min-h-[60px] max-h-[200px] resize-none"
              rows={2}
            />
            <div className="flex items-center gap-3 mt-2 px-2">
              <Button
                variant="ghost"
                size="sm"
                className="h-auto p-0 text-xs text-muted-foreground hover:text-blue-600"
                disabled={isTyping}
              >
                <Paperclip className="w-3 h-3 mr-1" />
                Allega
              </Button>
              <span className="text-xs text-muted-foreground">•</span>
              <Button
                variant="ghost"
                size="sm"
                className="h-auto p-0 text-xs text-muted-foreground hover:text-blue-600"
                disabled={isTyping}
              >
                <Mic className="w-3 h-3 mr-1" />
                Voce
              </Button>
            </div>
          </div>
          <Button
            onClick={sendMessage}
            disabled={!inputValue.trim() || isTyping}
            className="px-6 py-3 shadow-md"
          >
            <Send className="w-4 h-4 mr-2" />
            Invia
          </Button>
        </div>
      </div>
    </div>
  );
}
