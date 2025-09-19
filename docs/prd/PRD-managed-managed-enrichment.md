# PRD: Managed (.NET) Runtime Enrichment

## Metadata
| Field | Value |
|-------|-------|
| PRD ID | PRD-managed-enrichment |
| Version | v0.1 (draft) |
| Owner | TBD |
| Stakeholders | Eng, Support |
| Created | 2025-09-19 |
| Last Updated | 2025-09-19 |
| Status | Draft |
| Related ADRs | ADR-0001 |

## 1. Problem Statement
Native CDB output is verbose and lacks structured managed runtime insight (threads, exceptions, GC heaps). Need enriched analysis for .NET crash dumps to accelerate triage.

## 2. Goals
- Integrate CLRMD to inspect managed dump state
- Provide tool `analyze_managed_context` returning JSON + Markdown summary
- Include: top managed exception chain, managed threads with top frames, finalizer queue length, GC heap size by generation, LOH usage, loaded assemblies snapshot
- Optional: deadlock heuristic (threads waiting on same sync object)

### Non-Goals
- Full memory graph / object retention tree
- Large heap diffing

## 3. Users & Personas
| Persona | Need | Success |
|---------|------|--------|
| Support Engineer | Identify root exception quickly | Top exception surfaced |
| AI Agent | Structured telemetry for reasoning | JSON schema output |

## 4. Use Cases
| ID | Story | Priority | Acceptance |
|----|-------|----------|-----------|
| UC-1 | Get managed exception summary | Must | Shows type, message, first/last seen |
| UC-2 | List busy threads | Must | Includes top 3 managed frames |
| UC-3 | GC heap overview | Must | Sizes per generation + LOH |
| UC-4 | Assembly inventory | Should | Name + version + path |
| UC-5 | Deadlock heuristic | Could | Flag potential cycles |

## 5. Solution Overview
Load dump with CLRMD DataTarget; create analysis pipeline producing structured DTO then dual-render JSON + Markdown. Provide schema version for forward compatibility.

## 6. Functional Requirements
| FR ID | Description | Acceptance |
|-------|-------------|-----------|
| FR-1 | Load managed context | CLRMD DataTarget loads without memory leak |
| FR-2 | Exception extraction | Primary & inner exceptions captured |
| FR-3 | Thread summary | Top waiting/blocking status + frames |
| FR-4 | GC heap stats | Generation sizes & LOH/POH |
| FR-5 | Assembly list | Name, Version, Path |
| FR-6 | JSON + Markdown output | Both formats available |

## 7. Non-Functional Requirements
| NFR ID | Category | Requirement | Measure |
|--------|----------|------------|---------|
| NFR-1 | Performance | Managed analysis duration | <2s medium dump |
| NFR-2 | Memory | Additional working set | <300MB overhead |
| NFR-3 | Observability | Include analysis duration metric | Logged |

## 8. Dependencies
| Type | Name | Purpose | Risk | Mitigation |
|------|------|---------|------|------------|
| External | CLRMD | Managed inspection | API changes | Pin version |

## 9. Risks & Mitigations
| Risk | Impact | Likelihood | Mitigation |
|------|--------|-----------|-----------|
| CLR version mismatch | Failure to load | Medium | Graceful fallback to native only |
| Large heap slows analysis | Latency | Medium | Early size sampling, skip details |
| JSON schema drift | Consumer breakage | Low | Versioned `schemaVersion` field |

## 10. Metrics
| Metric | Target |
|--------|--------|
| Managed analysis success rate | 99% |
| Avg managed analysis duration | <1.5s |
| Deadlock heuristic false positives | <10% (qual tests) |

## 11. Rollout Strategy
Behind feature flag `ManagedEnrichmentEnabled`; default off first release; enable after validation.

## 12. Open Questions
| ID | Question | Owner | Due |
|----|----------|-------|-----|
| Q1 | Provide optional per-thread full stacks? | TBD | 2025-10-03 |

## 13. Changelog
| Version | Date | Author | Summary |
|---------|------|--------|---------|
| v0.1 | 2025-09-19 | TBD | Initial draft |
