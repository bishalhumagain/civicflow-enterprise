# Screenshot Guide

Real application captures with synthetic data live under `docs/screenshots/`.
The five captures below cover every user group and the authorized document
flow, which is the smallest set that explains the project end to end.

| Screenshot ID | Status | Suggested filename | Application path | Required role | What it should demonstrate | Capture notes |
|---|---|---|---|---|---|---|
| `citizen-case-tracking` | Added | `docs/screenshots/citizen-case-tracking.png` | Citizen portal → My cases → open one of the citizen's cases | Citizen | Citizen-owned case list, citizen-visible progress timeline, updates, and documents. | Use a synthetic account and case; keep internal notes and staff-only documents out of the capture. |
| `staff-assigned-case` | Added | `docs/screenshots/staff-assigned-case.png` | Organization workspace → Cases → open a case assigned to the signed-in CaseWorker | Staff with a `CaseWorker` assignment | Assigned-case context, workflow controls, explicit **Save changes** bar, documents, communication, and history. | Use a synthetic case; do not include real citizen information or credentials. The capture should show that there is no delete action. |
| `organization-admin-my-organization` | Added | `docs/screenshots/organization-admin-my-organization.png` | Organization workspace → My Organization | Organization Admin | Current organization profile, team contacts, memberships, and invitations. | Use one synthetic organization; ensure no other organization's data is visible. |
| `platform-admin-overview` | Added | `docs/screenshots/platform-admin-overview.png` | Sign in to the Platform Admin portal → Dashboard | Platform Admin (`SuperAdmin`) | Platform-level summary cards, case distribution, and navigation. | Use synthetic organizations and counts. Keep browser chrome and personal account details out of frame. |
| `authorized-document-preview` | Added | `docs/screenshots/authorized-document-preview.png` | Organization workspace → Cases → open an authorized case → Documents → Preview | Organization Admin or assigned `CaseWorker` | Document preview opened through the authorized case interface. | Use a non-sensitive synthetic PDF or image. Do not capture local storage paths or browser developer tools. |

## Coverage

| Project area | Screenshot |
|---|---|
| Citizen intake, tracking, and citizen-visible communication | `citizen-case-tracking` |
| Staff case management, workflow, documents, and history | `staff-assigned-case` |
| Organization administration, memberships, and invitations | `organization-admin-my-organization` |
| Platform administration and platform-wide reporting | `platform-admin-overview` |
| Authorized document preview | `authorized-document-preview` |

This set intentionally focuses on the screens that best explain the project
rather than every state. Validation errors, empty states, audit and background
job screens, and other secondary views are covered by the automated tests and
API documentation instead of additional screenshots.

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
- Update the matching image and caption in `README.md` when a screen changes.
  Keep the screenshot ID in this guide and update the navigation path or
  capture notes if they change.
