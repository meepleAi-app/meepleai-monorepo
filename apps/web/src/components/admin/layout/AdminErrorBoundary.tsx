'use client';

import { Component, type ErrorInfo, type ReactNode } from 'react';

import { AlertTriangle, RefreshCw, ChevronDown, ChevronUp } from 'lucide-react';

interface Props {
  children: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
  showDetails: boolean;
}

export class AdminErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false, error: null, showDetails: false };
  }

  static getDerivedStateFromError(error: Error): Partial<State> {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error('[AdminErrorBoundary]', error, errorInfo);
  }

  handleReload = () => {
    this.setState({ hasError: false, error: null, showDetails: false });
  };

  toggleDetails = () => {
    this.setState(prev => ({ showDetails: !prev.showDetails }));
  };

  render() {
    if (this.state.hasError) {
      const { error, showDetails } = this.state;

      return (
        <div className="flex items-center justify-center min-h-[400px] p-6">
          <div className="max-w-md w-full rounded-2xl border border-red-200/60 dark:border-red-900/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-sm p-8 text-center">
            <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-xl bg-red-100/80 dark:bg-red-900/30 mb-4">
              <AlertTriangle className="h-6 w-6 text-red-600 dark:text-red-400" />
            </div>

            <h2 className="font-quicksand text-lg font-bold text-foreground">
              Something went wrong
            </h2>
            <p className="mt-1.5 text-sm text-muted-foreground">
              A component in this section crashed. You can try reloading it.
            </p>

            <div className="mt-5 flex items-center justify-center gap-3">
              <button
                onClick={this.handleReload}
                className="inline-flex items-center gap-2 rounded-lg bg-amber-100/80 dark:bg-amber-900/30 px-4 py-2 text-sm font-semibold text-amber-900 dark:text-amber-300 transition-colors hover:bg-amber-200/80 dark:hover:bg-amber-900/50"
              >
                <RefreshCw className="h-4 w-4" />
                Reload Section
              </button>
            </div>

            {error && (
              <div className="mt-4">
                <button
                  onClick={this.toggleDetails}
                  className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground transition-colors"
                >
                  {showDetails ? (
                    <ChevronUp className="h-3 w-3" />
                  ) : (
                    <ChevronDown className="h-3 w-3" />
                  )}
                  {showDetails ? 'Hide' : 'Show'} error details
                </button>

                {showDetails && (
                  <pre className="mt-2 max-h-40 overflow-auto rounded-lg bg-slate-100 dark:bg-zinc-900 p-3 text-left text-xs text-red-700 dark:text-red-400">
                    {error.message}
                    {error.stack && `\n\n${error.stack}`}
                  </pre>
                )}
              </div>
            )}
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
