import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { CacheHitGauge } from '../CacheHitGauge';
import type { CacheMetrics } from '../types';

const mockCacheData: CacheMetrics = {
  hitRate: 85,
  missRate: 15,
  totalHits: 12500,
  totalMisses: 2200,
  cacheSize: 256,
  ttlSeconds: 3600,
};

describe('CacheHitGauge', () => {
  it('renders the component title', () => {
    render(<CacheHitGauge data={mockCacheData} />);
    expect(screen.getByText('Cache Performance')).toBeInTheDocument();
  });

  it('displays hit rate percentage', () => {
    render(<CacheHitGauge data={mockCacheData} />);
    expect(screen.getByText('85%')).toBeInTheDocument();
    expect(screen.getByText('Hit Rate')).toBeInTheDocument();
  });

  it('shows total hits count', () => {
    render(<CacheHitGauge data={mockCacheData} />);
    expect(screen.getByText('Hits')).toBeInTheDocument();
    // toLocaleString() may or may not add commas depending on locale
    expect(screen.getByText(/12[,.]?500/)).toBeInTheDocument();
  });

  it('shows total misses count', () => {
    render(<CacheHitGauge data={mockCacheData} />);
    expect(screen.getByText('Misses')).toBeInTheDocument();
    // toLocaleString() may or may not add commas depending on locale
    expect(screen.getByText(/2[,.]?200/)).toBeInTheDocument();
  });

  it('displays cache size and TTL', () => {
    render(<CacheHitGauge data={mockCacheData} />);
    // TTL: 3600 seconds = 60 minutes
    expect(screen.getByText('Cache: 256MB • TTL: 60min')).toBeInTheDocument();
  });

  it('applies custom className', () => {
    const { container } = render(
      <CacheHitGauge data={mockCacheData} className="custom-class" />
    );
    expect(container.firstChild).toHaveClass('custom-class');
  });

  it('renders the circular gauge SVG', () => {
    const { container } = render(<CacheHitGauge data={mockCacheData} />);
    // The second SVG is the circular gauge (first one is the Database icon)
    const svgs = container.querySelectorAll('svg');
    expect(svgs.length).toBeGreaterThan(1);
    const gaugeSvg = svgs[1];
    expect(gaugeSvg).toBeInTheDocument();
    expect(gaugeSvg).toHaveAttribute('viewBox', '0 0 100 100');
  });

  it('handles low hit rate', () => {
    const lowHitData: CacheMetrics = {
      ...mockCacheData,
      hitRate: 35,
    };
    render(<CacheHitGauge data={lowHitData} />);
    expect(screen.getByText('35%')).toBeInTheDocument();
  });

  it('handles high hit rate', () => {
    const highHitData: CacheMetrics = {
      ...mockCacheData,
      hitRate: 98,
    };
    render(<CacheHitGauge data={highHitData} />);
    expect(screen.getByText('98%')).toBeInTheDocument();
  });

  it('formats TTL correctly for different durations', () => {
    const shortTTLData: CacheMetrics = {
      ...mockCacheData,
      ttlSeconds: 300, // 5 minutes
    };
    render(<CacheHitGauge data={shortTTLData} />);
    expect(screen.getByText('Cache: 256MB • TTL: 5min')).toBeInTheDocument();
  });
});
