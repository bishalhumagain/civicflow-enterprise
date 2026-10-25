import { expect, test, type Page } from '@playwright/test'
import {
  E2E_CITIZEN_ONE,
  E2E_CITIZEN_TWO,
  E2E_ORGANIZATION_NAME,
  E2E_PASSWORD,
  E2E_PLATFORM_ADMIN,
  E2E_SECOND_ORGANIZATION_NAME,
  E2E_STAFF_ADMIN,
} from './test-data'

const apiUrl =
  process.env.CIVICFLOW_E2E_API_URL?.replace(/\/$/, '') ?? 'http://localhost:5001'

const runId = `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`
const title = `E2E pothole inspection ${runId}`
const description =
  `Automated browser journey ${runId}: inspect the pothole in the ` +
  'northbound lane near the community library.'
const citizenDocumentName = `citizen-evidence-${runId}.txt`
const staffVisibleDocumentName = `shared-inspection-${runId}.txt`
const staffInternalDocumentName = `internal-assessment-${runId}.txt`
const publicUpdate = `Public update ${runId}: the inspection is scheduled.`
const statusExplanation =
  `Visible status note ${runId}: the case team is reviewing the report.`
const internalNote =
  `Internal note ${runId}: never expose this staff-only assessment.`
const informationRequest =
  `Information request ${runId}: please share the nearest landmark.`
const citizenResponse =
  `Citizen response ${runId}: the pothole is opposite the community library.`
const pagingPrefix = `E2E paging ${runId}`

const state: {
  caseId: string
  referenceNumber: string
} = {
  caseId: '',
  referenceNumber: '',
}

test.describe.configure({ mode: 'serial' })

async function signIn(
  page: Page,
  path: '/citizen/login' | '/admin' | '/super-platform-admin',
  email: string,
  heading: string | RegExp,
): Promise<void> {
  await page.goto(path)
  await expect(page.getByRole('heading', { name: heading })).toBeVisible()
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Password').fill(E2E_PASSWORD)
  await page.getByRole('button', { name: 'Sign in', exact: true }).click()
}

async function openCitizenDashboard(page: Page): Promise<void> {
  await signIn(page, '/citizen/login', E2E_CITIZEN_ONE, 'Citizen Sign In')
  await expect(
    page.getByRole('heading', { name: /Welcome back, E2E/ }),
  ).toBeVisible()
}

async function getCsrfToken(page: Page): Promise<string> {
  const response = await page.request.get(`${apiUrl}/api/auth/csrf`)
  expect(response.ok()).toBeTruthy()
  const body = (await response.json()) as { token: string }
  expect(body.token).toBeTruthy()
  return body.token
}

async function createCitizenCasesViaApi(
  page: Page,
  prefix: string,
  count: number,
): Promise<void> {
  const organizationsResponse = await page.request.get(
    `${apiUrl}/api/citizens/organizations`,
  )
  expect(organizationsResponse.ok()).toBeTruthy()
  const organizations = (await organizationsResponse.json()) as Array<{
    id: string
  }>
  const organizationId = organizations[0]?.id
  expect(organizationId).toBeTruthy()

  const token = await getCsrfToken(page)

  for (let index = 1; index <= count; index += 1) {
    const response = await page.request.post(`${apiUrl}/api/citizens/cases`, {
      headers: { 'X-CSRF-TOKEN': token },
      multipart: {
        organizationId: organizationId!,
        title: `${prefix} ${index}`,
        description:
          `Pagination fixture ${index} for the deterministic browser ` +
          'tracking suite.',
      },
    })

    if (response.status() !== 201) {
      throw new Error(
        `Pagination fixture ${index} failed with HTTP ${response.status()}: ` +
          (await response.text()),
      )
    }
  }
}

