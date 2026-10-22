---
paths:
  - "scripts/Net/**"
  - "src/networking/**"
  - "Assets/**/{Networking,networking,Network,network}/**/*.cs"
  - "Source/**/{Networking,networking,Network,network}/**/*.{h,cpp}"
---

> **dogeballVR:** the current model is host authority over ENet, no client prediction, no rollback, no host migration (see README). Do not add or "fix" these without an ADR; report gaps in the PR instead.

# Network Code Rules

- Server is AUTHORITATIVE for all gameplay-critical state — never trust the client
- All network messages must be versioned for forward/backward compatibility
- Client predicts locally, reconciles with server — implement rollback for mispredictions
- Handle disconnection, reconnection, and host migration gracefully
- Rate-limit all network logging to prevent log flooding
- All networked values must specify replication strategy: reliable/unreliable, frequency, interpolation
- Bandwidth budget: define and track per-message-type bandwidth usage
- Security: validate all incoming packet sizes and field ranges
