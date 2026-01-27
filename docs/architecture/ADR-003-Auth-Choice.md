# ADR-003: Authentication — Azure AD B2C

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
1. Azure AD B2C (OIDC)
2. Custom auth (Identity + tokens)
3. Third-party auth provider

## Decision
Use Azure AD B2C with user flows and custom policies as needed.

## Consequences
- Pros: Secure, scalable, compliant; built-in MFA and policies
- Cons: Learning curve; tenant management overhead
- Mitigation: Use standard user flows; document roles and scopes
