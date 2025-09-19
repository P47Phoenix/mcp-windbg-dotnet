# PRD: Session Lifecycle & CDB Process Management

## Metadata
| Field | Value |
|-------|-------|
| PRD ID | PRD-session-lifecycle |
| Version | v0.1 (draft) |
| Owner | TBD |
| Stakeholders | Eng |
| Created | 2025-09-19 |
| Last Updated | 2025-09-19 |
| Status | Draft |
| Related ADRs | ADR-0001 |

## 1. Problem Statement
Need robust management for individual CDB sessions tied to crash dumps or remote connections with isolation, timeouts, cleanup, and reuse.

## 2. Goals
- Create `CdbSession` wrapper in .NET (marker-based completion)
- Implement thread-safe `SessionRepository`
- Support dump (`-z`) and remote (`-remote`) modes
- Enforce mutual exclusivity & validation
- Implement idle timeout eviction & explicit close

### Non-Goals
- Advanced analysis commands
- Managed runtime enrichment

## 3. Users & Personas
| Persona | Need | Success Signal |
|---------|------|----------------|
| AI Tool | Run multiple analyses sequentially | Sessions reliably reuse or close |
| Developer | Avoid orphaned cdb.exe | No zombie processes after tests |

## 4. Use Cases
| ID | Story | Priority | Acceptance |
|----|-------|----------|------------|
| UC-1 | Open dump session | Must | Returns session id |
| UC-2 | Open remote session | Must | Returns session id |
| UC-3 | Run command in existing session | Must | Uses existing process |
| UC-4 | Close session | Must | Process terminated |
| UC-5 | Idle session auto-evicted | Should | Evicted after N mins idle |

## 5. Solution Overview
Spawn `cdb.exe` processes, monitor stdout line-by-line, use injected `.echo <GUID>` marker for completion, store sessions dictionary keyed by canonical ID.

## 6. Functional Requirements
| FR ID | Description | Acceptance |
|-------|-------------|------------|
| FR-1 | Session create (dump) | Returns active session object |
| FR-2 | Session create (remote) | Valid connection string format |
| FR-3 | Command send w/ marker | Output aggregated before marker |
| FR-4 | Timeout handling | Command error after T seconds |
| FR-5 | Session close | Process exit code captured |
| FR-6 | Idle eviction | Evict after config IdleMinutes |

## 7. Non-Functional Requirements
| NFR ID | Category | Requirement | Measure |
|--------|----------|------------|---------|
| NFR-1 | Resource | Max concurrent sessions configurable | Default 5 |
| NFR-2 | Reliability | No handle leaks (verified in test) | Pass leak test |
| NFR-3 | Performance | Command overhead added by wrapper | <50ms per command |
| NFR-4 | Observability | Structured session state snapshot | Provided via tool |

## 8. Dependencies
| Type | Name | Purpose | Risk | Mitigation |
|------|------|---------|------|------------|
| External | cdb.exe | Debug engine host | Not installed | Detect + error guidance |

## 9. Risks & Mitigations
| Risk | Impact | Likelihood | Mitigation |
|------|--------|-----------|-----------|
| Deadlock on stdout read | High | Medium | Dedicated async reader + buffer cap |
| Marker collision | Medium | Low | Use GUID markers |
| Session leak | High | Medium | Idle eviction + disposal pattern |

## 10. Metrics
| Metric | Target |
|--------|--------|
| Session leak rate | 0 after tests |
| Avg command round-trip | <400ms triage commands |
| Forced eviction occurrences | Alert if >10/day |

## 11. Rollout Strategy
Feature branch -> tests -> merge; increments minor version.

## 12. Open Questions
| ID | Question | Owner | Due |
|----|----------|-------|-----|
| Q1 | Use System.Diagnostics.Process vs dbgeng COM? | TBD | 2025-09-23 |

## 13. Changelog
| Version | Date | Author | Summary |
|---------|------|--------|---------|
| v0.1 | 2025-09-19 | TBD | Initial draft |