test('citizen submits a request and opens its tracking detail', async ({
  page,
}) => {
  await openCitizenDashboard(page)

  await page.getByRole('button', { name: 'Public intake', exact: true }).click()
  await expect(
    page.getByRole('heading', { name: 'Request public assistance' }),
  ).toBeVisible()

  await page
    .getByLabel('Organization')
    .selectOption({ label: E2E_ORGANIZATION_NAME })
  await page.getByLabel('Issue title').fill(title)
  await page.getByLabel('Details').fill(description)
  await page
    .getByLabel(/Additional document/)
    .setInputFiles({
      name: citizenDocumentName,
      mimeType: 'text/plain',
      buffer: Buffer.from(`Citizen evidence for ${runId}`),
    })

  const submissionResponse = page.waitForResponse(
    (response) =>
      response.url().endsWith('/api/citizens/cases') &&
      response.request().method() === 'POST',
  )

  await page.getByRole('button', { name: 'Submit request' }).click()

  const response = await submissionResponse
  const responseBody = await response.text()
  expect(response.status(), responseBody).toBe(201)
  const submission = JSON.parse(responseBody) as {
    id: string
    referenceNumber: string
  }
  state.caseId = submission.id
  state.referenceNumber = submission.referenceNumber
  expect(state.referenceNumber).toMatch(/^CF-/)

  await expect(page.locator('.citizen-success')).toContainText(
    `Request ${state.referenceNumber} was submitted successfully with ${citizenDocumentName}.`,
  )

  await page.getByRole('button', { name: 'Track this request' }).click()

  await expect(
    page.getByRole('heading', { name: 'Request details' }),
  ).toBeVisible()
  const detail = page.locator('article.citizen-case-detail')
  await expect(detail.getByRole('heading', { name: title })).toBeVisible()
  await expect(
    detail.getByText(`Reference ${state.referenceNumber}`),
  ).toBeVisible()
  await expect(detail.locator('.status-pill')).toHaveText('Submitted')
  await expect(
    detail.getByRole('heading', { name: 'Status timeline' }),
  ).toBeVisible()
  await expect(
    detail.getByText('Request submitted', { exact: true }),
  ).toBeVisible()
  await expect(
    detail.locator('.citizen-document-list').getByText(citizenDocumentName),
  ).toBeVisible()

  const downloadPromise = page.waitForEvent('download')
  await detail.getByRole('button', { name: 'Download' }).click()
  const download = await downloadPromise
  expect(download.suggestedFilename()).toBe(citizenDocumentName)
})

