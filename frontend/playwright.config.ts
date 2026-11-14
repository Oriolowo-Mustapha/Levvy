import { defineConfig } from "@playwright/test";
const baseURL = process.env.PLAYWRIGHT_BASE_URL || "http://127.0.0.1:3000";
export default defineConfig({
  testDir: "./tests",
  fullyParallel: false,
  timeout: 60000,
  reporter: [["list"], ["json", { outputFile: "test-results/results.json" }]],
  use: {
    baseURL,
    channel:
      process.env.PLAYWRIGHT_CHANNEL ||
      (process.platform === "win32" ? "chrome" : undefined),
    headless: true,
    launchOptions: {
      args: [
        "--use-fake-device-for-media-stream",
        "--use-fake-ui-for-media-stream",
      ],
    },
    trace: "retain-on-failure",
  },
  webServer: {
    command: process.env.PLAYWRIGHT_USE_PRODUCTION
      ? "npm run start"
      : `npm run dev -- --hostname 127.0.0.1 --port ${new URL(baseURL).port || "3000"}`,
    env: { PORT: new URL(baseURL).port || "3000", HOSTNAME: "127.0.0.1" },
    url: baseURL,
    reuseExistingServer: true,
  },
});
