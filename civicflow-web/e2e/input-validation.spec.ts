import { expect, test } from "@playwright/test"
import { E2E_ORGANIZATION_NAME, E2E_PASSWORD } from "./test-data"

const runId = Date.now().toString(36) + "-" + Math.random().toString(36).slice(2, 8)
const mockUserId = "a0000000-0000-0000-0000-000000000001"
const mockOrganizationId = "11111111-1111-1111-1111-111111111111"

test("registration blocks invalid fields with accessible messages", async ({
  page,
}) => {
  await page.goto("/citizen/register")
  await expect(
    page.getByRole("heading", { name: "Create a Citizen Account" }),
  ).toBeVisible()

  await page.getByRole("button", { name: "Register" }).click()

  await expect(
    page.getByText("Fix the highlighted fields before creating your account."),
  ).toBeVisible()
  await expect(page.locator("#register-full-name-error")).toHaveText(
    "Full name is required.",
  )
  await expect(page.locator("#register-email-error")).toHaveText(
    "Email address is required.",
  )
  await expect(page.locator("#register-phone-error")).toHaveText(
    "Phone number is required.",
  )
  await expect(page.locator("#register-nid-error")).toHaveText(
    "National ID number is required.",
  )
  await expect(page.locator("#register-citizenship-error")).toHaveText(
    "Citizenship number is required.",
  )
  await expect(page.locator("#register-password-error")).toHaveText(
    "Enter a password.",
  )

  await page.getByLabel("Full name").fill("   ")
  await page.getByLabel("Email").fill("not-an-email")
  await page.getByLabel("Phone number").fill("123")
  await page.getByLabel("NID number").fill("   ")
  await page.getByLabel("Citizenship number").fill("   ")
  await page.getByLabel("Password").fill("short")
  await page.getByRole("button", { name: "Register" }).click()

  await expect(page.locator("#register-full-name-error")).toHaveText(
    "Full name is required.",
  )
  await expect(page.locator("#register-email-error")).toHaveText(
    "Enter a valid email address, for example name@example.com.",
  )
  await expect(page.locator("#register-phone-error")).toHaveText(
    "Phone number must be at least 6 characters.",
  )
  await expect(page.locator("#register-password-error")).toHaveText(
    "Use at least 12 characters.",
  )
  await expect(page).toHaveURL(/\/citizen\/register$/)
})

test("registration normalizes padded input before calling the API", async ({
  page,
}) => {
  const suffix = runId
  const email = "E2E.Validation." + suffix + "@Example.TEST"
  const captured: Record<string, string> = {}

  await page.route("**/api/auth/register", async (route) => {
    Object.assign(captured, route.request().postDataJSON() as Record<string, string>)
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ succeeded: true, message: "Registration received." }),
    })
  })

  await page.goto("/citizen/register")
  await page.getByLabel("Full name").fill("  E2E   Validation Citizen  ")
  await page.getByLabel("Email").fill("  " + email + "  ")
  await page.getByLabel("Phone number").fill(" 9800000099 ")
  await page.getByLabel("NID number").fill("  nid-" + suffix + "  ")
  await page.getByLabel("Citizenship number").fill("  cit-" + suffix + "  ")
  await page.getByLabel("Password").fill(E2E_PASSWORD)
  await page.getByRole("button", { name: "Register" }).click()

  await expect(
    page.getByRole("heading", { name: "Verify your email" }),
  ).toBeVisible()

  expect(captured.fullName).toBe("E2E Validation Citizen")
  expect(captured.email).toBe(email.toLowerCase())
  expect(captured.phoneNumber).toBe("9800000099")
  expect(captured.nidNumber).toBe(("NID-" + suffix).toUpperCase())
  expect(captured.citizenshipNumber).toBe(("CIT-" + suffix).toUpperCase())
})