test('staff records public updates, a confidential note, an information request, and document visibility', async ({
  browser,
}) => {
  expect(state.caseId).toBeTruthy()

  const context = await browser.newContext()
  const page = await context.newPage()

  try {
    await signIn(
      page,
      '/admin',
      E2E_STAFF_ADMIN,
      'Organization Sign In',
    )
    await expect(
      page.getByRole('heading', { name: 'Case management' }),
    ).toBeVisible()

    const search = page.getByLabel('Search cases')
    await expect(search).toBeVisible()
    await search.fill(title)

    const row = page
      .getByRole('region', { name: 'Cases list' })
      .locator('tbody tr')
      .filter({ hasText: title })
    await expect(row).toBeVisible()
    await row.getByRole('button').click()

    const detail = page.locator('aside.detail-card')
    await expect(detail.getByText(state.referenceNumber)).toBeVisible()
    await expect(detail.getByLabel('Case title')).toHaveValue(title)

    const statusForm = detail.locator('.status-change-form')
    await statusForm.locator('select').selectOption('UnderReview')
    await statusForm.locator('textarea').fill(statusExplanation)
    await statusForm
      .getByRole('checkbox', { name: /Include this explanation/ })
      .check()
    await detail
      .getByRole('button', { name: 'Apply status change' })
      .click()

    await expect(detail.locator('.status-pill')).toHaveText('Under review')

    // An invalid jump must surface the server rule instead of silently
    // pretending the transition succeeded.
    await statusForm.locator('select').selectOption('Closed')
    await detail
      .getByRole('button', { name: 'Apply status change' })
      .click()
    await expect(
      page
        .locator('main')
        .getByRole('alert')
        .filter({ hasText: 'A case cannot transition from UnderReview to Closed.' }),
    ).toBeVisible()
    await statusForm.locator('select').selectOption('UnderReview')

    const communication = detail
      .locator('.detail-section')
      .filter({ has: page.getByRole('heading', { name: 'Citizen communication' }) })

    await communication.getByLabel('Update type').selectOption('PublicUpdate')
    await communication.getByLabel('Message').fill(publicUpdate)
    await communication
      .getByRole('button', { name: 'Publish update' })
      .click()
    const publicUpdateEntry = communication
      .locator('.case-update')
      .filter({ hasText: publicUpdate })
    await expect(publicUpdateEntry).toBeVisible()
    await expect(publicUpdateEntry.locator('.visibility-badge')).toHaveText(
      'Visible to citizen',
    )

    await communication.getByLabel('Update type').selectOption('InternalNote')
    await communication.getByLabel('Message').fill(internalNote)
    await communication
      .getByRole('button', { name: 'Publish update' })
      .click()
    const internalEntry = communication
      .locator('.case-update')
      .filter({ hasText: internalNote })
    await expect(internalEntry).toBeVisible()
    await expect(internalEntry.locator('.visibility-badge')).toHaveText(
      'Staff only',
    )

    await communication
      .getByLabel('Update type')
      .selectOption('InformationRequest')
    await communication.getByLabel('Message').fill(informationRequest)
    await communication
      .getByRole('button', { name: 'Publish update' })
      .click()
    await expect(
      communication.locator('.case-update').filter({ hasText: informationRequest }),
    ).toBeVisible()
    await expect(detail.locator('.status-pill')).toHaveText(
      'Awaiting information',
    )

    const documents = detail
      .locator('.detail-section')
      .filter({ has: page.getByRole('heading', { name: 'Documents' }) })
    const sharedCheckbox = documents.getByRole('checkbox', {
      name: 'Share new documents with the citizen',
    })

    await sharedCheckbox.check()
    const visibleUpload = page.waitForResponse(
      (response) =>
        response.url().endsWith(`/api/cases/${state.caseId}/documents`) &&
        response.request().method() === 'POST',
    )
    await documents
      .getByLabel('Attach supporting document')
      .setInputFiles({
        name: staffVisibleDocumentName,
        mimeType: 'text/plain',
        buffer: Buffer.from(`Citizen-readable inspection ${runId}`),
      })
    expect((await visibleUpload).status()).toBe(201)
    const visibleDocument = documents
      .locator('li')
      .filter({ hasText: staffVisibleDocumentName })
    await expect(visibleDocument).toBeVisible()
    await expect(visibleDocument.locator('.visibility-badge')).toHaveText(
      'Shared with citizen',
    )

    await sharedCheckbox.uncheck()
    const internalUpload = page.waitForResponse(
      (response) =>
        response.url().endsWith(`/api/cases/${state.caseId}/documents`) &&
        response.request().method() === 'POST',
    )
    await documents
      .getByLabel('Attach supporting document')
      .setInputFiles({
        name: staffInternalDocumentName,
        mimeType: 'text/plain',
        buffer: Buffer.from(`Staff-only assessment ${runId}`),
      })
    expect((await internalUpload).status()).toBe(201)
    const internalDocument = documents
      .locator('li')
      .filter({ hasText: staffInternalDocumentName })
    await expect(internalDocument).toBeVisible()
    await expect(internalDocument.locator('.visibility-badge')).toHaveText(
      'Internal',
    )

    const statusHistory = detail
      .locator('.detail-section')
      .filter({ has: page.getByRole('heading', { name: 'Status history' }) })
    await expect(statusHistory.getByText(statusExplanation)).toBeVisible()
    await expect(
      statusHistory
        .locator('.timeline-explanation')
        .filter({ hasText: statusExplanation })
        .locator('.visibility-badge'),
    ).toHaveText('Visible to citizen')
  } finally {
    await context.close()
  }
})

