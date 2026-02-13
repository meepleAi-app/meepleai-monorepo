/**
 * ChatErrorBoundary - Error boundary for chat components
 * Catches rendering errors from malformed SSE data or markdown
 */

'use client';

import { Component, ReactNode } from 'react';

import { AlertCircle } from 'lucide-react';

import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { logger } from '@/lib/logger';

interface Props {
  children: ReactNode;
  onReset?: () => void;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

export class ChatErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, _errorInfo: React.ErrorInfo) {
    logger.error('Chat rendering error caught by ErrorBoundary', error);
  }

  handleReset = () => {
    this.setState({ hasError: false, error: null });
    this.props.onReset?.();
  };

  render() {
    if (this.state.hasError) {
      return (
        <div className="flex items-center justify-center min-h-[400px] p-6">
          <Alert variant="destructive" className="max-w-md">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>
              <div className="space-y-3">
                <p className="font-semibold">Chat encountered an error</p>
                <p className="text-sm">
                  {this.state.error?.message || 'Something went wrong rendering the chat'}
                </p>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={this.handleReset}
                  className="mt-2"
                >
                  Try Again
                </Button>
              </div>
            </AlertDescription>
          </Alert>
        </div>
      );
    }

    return this.props.children;
  }
}
