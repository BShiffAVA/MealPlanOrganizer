# ADR-004: Real-time Updates — Require SignalR for Ratings and Meal Plans

## Status
Accepted

## Context
Real-time updates are required for ratings and meal plan changes to ensure household members see updates immediately.

## Decision Drivers
- Real-time visibility for ratings and meal plan changes
- Cost minimization (target <$50/month)
- Offline-first sync model remains for resilience

## Options Considered
1. Azure SignalR Service (messages to clients)
2. Polling/background sync in clients
3. Azure Web PubSub

## Decision
Enable Azure SignalR Service in production to deliver real-time updates for ratings and meal plan changes. Maintain background sync as a fallback to handle intermittent connectivity and ensure eventual consistency.

## Consequences
- Pros: Instant updates for critical features; improved UX and collaboration
- Cons: Additional monthly cost for SignalR; operational considerations
- Mitigation: Scope events to ratings and meal plans; use serverless scale units; monitor usage and adjust message rates; keep background sync active
