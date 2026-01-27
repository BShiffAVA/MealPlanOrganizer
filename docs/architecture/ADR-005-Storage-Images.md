# ADR-005: Image Storage — Azure Blob with Client-side Compression

## Status
Accepted

## Context
Recipe photos require durable, cheap storage and efficient mobile handling.

## Decision Drivers
- Low cost per GB
- Simple upload via SAS
- Bandwidth optimization for mobile

## Options Considered
1. Azure Blob Storage (Hot, GPv2)
2. Store images in DB (base64)
3. Third-party image CDN

## Decision
Use Azure Blob Storage with SAS for uploads; compress images client-side and optionally resize server-side.

## Consequences
- Pros: Cheap, robust, scalable; easy integration
- Cons: Requires generating SAS tokens and lifecycle policies
- Mitigation: Short-lived SAS, container-level policies, lifecycle management (delete stale)
