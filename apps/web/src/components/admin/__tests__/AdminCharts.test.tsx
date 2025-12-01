import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import {
  EndpointDistributionChart,
  LatencyDistributionChart,
  RequestsTimeSeriesChart,
  FeedbackChart,
} from '../AdminCharts';

describe('AdminCharts', () => {
  describe('EndpointDistributionChart', () => {
    it('should render chart with endpoint data', () => {
      const endpointCounts = {
        '/api/v1/games': 100,
        '/api/v1/chat': 75,
        '/api/v1/upload': 50,
      };

      const { container } = render(<EndpointDistributionChart endpointCounts={endpointCounts} />);

      expect(container.querySelector('.recharts-wrapper')).toBeInTheDocument();
    });

    it('should handle empty endpoint data', () => {
      const { container } = render(<EndpointDistributionChart endpointCounts={{}} />);

      expect(container.querySelector('.recharts-wrapper')).toBeInTheDocument();
    });
  });

  describe('LatencyDistributionChart', () => {
    it('should render chart with latency data', () => {
      const requests = [
        { latencyMs: 100, endpoint: '/api/v1/games', createdAt: '2025-12-01T10:00:00Z' },
        { latencyMs: 250, endpoint: '/api/v1/chat', createdAt: '2025-12-01T10:01:00Z' },
      ];

      const { container } = render(<LatencyDistributionChart requests={requests} />);

      expect(container.querySelector('.recharts-wrapper')).toBeInTheDocument();
    });

    it('should handle empty request data', () => {
      const { container } = render(<LatencyDistributionChart requests={[]} />);

      expect(container.querySelector('.recharts-wrapper')).toBeInTheDocument();
    });
  });

  describe('RequestsTimeSeriesChart', () => {
    it('should render chart with time series data', () => {
      const requests = [
        { createdAt: '2025-12-01T10:00:00Z', status: '200' },
        { createdAt: '2025-12-01T10:01:00Z', status: '200' },
        { createdAt: '2025-12-01T10:02:00Z', status: '500' },
      ];

      const { container } = render(<RequestsTimeSeriesChart requests={requests} />);

      expect(container.querySelector('.recharts-wrapper')).toBeInTheDocument();
    });

    it('should handle empty request data', () => {
      const { container } = render(<RequestsTimeSeriesChart requests={[]} />);

      expect(container.querySelector('.recharts-wrapper')).toBeInTheDocument();
    });
  });

  describe('FeedbackChart', () => {
    it('should render chart with feedback data', () => {
      const feedbackCounts = {
        positive: 150,
        negative: 25,
        neutral: 50,
      };

      const { container } = render(<FeedbackChart feedbackCounts={feedbackCounts} />);

      expect(container.querySelector('.recharts-wrapper')).toBeInTheDocument();
    });

    it('should handle empty feedback data', () => {
      const { container } = render(<FeedbackChart feedbackCounts={{}} />);

      expect(container.querySelector('.recharts-wrapper')).toBeInTheDocument();
    });
  });
});
