# PRD: Command Parity & Analysis Bundle

## Metadata
| Field | Value |
|-------|-------|
| PRD ID | PRD-command-parity |
| Version | v0.1 (draft) |
| Owner | TBD |
| Stakeholders | Eng, AI Consumers |
| Created | 2025-09-19 |
| Last Updated | 2025-09-19 |
| Status | Draft |
| Related ADRs | ADR-0001 |

## 1. Problem Statement
Need to expose a minimal but high-value set of WinDBG commands via MCP mirroring the Python reference (triage analysis) while ensuring safety and consistent formatting.

## 2. Goals
- Implement tools equivalent to: open dump, open remote, run command, close dump, close remote, list dumps
- Add analysis convenience wrapper performing: `.lastevent`, `!analyze -v`, optional `kb`, `lm`, `~`, registers
- Provide allowlist enforcement & denial message
- Normalize output to Markdown + meta header

### Non-Goals
- Managed (.NET) specific introspection
- Advanced pattern recognition or heuristics

## 3. Users & Personas
| Persona | Need | Success |
|---------|------|--------|
| AI Agent | Quick crash triage | Single tool returns structured sections |
| Support Engineer | Fast cause hypothesis | See exception + top stack |

## 4. Use Cases
| ID | Story | Priority | Acceptance |
|----|-------|----------|-----------|
| UC-1 | Analyze dump triage | Must | Returns sections (Crash, Analysis, Stack) |
| UC-2 | Run allowed command | Must | Command output returned |
| UC-3 | Block disallowed command | Must | Error referencing policy |
| UC-4 | List dumps | Should | size & path info listed |

## 5. Solution Overview
Wrap existing session architecture; define `ICommandPolicy` for allowlist; implement renderer building Markdown document with TOC anchors.

## 6. Functional Requirements
| FR ID | Description | Acceptance |
|-------|-------------|-----------|
| FR-1 | open_dump tool | Validates path & spawns session |
| FR-2 | analyze_dump tool | Aggregates required commands |
| FR-3 | run_command tool | Enforces allowlist |
| FR-4 | list_dumps tool | Enumerates *.dmp with metadata |
| FR-5 | close_dump tool | Terminates session |
| FR-6 | open_remote tool | Remote attach path |
| FR-7 | close_remote tool | Remote detach |

## 7. Non-Functional Requirements
| NFR ID | Category | Requirement | Measure |
|--------|----------|------------|---------|
| NFR-1 | Performance | Analyze dump end-to-end | <5s for 500MB dump (warm symbols) |
| NFR-2 | Security | Deny not-allowlisted commands | 100% test coverage |
| NFR-3 | UX | Markdown stable section ordering | Deterministic tests |
| NFR-4 | Reliability | Error surfaces original CDB error | Included in output block |

## 8. Dependencies
| Type | Name | Purpose | Risk | Mitigation |
|------|------|---------|------|------------|
| External | cdb.exe | Execution | Symbols slow | Add symbol cache path guidance |

## 9. Risks & Mitigations
| Risk | Impact | Likelihood | Mitigation |
|------|--------|-----------|-----------|
| Symbol load slowness | Latency | High | Pre-configured SRV path, doc caching |
| Disallowed command bypass | Security | Low | Strict regex + unit tests |
| Output parsing drift | Formatting | Medium | Snapshot tests |

## 10. Metrics
| Metric | Target |
|--------|--------|
| Analyze average latency | <3s typical dumps |
| Allowlist violations blocked | 100% |
| Test coverage (command layer) | >=85% |

## 11. Rollout Strategy
Merge after feature flag default off -> enable after validation.

## 12. Open Questions
| ID | Question | Owner | Due |
|----|----------|-------|-----|
| Q1 | Provide JSON alternative early? | TBD | 2025-09-26 |

## 13. Changelog
| Version | Date | Author | Summary |
|---------|------|--------|---------|
| v0.1 | 2025-09-19 | TBD | Initial draft |
