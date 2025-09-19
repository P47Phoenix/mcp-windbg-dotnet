# PRD: Core Project Skeleton & MCP Bootstrapping

## Metadata
| Field | Value |
|-------|-------|
| PRD ID | PRD-core-skeleton |
| Version | v0.2 (in-progress) |
| Owner | TBD |
| Stakeholders | Eng, Product |
| Created | 2025-09-19 |
| Last Updated | 2025-09-19 |
| Status | In Delivery |
| Related ADRs | ADR-0001 |

## 1. Problem Statement
We need an initial .NET implementation of an MCP server enabling future Windows debugging tooling. Provide baseline repository structure, build, packaging, and a minimal MCP handshake with a placeholder tool to confirm connectivity.

## 2. Goals
- Create .NET solution and project layout for server
- Implement MCP stdio host with dummy health tool
- Provide configuration model (JSON + env overrides)
- Provide build script & CI placeholder (future)
- Add initial docs & usage instructions

### Non-Goals
- Actual WinDBG integration
- Command execution or sessions
- Symbol or CLR integration

## 3. Users & Personas
| Persona | Need | Success Signal |
|---------|------|----------------|
| AI Agent Runtime | Discover server tools | Tool list returns health tool |
| Developer | Run and debug server locally | Single command start & responds |

## 4. Use Cases
| ID | Story | Priority | Acceptance Notes |
|----|-------|----------|------------------|
| UC-1 | As a dev I can start the MCP server | Must | Returns tool list |
| UC-2 | As a client I can invoke a health tool | Must | Returns status JSON |

## 5. Solution Overview
Implement a console app (.NET 8) using standard input/output for MCP protocol. Provide one tool: `health_check` returning version and uptime.

## 6. Functional Requirements
| FR ID | Description | Acceptance Criteria |
|-------|-------------|--------------------|
| FR-1 | Provide tool enumeration | `list_tools` includes health_check |
| FR-2 | Handle health_check | Returns JSON with serverVersion & timestamp |
| FR-3 | Config loading | Accept config via appsettings.json + ENV prefix `MWD_` |

## 7. Non-Functional Requirements
| NFR ID | Category | Requirement | Measure |
|--------|----------|------------|---------|
| NFR-1 | Performance | Tool list latency | <200ms local |
| NFR-2 | Maintainability | Code structure layering | Separate Config, Tools, Protocol namespaces |
| NFR-3 | Reliability | Graceful shutdown | Ctrl+C disposes services |

## 8. Dependencies & Integrations
| Type | Name | Purpose | Risk | Mitigation |
|------|------|---------|------|------------|
| External | MCP protocol lib (future) | Compatibility | Low | Track spec version |

## 9. Risks & Mitigations
| Risk | Impact | Likelihood | Mitigation | Owner | Trigger |
|------|--------|-----------|-----------|-------|---------|
| Over-design early | Slows delivery | Medium | Limit scope to 1 tool | TBD | PR review |

## 10. Metrics
| Metric | Baseline | Target |
|--------|----------|--------|
| Startup time | - | <1s |
| Health tool latency | - | <150ms |

## 11. Rollout Strategy
Single commit; manual verification only. Tag `v0.1.0` when merged.

## 12. Open Questions
| ID | Question | Owner | Due |
|----|----------|-------|-----|
| Q1 | Use existing MCP .NET lib or custom? | TBD | 2025-09-22 |

## 13. Changelog
| Version | Date | Author | Summary |
|---------|------|--------|---------|
| v0.1 | 2025-09-19 | TBD | Initial draft |
| v0.2 | 2025-09-19 | TBD | Scaffold implementation started |

