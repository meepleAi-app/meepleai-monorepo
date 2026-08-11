'use client';

import { Suspense } from 'react';
import type { JSX } from 'react';

import dynamic from 'next/dynamic';

import type { MechanicCostByDay } from '@/lib/api/schemas/admin-mechanic-metrics.schemas';

// #532: daily-cost bar chart. Mirrors the AdminCharts dynamic-import pattern so recharts stays out of
// the SSR bundle but renders synchronously under NODE_ENV=test.
const isTest = process.env.NODE_ENV === 'test';
const recharts = isTest ? require('recharts') : null;

const ResponsiveContainer = isTest
  ? recharts.ResponsiveContainer
  : dynamic(() => import('recharts').then(m => m.ResponsiveContainer), { ssr: false });
const BarChart = isTest
  ? recharts.BarChart
  : dynamic(() => import('recharts').then(m => m.BarChart), { ssr: false });
const Bar = isTest
  ? recharts.Bar
  : dynamic(() => import('recharts').then(m => m.Bar), { ssr: false });
const XAxis = isTest
  ? recharts.XAxis
  : dynamic(() => import('recharts').then(m => m.XAxis), { ssr: false });
const YAxis = isTest
  ? recharts.YAxis
  : dynamic(() => import('recharts').then(m => m.YAxis), { ssr: false });
const CartesianGrid = isTest
  ? recharts.CartesianGrid
  : dynamic(() => import('recharts').then(m => m.CartesianGrid), { ssr: false });
const Tooltip = isTest
  ? recharts.Tooltip
  : dynamic(() => import('recharts').then(m => m.Tooltip), { ssr: false });

export interface MechanicCostChartProps {
  data: MechanicCostByDay[];
}

export function MechanicCostChart({ data }: MechanicCostChartProps): JSX.Element {
  return (
    <div data-testid="mechanic-cost-chart" className="h-72 w-full">
      <Suspense
        fallback={<div className="text-sm text-muted-foreground">Caricamento grafico…</div>}
      >
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={data}>
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis dataKey="date" tick={{ fontSize: 11 }} />
            <YAxis tick={{ fontSize: 11 }} />
            <Tooltip />
            <Bar dataKey="costUsd" fill="#1a73e8" name="Costo USD" />
          </BarChart>
        </ResponsiveContainer>
      </Suspense>
    </div>
  );
}
