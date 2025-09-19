# Acceptance Checklist Template

Use this before approving a release.

## Metadata
| Field | Value |
|-------|-------|
| Related PRD(s) | PRD-<id> |
| Release Version | <semver / tag> |
| Date | <YYYY-MM-DD> |
| Approver | <name/role> |

## 1. Functional Coverage
- [ ] All Must-have stories implemented
- [ ] All Should-have stories implemented or deferred with ADR
- [ ] Negative paths tested

## 2. Quality Gates
| Gate | Status (Pass/Fail/N/A) | Evidence Link |
|------|------------------------|---------------|
| Unit Test Coverage >= threshold |  |  |
| Integration Tests Green |  |  |
| Performance p95 within budget |  |  |
| Error Rate below threshold |  |  |
| Security review completed |  |  |

## 3. Documentation
- [ ] README updated
- [ ] PRD updated to reflect final scope
- [ ] Feature flags documented

## 4. Observability
| Aspect | Status | Notes |
|--------|--------|-------|
| Logs adequate |  |  |
| Metrics emitted |  |  |
| Alerts configured |  |  |
| Dashboards updated |  |  |

## 5. Deployment & Rollback
- [ ] Rollback plan validated
- [ ] Migration scripts idempotent
- [ ] Feature flags default safe state

## 6. Risks Reviewed
- [ ] High-impact risks have mitigation owners
- [ ] New risks since PRD captured & logged

## 7. Sign-Off
| Role | Name | Date | Decision |
|------|------|------|----------|
| Product |  |  | Approved |
| Engineering |  |  | Approved |
| QA |  |  | Approved |

## 8. Post-Release Monitoring Plan
| Metric | Threshold | Action |
|--------|-----------|--------|

