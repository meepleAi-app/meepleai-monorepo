using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query for the AI metrics trend chart (#1729).
///
/// Range values:
///   "Live" → last 1 hour, 1 min buckets (polling 10s lato FE)
///   "1h"   → last 1 hour, 1 min buckets (snapshot)
///   "24h"  → last 24 hours, 15 min buckets
///   "7d"   → last 7 days, 1 hour buckets
///
/// Returns p50/p95/errorRate per bucket so the FE AiTrendChart can
/// drop the legacy "approx — p50/p95/error pending" badge.
/// </summary>
internal record GetAiMetricsTrendQuery(string Range) : IQuery<AiMetricsTrendResult>;
