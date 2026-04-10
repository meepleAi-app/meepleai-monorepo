'use client';

import React, { Component, type ErrorInfo, type ReactNode } from 'react';

export interface SectionErrorBoundaryProps {
  sectionName: string;
  children: ReactNode;
}

interface State {
  hasError: boolean;
  errorMessage: string;
}

export class SectionErrorBoundary extends Component<SectionErrorBoundaryProps, State> {
  constructor(props: SectionErrorBoundaryProps) {
    super(props);
    this.state = { hasError: false, errorMessage: '' };
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, errorMessage: error.message };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo): void {
    if (typeof console !== 'undefined') {
      console.warn(`[MeepleDev] Section "${this.props.sectionName}" crashed:`, error, errorInfo);
    }
  }

  render(): ReactNode {
    if (this.state.hasError) {
      return (
        <div
          role="alert"
          style={{
            padding: 16,
            background: '#7f1d1d',
            color: '#fecaca',
            borderRadius: 6,
            fontSize: 11,
          }}
        >
          <strong>{this.props.sectionName} section crashed</strong>
          <div style={{ marginTop: 6, fontFamily: 'monospace' }}>{this.state.errorMessage}</div>
          <button
            type="button"
            onClick={() => this.setState({ hasError: false, errorMessage: '' })}
            style={{
              marginTop: 8,
              padding: '4px 8px',
              background: '#f9fafb',
              color: '#7f1d1d',
              border: 'none',
              borderRadius: 4,
              cursor: 'pointer',
              fontSize: 10,
            }}
          >
            Retry
          </button>
        </div>
      );
    }
    return this.props.children;
  }
}
