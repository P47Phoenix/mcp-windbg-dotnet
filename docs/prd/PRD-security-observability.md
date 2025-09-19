# PRD: Security, Policy & Observability Layer

## Metadata
| Field | Value |
|-------|-------|
| PRD ID | PRD-security-observability |
| Version | v0.1 (draft) |
| Owner | TBD |
| Stakeholders | Eng, Sec |
| Created | 2025-09-19 |
| Last Updated | 2025-09-19 |
| Status | Draft |
| Related ADRs | ADR-0001 |

## 1. Problem Statement
As command execution expands, need safety constraints, audit trails, and rich telemetry to prevent misuse and accelerate debugging of the debugger service itself.

## 2. Goals
- Command policy (allowlist + optional regex denylist)
- Per-session + global metrics (counts, latency, timeouts, failures)
- Structured logging with correlation IDs
- Tool: `session_info`, `list_sessions`, `stats_snapshot`
- Redaction rules for sensitive paths / PII-like strings
- Configurable rate limiting (commands / minute per session)

### Non-Goals
- Full authN/Z system
- SIEM integration (placeholder hooks only)

## 3. Users & Personas
| Persona | Need | Success |
|---------|------|--------|
| Security Reviewer | Validate allowed surface | Policy file diff small |
| Operator | Diagnose latency spike | Stats snapshot shows culprit |

## 4. Use Cases
| ID | Story | Priority | Acceptance |
|----|-------|----------|-----------|
| UC-1 | Block disallowed command | Must | Error references policy name |
| UC-2 | View session stats | Must | Latency & command count shown |
| UC-3 | Get global metrics | Must | Aggregated counters exported |
| UC-4 | Rate limit abuse | Should | Excess returns 429-like error |
| UC-5 | Redact sensitive path | Should | Output replaced with `<REDACTED>` |

## 5. Solution Overview
Introduce middleware pipeline wrapping command execution: PolicyCheck -> RateLimiter -> Executor -> Redactor -> MetricsRecorder -> Logger. Metrics exposed internally (tool) and optionally Prometheus endpoint (future). Policy defined in JSON configuration reloaded on SIGHUP / file change.

## 6. Functional Requirements
| FR ID | Description | Acceptance |
|-------|-------------|-----------|
| FR-1 | Policy enforcement | Denied commands never reach executor |
| FR-2 | Rate limit | Config threshold enforced per session |
| FR-3 | Redaction | Pattern list applied post-exec |
| FR-4 | Metrics capture | Command latency histogram recorded |
| FR-5 | Session info tool | Returns last N commands summary |
| FR-6 | Stats snapshot tool | Returns global counters snapshot |

## 7. Non-Functional Requirements
| NFR ID | Category | Requirement | Measure |
|--------|----------|------------|---------|
| NFR-1 | Security | Policy evaluation overhead | <5% latency penalty |
| NFR-2 | Performance | Rate limiter scalability | 100 sessions OK |
| NFR-3 | Observability | Correlation ID presence | 100% requests |
| NFR-4 | Reliability | Metrics write errors | Non-fatal, logged |

## 8. Dependencies
| Type | Name | Purpose | Risk | Mitigation |
|------|------|---------|------|------------|
| External | (Optional) Prometheus-net | Metrics export | Added complexity | Feature flag |

## 9. Risks & Mitigations
| Risk | Impact | Likelihood | Mitigation |
|------|--------|-----------|-----------|
| Overly broad deny regex | Blocks valid commands | Medium | Test suite coverage |
| Rate limiter starvation | Reduced throughput | Low | Token bucket tuning |
| Redaction false positives | Confusing output | Medium | Scoped patterns & tests |

## 10. Metrics
| Metric | Target |
|--------|--------|
| Policy violation attempts logged | 100% |
| Average added latency (policy+metrics) | <20ms |
| Rate-limited requests ratio | <5% normal load |

## 11. Rollout Strategy
Feature flags: `PolicyEnforcementEnabled`, `RateLimitingEnabled`. Gradual enable in staging first.

## 12. Open Questions
| ID | Question | Owner | Due |
|----|----------|-------|-----|
| Q1 | Provide external metrics endpoint now or later? | TBD | 2025-10-10 |

## 13. Changelog
| Version | Date | Author | Summary |
|---------|------|--------|---------|
| v0.1 | 2025-09-19 | TBD | Initial draft |
