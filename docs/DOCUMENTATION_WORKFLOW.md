# Documentation Workflow

This document defines how CivicFlow Enterprise documentation is maintained to ensure accuracy as the project evolves. For the full documentation index, see the [README](../README.md) → Documentation section.

---

## Table of Contents

- [Documentation Files](#documentation-files)
- [When Functionality Changes](#when-functionality-changes)
- [Documentation Accuracy Rules](#documentation-accuracy-rules)
- [Maintenance Checklist](#maintenance-checklist)

---

## Documentation Files

| File | Audience | When to update |
|---|---|---|
| `README.md` | All audiences (developers, managers, operators, security reviewers) | Setup, project structure, features, prerequisites, technology stack, API reference, or development commands change |
| `CHANGELOG.md` | All audiences | Any meaningful change (features, fixes, breaking changes, important updates) |
| `docs/USER_MANUAL.md` | End users (citizens, staff, administrators) | User-facing behavior, screens, workflows, permissions, roles, or error handling change |
| `docs/DELIVERY-ROADMAP.md` | Project managers, developers | Implementation status, delivery sequence, or testing strategy changes |
| `docs/INTEGRATION-VERIFICATION.md` | Security reviewers, developers | Test coverage, verification results, or acceptance evidence change |
| `docs/RLS-DESIGN.md` | Security reviewers, database engineers | RLS policies, protected tables, or database role configuration change |
| `docs/RELEASE-VERIFICATION.md` | Release engineers, security reviewers | Release signing workflow, provenance metadata, or CI pipeline changes |
| `docs/DOCUMENTATION_WORKFLOW.md` | All contributors | This file — update when the documentation process itself changes |
| `CIVICFLOW-PROVENANCE.json` | Release engineers | Project identity metadata (owner, repository, license status, release verification) |

---

## When Functionality Changes

1. **Inspect changes** — Review the modified source code, tests, migrations, and configuration to understand the actual implementation changes.
2. **Update README** — If setup, project structure, features, prerequisites, technology stack, API reference, or development commands change, update `README.md`. Keep it well-structured and accessible to all audiences.
3. **Update User Manual** — If user-facing behavior, screens, workflows, permissions, roles, or error handling change, update `docs/USER_MANUAL.md`. Document only verified working functionality.
4. **Update Delivery Roadmap** — If implementation status, delivery sequence, or testing strategy changes, update `docs/DELIVERY-ROADMAP.md`.
5. **Update Integration Verification** — If test coverage or verification evidence changes, update `docs/INTEGRATION-VERIFICATION.md`.
6. **Update other docs** — Update RLS design, release verification, or this workflow doc when those areas change.
7. **Changelog** — Add an entry to `CHANGELOG.md` for meaningful changes (features, fixes, breaking changes, important updates). Do not invent hypothetical entries.
8. **Validate** — Check links, commands, formatting, and claims. Cross-verify against actual code.
9. **Report uncertainty** — If verification is incomplete, explicitly document limitations.

---

## Documentation Accuracy Rules

- **Verify before documenting** — Do not document planned features as implemented. Distinguish implemented vs planned.
- **Evidence-based** — Base claims on actual code, tests, configs. A class/folder/name alone is not proof.
- **Preserve limitations** — Do not remove known limitations until functionality is verified working.
- **Avoid duplication** — Link between docs rather than copying large sections. The README is the central reference; other docs should link to it rather than repeating content.
- **No secrets** — Never include credentials, tokens, passwords, keys, or personal data.
- **Be explicit about uncertainty** — When implementation status is unclear, state it.
- **No production-readiness claims** — Don't claim readiness without verification.
- **Keep the table of contents updated** — Each doc has a TOC; update it when sections are added or removed.
- **Cross-reference** — Use relative links to other docs. The README has a documentation index linking all docs.

---

## Maintenance Checklist

Before submitting changes, verify:

- [ ] Claims match verified implementation
- [ ] Commands tested against actual project files
- [ ] Relative links valid (check all `../README.md` and inter-doc links)
- [ ] No secrets exposed
- [ ] Status classifications consistent across docs (README, roadmap, integration verification, user manual)
- [ ] User manual reflects actual UI/workflows
- [ ] Table of contents in each doc reflects current sections
- [ ] CHANGELOG has an entry for the documentation change
- [ ] New features appear in the README's Key Capabilities and API Reference
- [ ] New user-facing features appear in the User Manual
