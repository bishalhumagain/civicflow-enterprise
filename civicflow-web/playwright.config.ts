import { defineConfig, devices } from '@playwright/test'

const apiUrl =
  process.env.CIVICFLOW_E2E_API_URL?.replace(/\/$/, '') ?? 'http://localhost:5001'
const webUrl =
  process.env.CIVICFLOW_E2E_WEB_URL?.replace(/\/$/, '') ?? 'http://localhost:5173'

/**
 * Browser journeys run serially against a disposable PostgreSQL database.
 * The API and Vite servers are reused when they are already running so a
 * developer can keep a debugging session open; CI starts them from scratch.
 */
export default defineConfig({
  testDir: './e2e',
  globalSetup: './e2e/global-setup.ts',
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  timeout: 90_000,
  expect: { timeout: 10_000 },
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: webUrl,
    locale: 'en-US',
    timezoneId: 'UTC',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: [
    {
      command:
        'dotnet run --project ../PublicServiceCaseManagement.Api/' +
        'PublicServiceCaseManagement.Api.csproj --configuration Release ' +
        '--urls http://localhost:5001',
      url: `${apiUrl}/api/auth/csrf`,
      reuseExistingServer: !process.env.CI,
      timeout: 180_000,
    },
    {
      command:
        'npm run dev -- --host 127.0.0.1 --port 5173 --strictPort',
      url: webUrl,
      env: {
        VITE_API_BASE_URL: apiUrl,
      },
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
    },
  ],
})
