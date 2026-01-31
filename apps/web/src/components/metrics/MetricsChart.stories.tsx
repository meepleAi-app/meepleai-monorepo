import React from 'react';

import { MetricsChart } from './MetricsChart';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof MetricsChart> = {
  title: 'Components/MetricsChart',
  component: MetricsChart,
  parameters: {
    layout: 'padded',
    chromatic: { disableSnapshot: false },
  },
  tags: ['autodocs'],
  argTypes: {
    type: {
      control: 'select',
      options: ['line', 'area', 'bar'],
      description: 'Chart type',
    },
    height: {
      control: { type: 'number', min: 200, max: 600, step: 50 },
      description: 'Chart height in pixels',
    },
    showGrid: {
      control: 'boolean',
      description: 'Show grid lines',
    },
    showTooltip: {
      control: 'boolean',
      description: 'Show interactive tooltip',
    },
    showLegend: {
      control: 'boolean',
      description: 'Show legend',
    },
    enableBrush: {
      control: 'boolean',
      description: 'Enable zoom/pan brush',
    },
    loading: {
      control: 'boolean',
      description: 'Loading state',
    },
  },
};

export default meta;
type Story = StoryObj<typeof MetricsChart>;

// Sample data
const sampleData = [
  { time: '00:00', requests: 45, errors: 2, latency: 120 },
  { time: '01:00', requests: 52, errors: 1, latency: 105 },
  { time: '02:00', requests: 38, errors: 3, latency: 145 },
  { time: '03:00', requests: 61, errors: 0, latency: 95 },
  { time: '04:00', requests: 73, errors: 2, latency: 110 },
  { time: '05:00', requests: 58, errors: 1, latency: 130 },
  { time: '06:00', requests: 82, errors: 4, latency: 160 },
  { time: '07:00', requests: 95, errors: 2, latency: 115 },
  { time: '08:00', requests: 110, errors: 3, latency: 125 },
  { time: '09:00', requests: 125, errors: 1, latency: 100 },
  { time: '10:00', requests: 140, errors: 5, latency: 180 },
  { time: '11:00', requests: 132, errors: 2, latency: 105 },
];

const requestsSeries = [
  { key: 'requests', name: 'Total Requests', color: '#1a73e8' },
  { key: 'errors', name: 'Errors', color: '#ea4335' },
];

const latencySeries = [{ key: 'latency', name: 'Avg Latency (ms)', color: '#f9ab00' }];

// Stories
export const LineChart: Story = {
  args: {
    type: 'line',
    data: sampleData,
    xAxisKey: 'time',
    series: requestsSeries,
    height: 300,
    showGrid: true,
    showTooltip: true,
    showLegend: true,
    enableBrush: false,
    xAxisLabel: 'Time',
    yAxisLabel: 'Count',
  },
};

export const AreaChart: Story = {
  args: {
    type: 'area',
    data: sampleData,
    xAxisKey: 'time',
    series: requestsSeries,
    height: 300,
    showGrid: true,
    showTooltip: true,
    showLegend: true,
    enableBrush: false,
    xAxisLabel: 'Time',
    yAxisLabel: 'Count',
  },
};

export const BarChart: Story = {
  args: {
    type: 'bar',
    data: sampleData,
    xAxisKey: 'time',
    series: requestsSeries,
    height: 300,
    showGrid: true,
    showTooltip: true,
    showLegend: true,
    enableBrush: false,
    xAxisLabel: 'Time',
    yAxisLabel: 'Count',
  },
};

export const WithBrush: Story = {
  args: {
    type: 'line',
    data: sampleData,
    xAxisKey: 'time',
    series: requestsSeries,
    height: 350,
    showGrid: true,
    showTooltip: true,
    showLegend: true,
    enableBrush: true,
    xAxisLabel: 'Time',
    yAxisLabel: 'Count',
  },
};

export const SingleSeries: Story = {
  args: {
    type: 'line',
    data: sampleData,
    xAxisKey: 'time',
    series: latencySeries,
    height: 300,
    showGrid: true,
    showTooltip: true,
    showLegend: true,
    enableBrush: false,
    xAxisLabel: 'Time',
    yAxisLabel: 'Latency (ms)',
  },
};

export const NoGrid: Story = {
  args: {
    type: 'line',
    data: sampleData,
    xAxisKey: 'time',
    series: requestsSeries,
    height: 300,
    showGrid: false,
    showTooltip: true,
    showLegend: true,
    enableBrush: false,
  },
};

export const NoLegend: Story = {
  args: {
    type: 'line',
    data: sampleData,
    xAxisKey: 'time',
    series: requestsSeries,
    height: 300,
    showGrid: true,
    showTooltip: true,
    showLegend: false,
    enableBrush: false,
  },
};

export const Loading: Story = {
  args: {
    type: 'line',
    data: [],
    xAxisKey: 'time',
    series: requestsSeries,
    height: 300,
    loading: true,
  },
};

export const Empty: Story = {
  args: {
    type: 'line',
    data: [],
    xAxisKey: 'time',
    series: requestsSeries,
    height: 300,
    loading: false,
    emptyMessage: 'No metrics data available',
  },
};

export const CustomColors: Story = {
  args: {
    type: 'area',
    data: sampleData,
    xAxisKey: 'time',
    series: [
      { key: 'requests', name: 'Requests', color: '#10b981', strokeWidth: 3 },
      { key: 'errors', name: 'Errors', color: '#ef4444', strokeWidth: 2 },
    ],
    height: 300,
    showGrid: true,
    showTooltip: true,
    showLegend: true,
    enableBrush: false,
  },
};

export const Tall: Story = {
  args: {
    type: 'line',
    data: sampleData,
    xAxisKey: 'time',
    series: requestsSeries,
    height: 500,
    showGrid: true,
    showTooltip: true,
    showLegend: true,
    enableBrush: false,
  },
};

export const Compact: Story = {
  args: {
    type: 'line',
    data: sampleData,
    xAxisKey: 'time',
    series: requestsSeries,
    height: 200,
    showGrid: true,
    showTooltip: true,
    showLegend: false,
    enableBrush: false,
  },
};

// Dark mode example
export const DarkMode: Story = {
  args: {
    type: 'area',
    data: sampleData,
    xAxisKey: 'time',
    series: requestsSeries,
    height: 300,
    showGrid: true,
    showTooltip: true,
    showLegend: true,
    enableBrush: false,
  },
  decorators: [
    Story => (
      <div className="dark rounded-lg bg-slate-900 p-6">
        <Story />
      </div>
    ),
  ],
};

// Multiple charts comparison
export const MultipleCharts: Story = {
  render: () => (
    <div className="space-y-6">
      <div>
        <h3 className="mb-2 text-lg font-semibold">Line Chart</h3>
        <MetricsChart
          type="line"
          data={sampleData}
          xAxisKey="time"
          series={requestsSeries}
          height={250}
        />
      </div>
      <div>
        <h3 className="mb-2 text-lg font-semibold">Area Chart</h3>
        <MetricsChart
          type="area"
          data={sampleData}
          xAxisKey="time"
          series={requestsSeries}
          height={250}
        />
      </div>
      <div>
        <h3 className="mb-2 text-lg font-semibold">Bar Chart</h3>
        <MetricsChart
          type="bar"
          data={sampleData}
          xAxisKey="time"
          series={requestsSeries}
          height={250}
        />
      </div>
    </div>
  ),
};
