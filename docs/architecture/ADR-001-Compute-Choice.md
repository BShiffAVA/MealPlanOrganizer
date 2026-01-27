# ADR-001: Compute Choice — Azure Functions (Consumption)

## Status
Accepted

## Context
The backend must be low-cost, scalable, and simple to operate for a small household app. Workload is bursty and latency-sensitive for mobile clients, with predominantly CRUD operations and lightweight recommendation queries.

## Decision Drivers
- Budget under $50/month
- Stateless REST endpoints
- Minimal ops overhead
- Native integration with Azure services

## Options Considered
1. Azure Functions (consumption, .NET)
2. Azure App Service (Basic)
3. Azure Container Apps (serverless containers)

## Decision
Choose Azure Functions (consumption) for HTTP-triggered APIs.

## Consequences
- Pros: Lowest cost at low traffic, auto-scale, simple deployments
- Cons: Cold starts possible; long-running operations need queue/Durable Functions
- Mitigation: Pre-warm critical endpoints; design for idempotency and retries
