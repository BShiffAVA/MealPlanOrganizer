# ADR-003: Authentication — Microsoft Entra External ID

## Status
Accepted

## Context
Mobile clients need secure, standards-based authentication without building custom auth. Teens are users; parental oversight may be needed.

## Decision Drivers
- OIDC/JWT compatibility with MAUI
- Self-service password reset and policies
- Household isolation via app authorization layer
- Low maintenance and secure by default

## Options Considered
1. Microsoft Entra External ID (OIDC)
2. Custom auth (Identity + tokens)
3. Third-party auth provider

## Decision
Use Microsoft Entra External ID (in external tenant) with user flows as needed.

## Consequences
- Pros: Next-generation CIAM solution; secure, scalable, compliant; built-in MFA, Conditional Access, and MSAL support; better mobile integration; all Microsoft innovation on this platform
- Cons: Requires external tenant configuration separate from workforce
- Mitigation: Use standard user flows; document roles and scopes; leverage MSAL SDK for .NET MAUI
