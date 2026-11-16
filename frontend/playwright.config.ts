import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests/e2e',
  workers: 1,
  timeout: 60_000,
  outputDir: '../.local/stage4/browser-results',
  reporter: [['list']],
  use: { headless: true, trace: 'off', screenshot: 'off' },
  projects: process.env.MAILHUB_BROWSER_EXECUTABLE
    ? [
        {
          name: 'installed-chromium',
          use: {
            browserName: 'chromium',
            launchOptions: { executablePath: process.env.MAILHUB_BROWSER_EXECUTABLE },
          },
        },
      ]
    : [
        { name: 'chrome', use: { channel: 'chrome' } },
        { name: 'edge', use: { channel: 'msedge' } },
      ],
});
