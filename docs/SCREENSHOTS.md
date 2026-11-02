# Screenshot Guide

The repository intentionally contains no screenshots. Capture the real
application with synthetic, non-sensitive data, then add images at the
suggested relative paths and replace the matching README placeholder with a
Markdown image link.

| Screenshot ID | Suggested filename | Application path | Required role | What it should demonstrate | Capture notes |
|---|---|---|---|---|---|
| `platform-admin-overview` | `docs/screenshots/platform-admin-overview.png` | Sign in to the Platform Admin portal → Dashboard | Platform Admin (`SuperAdmin`) | Platform-level summary cards and navigation. | Use synthetic organizations and counts. Keep browser chrome and personal account details out of frame. |
| `organization-admin-my-organization` | `docs/screenshots/organization-admin-my-organization.png` | Organization workspace → My Organization | Organization Admin | Current organization profile, team contacts, and visible organization-level staff roles. | Use one synthetic organization; ensure no other organization's data is visible. |
| `staff-assigned-case` | `docs/screenshots/staff-assigned-case.png` | Organization workspace → Cases → open a case assigned to the signed-in CaseWorker | Staff with a `CaseWorker` assignment | Assigned-case context, status/history, and permitted staff actions. | Use a synthetic case; do not include real citizen information or credentials. |
| `citizen-case-tracking` | `docs/screenshots/citizen-case-tracking.png` | Citizen portal → My Cases → open one of the citizen's cases | Citizen | Citizen-owned case list and citizen-visible progress timeline. | Use a synthetic account and case; keep internal notes and staff-only documents out of the capture. |
| `authorized-document-preview` | `docs/screenshots/authorized-document-preview.png` | Organization workspace → Cases → open an authorized case → Documents → Preview | Organization Admin or assigned `CaseWorker` | Document preview opened through the authorized case interface. | Use a non-sensitive synthetic PDF or image. Do not capture local storage paths or browser developer tools. |

## Capture checklist

- Capture the current application, not mockups or reconstructed UI.
- Use synthetic case names, organizations, email addresses, and document
  contents only.
- Confirm the selected role can access the displayed data before capturing.
- Keep authentication cookies, tokens, private URLs, local filesystem paths,
  and browser developer tools out of the image.
- Prefer a consistent viewport and scale; check legibility at normal README
  width.
- Add each image under `docs/screenshots/` using the exact filename above.
- Replace only the matching placeholder in `README.md`; preserve its
  screenshot ID and update this guide if the navigation path changes.
