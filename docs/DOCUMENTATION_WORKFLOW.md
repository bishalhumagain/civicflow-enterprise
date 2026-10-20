# Documentation Workflow

This document defines how CivicFlow documentation is maintained to ensure accuracy as the project evolves.

## When functionality changes
1. **Inspect changes** - Review the modified source code, tests, migrations, and configuration to understand the actual implementation changes.
2. **Update README** - If setup, project structure, features, prerequisites, technology stack, or development commands change, update `README.md`. Keep it concise and developer-focused.
3. **Update User Manual** - If user-facing behavior, screens, workflows, permissions, roles, or error handling change, update `docs/USER_MANUAL.md`. Document only verified working functionality.
4. **Update other docs** - Update architecture/security/development docs when those areas change.
5. **Changelog** - Add an entry to `CHANGELOG.md` for meaningful changes (features, fixes, breaking changes, important updates). Do not invent hypothetical entries.
6. **Validate** - Check links, commands, formatting, and claims. Cross-verify against actual code.
7. **Report uncertainty** - If verification is incomplete, explicitly document limitations.

## Documentation accuracy rules
- **Verify before documenting** - Do not document planned features as implemented. Distinguish implemented vs planned.
- **Evidence-based** - Base claims on actual code, tests, configs. A class/folder/name alone is not proof.
- **Preserve limitations** - Do not remove known limitations until functionality is verified working.
- **Avoid duplication** - Link between docs rather than copying large sections.
- **No secrets** - Never include credentials, tokens, passwords, keys, or personal data.
- **Be explicit about uncertainty** - When implementation status is unclear, state it.
- **No production-readiness claims** - Don't claim readiness without verification.

## Maintenance checklist
- [ ] Claims match verified implementation
- [ ] Commands tested against actual project files
- [ ] Relative links valid
- [ ] No secrets exposed
- [ ] Status classifications consistent across docs
- [ ] User manual reflects actual UI/workflows