test('citizen sees only permitted timeline data and can answer the information request', async ({
  page,
}) => {
  await openCitizenDashboard(page)

  const search = page.getByLabel('Search my requests')
  await search.fill(title)
  const card = page
    .locator('.citizen-request-grid article')
    .filter({ hasText: title })
  await expect(card).toBeVisible()
  await card.getByRole('button', { name: 'View details' }).click()

  const detail = page.locator('article.citizen-case-detail')
  await expect(detail.locator('.status-pill')).toHaveText(
    'Awaiting information',
  )
  await expect(
    detail.getByText(
      'The case team needs more information from you. Review the request below and add your response.',
    ),
  ).toBeVisible()

  await expect(
    detail.getByText('Under review → Awaiting information'),
  ).toBeVisible()
  await expect(detail.getByText(statusExplanation)).toBeVisible()

  const updates = detail.locator('.citizen-update')
  const publicEntry = updates.filter({ hasText: publicUpdate })
  await expect(publicEntry).toContainText('Update from the case team')
  const requestEntry = updates.filter({ hasText: informationRequest })
  await expect(requestEntry).toContainText('Information requested')

  // Internal notes and internal documents never reach the citizen response.
  await expect(detail.getByText(internalNote)).toHaveCount(0)
  await expect(detail.getByText(staffInternalDocumentName)).toHaveCount(0)

  const visibleDocument = detail
    .locator('.citizen-document-list li')
    .filter({ hasText: staffVisibleDocumentName })
  await expect(visibleDocument).toBeVisible()

  const downloadPromise = page.waitForEvent('download')
  await visibleDocument.getByRole('button', { name: 'Download' }).click()
  expect((await downloadPromise).suggestedFilename()).toBe(
    staffVisibleDocumentName,
  )

  await detail.getByLabel('Your message').fill(citizenResponse)
  await detail.getByRole('button', { name: 'Send response' }).click()

  await expect(
    page
      .locator('.success-message')
      .filter({ hasText: 'Your response was sent to the case team.' }),
  ).toBeVisible()
  await expect(detail.locator('.status-pill')).toHaveText('Under review')
  await expect(
    detail.getByText('Awaiting information → Under review'),
  ).toBeVisible()
  await expect(
    detail.locator('.citizen-update').filter({ hasText: citizenResponse }),
  ).toContainText('Your response')
  await expect(
    detail.getByText(
      'The case team needs more information from you. Review the request below and add your response.',
    ),
  ).toHaveCount(0)

  await detail
    .getByRole('button', { name: 'Back to my requests' })
    .first()
    .click()

  await page.getByLabel('Search my requests').fill(runId)
  await expect(card).toBeVisible()
  await page
    .getByLabel('Filter my requests by status')
    .selectOption('UnderReview')
  await expect(card).toBeVisible()

  await page
    .getByLabel('Filter my requests by status')
    .selectOption('Resolved')
  await expect(
    page.getByText('No requests match the current search and filters.'),
  ).toBeVisible()
  await page.getByRole('button', { name: 'Clear filters' }).click()

  await page.getByLabel('Sort my requests').selectOption('created_asc')
  await expect(page.getByLabel('Sort my requests')).toHaveValue('created_asc')

  await createCitizenCasesViaApi(page, pagingPrefix, 12)
  await page.getByLabel('Search my requests').fill(pagingPrefix)
  await expect(page.getByText(/Showing 10 of 12/)).toBeVisible()
  await expect(page.locator('.pagination')).toContainText('Page 1 of 2')

  await page.locator('.pagination').getByRole('button', { name: 'Next' }).click()
  await expect(page.getByText(/Showing 2 of 12/)).toBeVisible()
  await expect(page.locator('.pagination')).toContainText('Page 2 of 2')
  await expect(page.locator('.citizen-request-grid article')).toHaveCount(2)
})

test('another citizen cannot read, mutate, or infer the case', async ({
  browser,
}) => {
  const context = await browser.newContext()
  const page = await context.newPage()

  try {
    await signIn(page, '/citizen/login', E2E_CITIZEN_TWO, 'Citizen Sign In')
    await expect(
      page.getByRole('heading', { name: /Welcome back, E2E/ }),
    ).toBeVisible()

    await page.getByLabel('Search my requests').fill(title)
    await expect(
      page.getByText('No requests match the current search and filters.'),
    ).toBeVisible()

    const caseResponse = await page.request.get(
      `${apiUrl}/api/citizens/cases/${state.caseId}`,
    )
    expect(caseResponse.status()).toBe(404)

    const documentsResponse = await page.request.get(
      `${apiUrl}/api/citizens/cases/${state.caseId}/documents`,
    )
    expect(documentsResponse.status()).toBe(404)

    const token = await getCsrfToken(page)
    const responseAttempt = await page.request.post(
      `${apiUrl}/api/citizens/cases/${state.caseId}/responses`,
      {
        headers: { 'X-CSRF-TOKEN': token },
        data: { message: 'Cross-citizen write attempt.' },
      },
    )
    expect(responseAttempt.status()).toBe(404)

    const staffAttempt = await page.request.get(
      `${apiUrl}/api/cases/${state.caseId}`,
    )
    expect(staffAttempt.status()).toBe(403)

    await context.clearCookies()
    const expiredSession = await page.request.get(
      `${apiUrl}/api/citizens/cases`,
    )
    expect(expiredSession.status()).toBe(401)

    await page.goto('/workspace')
    await expect(
      page.getByRole('heading', { name: 'Citizen Portal' }),
    ).toBeVisible()
  } finally {
    await context.close()
  }
})

