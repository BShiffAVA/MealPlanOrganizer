# ADR-002: Database Choice — Azure SQL Basic Tier

## Status
Accepted

## Context
Data is relational (households, users, recipes, ingredients, plans, ratings) with transactional integrity needs and flexible queries.

## Decision Drivers
- Strong consistency and relationships
- Simple reporting and search
- Familiar SQL development
- Cost-effective at small scale

## Options Considered
1. Azure SQL Database (Basic)
2. Azure Cosmos DB (Core/SQL)
3. PostgreSQL (Flexible Server)

## Decision
Choose Azure SQL Database (Basic tier) with normalized schema and indexes.

## Consequences
- Pros: ACID transactions, mature tooling, straightforward migrations
- Cons: Scaling writes may require tuning; Basic tier performance limits
- Mitigation: Indexing, connection pooling, query optimization; upgrade tier if needed