test("password recovery reports accessible email errors and normalizes the address", async ({
  page,
}) => {
  const captured: { body: Record<string, string> | null } = { body: null }

  await page.route("**/api/auth/me", async (route) => {
    await route.fulfill({ status: 401, body: "" })
  })
  await page.route("**/api/auth/forgot-password", async (route) => {
    captured.body = route.request().postDataJSON() as Record<string, string>
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        succeeded: true,
        message: "Recovery request received.",
      }),
    })
  })

  await page.goto("/forgot-password")
  await expect(
    page.getByRole("heading", { name: "Reset your password" }),
  ).toBeVisible()

  await page.getByRole("button", { name: "Send recovery link" }).click()
  await expect(page.locator("#recovery-email-error")).toHaveText(
    "Email address is required.",
  )
  await expect(page.getByLabel("Email")).toBeFocused()
  await expect(page.getByLabel("Email")).toHaveAttribute("aria-invalid", "true")

  await page.getByLabel("Email").fill("not-an-email")
  await page.getByRole("button", { name: "Send recovery link" }).click()
  await expect(page.locator("#recovery-email-error")).toHaveText(
    "Enter a valid email address, for example name@example.com.",
  )
  await expect(page.getByLabel("Email")).toBeFocused()

  await page.getByLabel("Email").fill("  E2E.Recovery@Example.TEST  ")
  await page.getByRole("button", { name: "Send recovery link" }).click()

  await expect(page.getByRole("status")).toContainText(
    "If e2e.recovery@example.test is registered",
  )
  expect(captured.body?.email).toBe("e2e.recovery@example.test")
})

test("public intake reports field errors and submits normalized case text", async ({
  page,
}) => {
  const captured: { body: string | null } = { body: null }

  await page.route("**/api/auth/me", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        id: mockUserId,
        fullName: "E2E Validation Citizen",
        email: "e2e.validation@example.test",
        platformRole: "None",
        emailConfirmed: true,
        roles: ["citizen"],
        permissions: ["citizen.self"],
      }),
    })
  })
  await page.route("**/api/notifications/unread-count", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ count: 0 }),
    })
  })
  await page.route("**/api/citizens/organizations", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify([
        {
          id: mockOrganizationId,
          name: E2E_ORGANIZATION_NAME,
          code: "CIVICFLOW-DEV",
        },
      ]),
    })
  })
  await page.route("**/api/citizens/cases", async (route) => {
    if (route.request().method() === "POST") {
      captured.body = route.request().postData()
      await route.fulfill({
        status: 201,
        contentType: "application/json",
        body: JSON.stringify({
          id: "22222222-2222-2222-2222-222222222222",
          referenceNumber: "CF-VALIDATION",
          additionalDocument: null,
        }),
      })
      return
    }

    await route.continue()
  })

  await page.goto("/workspace")
  await page.getByRole("button", { name: "Open public intake" }).click()
  await expect(
    page.getByRole("heading", { name: "Request public assistance" }),
  ).toBeVisible()

  const organization = page.getByLabel("Organization")
  await organization.selectOption("")
  await page.getByLabel("Issue title").fill("   ")
  await page.getByLabel("Details").fill("short")
  await page.getByRole("button", { name: "Submit request" }).click()

  await expect(
    page.getByText("Fix the highlighted fields before submitting your request."),
  ).toBeVisible()
  await expect(page.locator("#intake-organization-error")).toHaveText(
    "Select an organization.",
  )
  await expect(page.locator("#intake-title-error")).toHaveText(
    "Issue title is required.",
  )
  await expect(page.locator("#intake-description-error")).toHaveText(
    "Details must be at least 10 characters.",
  )

  const normalizedTitle = "E2E validation intake " + runId
  const description =
    "Intake validation fixture " + runId + " with sufficient detail."

  await organization.selectOption({ label: E2E_ORGANIZATION_NAME })
  await page.getByLabel("Issue title").fill("  E2E   validation   intake  " + runId)
  await page.getByLabel("Details").fill("  " + description + "  ")

  await page.getByRole("button", { name: "Submit request" }).click()

  await expect(page.locator(".citizen-success")).toContainText(
    "CF-VALIDATION",
  )
  expect(captured.body).not.toBeNull()
  expect(captured.body).toContain("name=\"title\"")
  expect(captured.body).toContain(normalizedTitle)
  expect(captured.body).toContain(description)
  expect(captured.body).not.toContain("  E2E   validation")
})