test('mutations are rejected when the anti-forgery token is invalid', async ({
  page,
}) => {
  await openCitizenDashboard(page)

  // Fetching a token also establishes the anti-forgery cookie; only the
  // echoed request header is deliberately wrong in the mutation below.
  await getCsrfToken(page)

  const organizationsResponse = await page.request.get(
    `${apiUrl}/api/citizens/organizations`,
  )
  const organizations = (await organizationsResponse.json()) as Array<{
    id: string
  }>

  const response = await page.request.post(`${apiUrl}/api/citizens/cases`, {
    headers: { 'X-CSRF-TOKEN': 'deliberately-invalid' },
    multipart: {
      organizationId: organizations[0].id,
      title: `E2E csrf rejection ${runId}`,
      description:
        'This request must fail before any case is persisted by the API.',
    },
  })

  expect(response.status()).toBe(400)
  const body = (await response.json()) as { code?: string }
  expect(body.code).toBe('antiforgery_invalid')
})

test('organization administrator filters every case in the organization', async ({
  browser,
}) => {
  const context = await browser.newContext()
  const page = await context.newPage()

  try {
    await signIn(page, '/admin', E2E_STAFF_ADMIN, 'Organization Sign In')
    await expect(
      page.getByRole('heading', { name: 'Case management' }),
    ).toBeVisible()
    await expect(
      page.getByRole('button', { name: 'Cases', exact: true }),
    ).toBeVisible()

    const rows = page.getByRole('region', { name: 'Cases list' })
    const filteredRow = rows.locator('tbody tr').filter({ hasText: title })

    await page.getByLabel('Filter by status').selectOption('UnderReview')
    await expect(filteredRow).toBeVisible()

    await page.getByLabel('Filter by priority').selectOption('High')
    await expect(
      page.getByText('No cases match the current filter.'),
    ).toBeVisible()

    await page.getByLabel('Filter by priority').selectOption('Normal')
    await expect(filteredRow).toBeVisible()

    await page.getByLabel('Search cases').fill(runId)
    await expect(filteredRow).toBeVisible()
    await expect(
      page.getByText(/Showing 1 of \d+/),
    ).toBeVisible()
  } finally {
    await context.close()
  }
})

test('platform administrator reviews all cases and can filter by organization', async ({
  browser,
}) => {
  const context = await browser.newContext()
  const page = await context.newPage()

  try {
    await signIn(
      page,
      '/super-platform-admin',
      E2E_PLATFORM_ADMIN,
      'Platform Administrator Sign In',
    )

    await expect(
      page.getByRole('heading', { name: 'Platform overview' }),
    ).toBeVisible()
    await expect(
      page.getByRole('heading', { name: 'Cases by organization' }),
    ).toBeVisible()
    await expect(
      page.getByRole('heading', { name: 'Cases by status' }),
    ).toBeVisible()
    await expect(
      page.getByRole('heading', { name: 'Priority mix' }),
    ).toBeVisible()

    await page.getByRole('button', { name: 'All cases', exact: true }).click()
    await expect(
      page.getByRole('heading', { name: 'All cases' }),
    ).toBeVisible()

    const table = page.getByRole('region', { name: 'Platform cases list' })
    const row = table.locator('tbody tr').filter({ hasText: title })

    await page.getByLabel('Organization').selectOption({
      label: E2E_SECOND_ORGANIZATION_NAME,
    })
    await expect(
      page.getByText('No cases match the current filters.'),
    ).toBeVisible()

    await page.getByLabel('Organization').selectOption({
      label: E2E_ORGANIZATION_NAME,
    })
    await expect(row).toBeVisible()

    await page.getByLabel('Status').selectOption('UnderReview')
    await expect(row).toBeVisible()

    await page.getByLabel('Priority').selectOption('High')
    await expect(
      page.getByText('No cases match the current filters.'),
    ).toBeVisible()

    await page.getByRole('button', { name: 'Reset', exact: true }).click()
    await page.getByLabel('Search all platform cases').fill(title)
    await expect(row).toBeVisible()

    await row.getByRole('button').click()

    const detail = page.locator('aside.platform-case-detail')
    await expect(detail).toContainText(state.referenceNumber)
    await expect(detail).toContainText(E2E_ORGANIZATION_NAME)
    await expect(detail).toContainText(E2E_CITIZEN_ONE)
    await expect(detail).toContainText(internalNote)
    await expect(detail).toContainText(staffInternalDocumentName)
    await expect(detail.getByText('Staff only')).toBeVisible()
    await expect(detail.getByText('Internal', { exact: true })).toBeVisible()
  } finally {
    await context.close()
  }
})
