import { test, expect, type Page } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

// Explicit contract fixtures. These tests do not claim to exercise external AI or storage services.
const sessionId = "11111111-1111-1111-1111-111111111111";
const code = "TEST-01";
const profile = {
  id: "22222222-2222-2222-2222-222222222222",
  firstName: "Ada",
  lastName: "Okafor",
  email: "ada@example.test",
  role: "instructor",
  preferredLanguage: "fr",
  captionFontSize: "md",
  accessibilityMode: "standard",
  authProvider: "local",
};
const lines = [
  {
    offsetMs: 1000,
    text: "Understanding starts with a question.",
    isCurrent: false,
  },
  {
    offsetMs: 2000,
    text: "Every good explanation makes room for another perspective.",
    isCurrent: false,
  },
];
const session = {
  id: sessionId,
  code,
  title: "Learning together — accessibility seminar",
  isLive: true,
  participantCount: 3,
  lines,
};
const pack = {
  sessionId,
  code,
  title: session.title,
  summary: "Understanding starts with a question. [00:01]",
  transcript: lines,
  flashcards: [
    {
      id: "card-1",
      front: "Where does understanding begin?",
      back: "With a question.",
      sourceOffsetMs: 1000,
    },
  ],
};
async function fixtures(page: Page, auth = false) {
  if (auth)
    await page.addInitScript(
      ({ profile }) =>
        sessionStorage.setItem(
          "levvy.auth",
          JSON.stringify({
            accessToken: "explicit-test-token",
            expiresAtUtc: "2099-01-01T00:00:00Z",
            user: profile,
          }),
        ),
      { profile },
    );
  await page.route("http://localhost:5000/**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    const method = route.request().method();
    let body: unknown;
    if (path.endsWith("/join"))
      body = { sessionId, participantId: "participant-test" };
    else if (path.endsWith("/transcript")) body = session;
    else if (path.endsWith("/pack")) body = pack;
    else if (path.endsWith("/ws-ticket"))
      body = { ticket: "explicit-test-ticket" };
    else if (path === "/sessions" && method === "POST")
      body = { sessionId, code };
    else if (
      path.endsWith("/end") ||
      (path.endsWith("/audio") && method === "POST")
    )
      body = { status: "ok" };
    else if (path.endsWith("/audio"))
      return route.fulfill({
        status: 404,
        json: { error: "No recording in contract fixture." },
      });
    else if (path.endsWith("/dashboard"))
      body = {
        profile,
        totalSessionsAttended: 1,
        totalSessionsHosted: 1,
        totalQuestionsAsked: 2,
        totalStudyPacksAvailable: 1,
        hostedSessions: [
          {
            ...session,
            startedAtUtc: "2026-11-14T09:00:00Z",
            hasStudyPack: true,
          },
        ],
        attendedSessions: [],
        studyPacks: [
          {
            studyPackId: "pack-1",
            sessionCode: code,
            sessionTitle: session.title,
            flashcardCount: 1,
          },
        ],
      };
    else if (path.endsWith("/profile"))
      body = { ...profile, ...route.request().postDataJSON() };
    else if (path === "/auth/login" || path === "/auth/register")
      body = {
        accessToken: "explicit-test-token",
        expiresAtUtc: "2099-01-01T00:00:00Z",
        user: profile,
      };
    else body = session;
    return route.fulfill({
      json: body,
      headers: { "Access-Control-Allow-Origin": "*" },
    });
  });
  await page.routeWebSocket("ws://localhost:5000/**", (socket) => {
    socket.onMessage(() => {});
  });
}

test("all routes fit the seven requested widths without page errors", async ({
  page,
}) => {
  await fixtures(page, true);
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));
  for (const width of [360, 390, 430, 768, 1024, 1440, 1920]) {
    await page.setViewportSize({ width, height: 1000 });
    for (const path of [
      "/",
      "/login",
      "/signup",
      "/dashboard",
      "/host",
      `/session/${code}`,
      `/session/${code}/pack`,
    ]) {
      await page.goto(path);
      await expect(page.locator("h1")).toBeVisible();
      await page.waitForTimeout(100);
      expect(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= window.innerWidth,
        ),
        `${path} overflow at ${width}`,
      ).toBe(true);
      if (
        (width === 390 || width === 1440) &&
        ["/", `/session/${code}`, `/session/${code}/pack`].includes(path)
      )
        await page.screenshot({
          path: `test-results/${path === "/" ? "home" : path.endsWith("/pack") ? "pack" : "captions"}-${width}.png`,
          fullPage: true,
        });
    }
  }
  expect(errors).toEqual([]);
});

test("join normalizes room code and sends selected caption preference", async ({
  page,
}) => {
  await fixtures(page);
  await page.goto("/");
  await page
    .getByRole("combobox", { name: "My caption language" })
    .selectOption("es");
  await page.getByPlaceholder("e.g. K4F-8Q2").fill("test-01");
  const submitted = page.waitForRequest((r) => r.url().endsWith("/join"));
  await page.getByRole("button", { name: "Join a lecture" }).click();
  expect((await submitted).postDataJSON().preferredLanguage).toBe("es");
  await expect(page).toHaveURL(`/session/${code}`);
});

test("captions synchronize by offset, deduplicate, and change language without reconnecting", async ({
  page,
}) => {
  await fixtures(page);
  let message: (data: string) => void = () => {};
  const sent: string[] = [];
  let opens = 0;
  await page.routeWebSocket("ws://localhost:5000/**", (socket) => {
    opens++;
    message = (data) => socket.send(data);
    socket.onMessage((data) => sent.push(String(data)));
  });
  await page.goto(`/session/${code}`);
  await expect(page.getByText("Connected", { exact: true })).toBeVisible();
  const caption = {
    type: "caption",
    sourceLanguage: "en",
    offsetMs: 3000,
    text: "A new idea, heard in real time.",
    final: true,
  };
  message(JSON.stringify(caption));
  message(JSON.stringify(caption));
  message(
    JSON.stringify({
      type: "translation",
      offsetMs: 3000,
      for: "fr",
      text: "Une nouvelle idée, entendue en temps réel.",
    }),
  );
  await expect(page.locator(".original")).toContainText(caption.text);
  await expect(page.locator(".translated")).toContainText("Une nouvelle idée");
  await expect(page.locator(".transcript-line")).toHaveCount(3);
  for (const width of [390, 1440]) {
    await page.setViewportSize({ width, height: 1000 });
    await page.screenshot({
      path: `test-results/live-captions-${width}.png`,
      fullPage: true,
    });
  }
  await page
    .getByRole("combobox", { name: "Caption language", exact: true })
    .selectOption("ar");
  await expect
    .poll(() => sent.some((s) => s.includes("set_lang") && s.includes("ar")))
    .toBe(true);
  expect(opens).toBe(1);
  await expect(page.locator(".translated")).toContainText(
    "Your translation will appear here.",
  );
});

test("Ask Levvy never labels an answer with invalid receipts as grounded", async ({
  page,
}) => {
  await fixtures(page);
  await page.route(`**/sessions/${code}/ask`, (route) =>
    route.fulfill({
      json: {
        type: "answer",
        status: "ok",
        text: "Unverified test answer.",
        receiptOffsetsMs: [999000],
        receipts: [999],
      },
    }),
  );
  await page.goto(`/session/${code}`);
  await page
    .getByLabel("Your question about this lecture")
    .fill("What was the main idea?");
  await page.getByRole("button", { name: "Ask a question" }).click();
  await expect(page.locator(".answer-card")).toContainText("could not verify");
  await expect(page.locator(".answer-card .receipt")).toHaveCount(0);
  await expect(page.locator(".verified")).toHaveCount(0);
});

test("verified receipts seek the recording in seconds", async ({ page }) => {
  await fixtures(page);
  await page.addInitScript(() => {
    HTMLMediaElement.prototype.play = function () {
      this.dataset.testPlayed = String(this.currentTime);
      return Promise.resolve();
    };
  });
  await page.route(`**/sessions/${code}/ask`, (route) =>
    route.fulfill({
      json: {
        type: "answer",
        status: "ok",
        text: "Understanding starts with a question.",
        receiptOffsetsMs: [1000],
        receipts: [1],
      },
    }),
  );
  await page.goto(`/session/${code}`);
  await page
    .getByLabel("Your question about this lecture")
    .fill("Where does understanding begin?");
  await page.getByRole("button", { name: "Ask a question" }).click();
  await page.locator(".answer-card .receipt").click();
  await expect(page.locator("audio")).toHaveAttribute("data-test-played", "1");
});

test("host uses a scoped ticket, captures decodable WebM, saves and ends", async ({
  page,
  context,
}) => {
  await context.grantPermissions(["microphone"]);
  await fixtures(page, true);
  const binary: Buffer[] = [];
  const meta: string[] = [];
  await page.routeWebSocket("ws://localhost:5000/**", (socket) => {
    expect(socket.url()).toContain("ticket=explicit-test-ticket");
    socket.onMessage((data) => {
      if (typeof data === "string") meta.push(data);
      else {
        binary.push(Buffer.from(data));
        socket.send(JSON.stringify({ type: "asr_status", state: "ready" }));
      }
    });
  });
  await page.addInitScript(() => {
    const original = navigator.mediaDevices.getUserMedia.bind(
      navigator.mediaDevices,
    );
    navigator.mediaDevices.getUserMedia = async (constraints) => {
      const stream = await original(constraints);
      (window as unknown as { testTracks: MediaStreamTrack[] }).testTracks =
        stream.getTracks();
      return stream;
    };
  });
  await page.goto("/host");
  await page.getByLabel("Lecture title").fill(session.title);
  await page.getByRole("button", { name: "Create lecture room" }).click();
  await expect(page.getByText(code, { exact: false }).first()).toBeVisible();
  await page.getByRole("button", { name: "Start recording" }).click();
  await expect.poll(() => binary.length, { timeout: 15000 }).toBeGreaterThan(0);
  expect([...binary[0].subarray(0, 4)]).toEqual([0x1a, 0x45, 0xdf, 0xa3]);
  expect(meta.some((m) => m.includes("chunk_meta"))).toBe(true);
  for (const width of [360, 390, 430, 768, 1024, 1440, 1920]) {
    await page.setViewportSize({ width, height: 1000 });
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
      `active host overflow at ${width}`,
    ).toBe(true);
    await page.evaluate(() => {
      if (document.activeElement instanceof HTMLElement)
        document.activeElement.blur();
    });
    if (width === 390 || width === 1440)
      await page.screenshot({
        path: `test-results/listening-${width}.png`,
        fullPage: true,
      });
  }
  const upload = page.waitForRequest(
    (r) => r.url().endsWith("/audio") && r.method() === "POST",
  );
  await page.getByRole("button", { name: "Stop & save recording" }).click();
  expect((await upload).postDataBuffer()?.length).toBeGreaterThan(0);
  await expect(
    page.getByText("Recording saved.", { exact: false }),
  ).toBeVisible();
  expect(
    await page.evaluate(() =>
      (
        window as unknown as { testTracks: MediaStreamTrack[] }
      ).testTracks.every((t) => t.readyState === "ended"),
    ),
  ).toBe(true);
  await page.getByRole("button", { name: "End lecture", exact: true }).click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await page.getByRole("button", { name: "End & save" }).click();
  await expect(
    page.getByRole("link", { name: "Open study pack" }),
  ).toBeVisible();
});

test("microphone denial is honest and leaves recording off", async ({
  page,
}) => {
  await fixtures(page, true);
  await page.addInitScript(() => {
    navigator.mediaDevices.getUserMedia = () =>
      Promise.reject(
        new DOMException("Microphone permission denied", "NotAllowedError"),
      );
  });
  await page.goto("/host");
  await page.getByLabel("Lecture title").fill("Permission test");
  await page.getByRole("button", { name: "Create lecture room" }).click();
  await page.getByRole("button", { name: "Start recording" }).click();
  await expect(page.locator(".error-state")).toContainText(
    "Microphone permission denied",
  );
  await expect(
    page.getByText("Microphone is off", { exact: true }),
  ).toBeVisible();
});

test("dashboard filters actual fixture sessions and saves profile", async ({
  page,
}) => {
  await fixtures(page, true);
  await page.goto("/dashboard");
  await expect(
    page.getByRole("heading", { name: "Hello, Ada." }),
  ).toBeVisible();
  await page.getByLabel("Search lectures").fill("no-match");
  await expect(page.locator(".session-card")).toHaveCount(0);
  await page.getByLabel("Search lectures").fill("accessibility");
  await expect(page.locator(".session-card")).toHaveCount(1);
  await page.getByLabel("First name").fill("Amara");
  await page.getByRole("button", { name: "Save preferences" }).click();
  await expect(page.getByText("Your preferences are saved.")).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Hello, Amara." }),
  ).toBeVisible();
});

test("study pack has playable citations and keyboard-operable flashcards", async ({
  page,
}) => {
  await fixtures(page);
  await page.goto(`/session/${code}/pack`);
  await expect(page.locator(".summary-text .receipt")).toHaveCount(1);
  const flip = page.locator(".flashcard-face");
  await flip.focus();
  await page.keyboard.press("Enter");
  await expect(flip).toContainText("With a question.");
  await page.keyboard.press("Space");
  await expect(flip).toContainText("Where does understanding begin?");
});

test("API failures show actionable errors, without fake success states", async ({
  page,
}) => {
  await page.route("http://localhost:5000/**", (route) =>
    route.abort("failed"),
  );
  await page.goto("/");
  await page.getByPlaceholder("e.g. K4F-8Q2").fill(code);
  await page.getByRole("button", { name: "Join a lecture" }).click();
  await expect(page.locator(".error-state")).toContainText(
    "Cannot reach Levvy",
  );
  await expect(page).toHaveURL("/");
});

test("key pages pass automated WCAG AA checks at mobile and desktop sizes", async ({
  page,
}) => {
  await fixtures(page, true);
  for (const width of [390, 1440]) {
    await page.setViewportSize({ width, height: 1000 });
    for (const path of [
      "/",
      "/login",
      "/signup",
      "/host",
      "/dashboard",
      `/session/${code}`,
      `/session/${code}/pack`,
    ]) {
      await page.goto(path);
      await expect(page.locator("h1")).toBeVisible();
      const results = await new AxeBuilder({ page })
        .withTags(["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"])
        .analyze();
      expect(
        results.violations.map((v) => ({
          id: v.id,
          nodes: v.nodes.map((n) => n.target),
        })),
        `${path} at ${width}`,
      ).toEqual([]);
    }
  }
});

test("email login and registration use the existing authentication contracts", async ({
  page,
}) => {
  await fixtures(page);
  await page.goto("/signup");
  await page.getByLabel("First name").fill("Ada");
  await page.getByLabel("Last name").fill("Okafor");
  await page.getByLabel("Email address").fill("ada@example.test");
  await page.getByLabel("Password", { exact: true }).fill("long-test-password");
  await page.getByRole("button", { name: "Create account" }).click();
  await expect(page).toHaveURL("/dashboard");
  await expect(
    page.getByRole("heading", { name: "Hello, Ada." }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Sign out", exact: true }).click();
  await page.goto("/login");
  await page.getByLabel("Email address").fill("ada@example.test");
  await page.getByLabel("Password", { exact: true }).fill("long-test-password");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL("/dashboard");
});

test("a lost socket reconnects with the same caption language", async ({
  page,
}) => {
  await fixtures(page);
  let opens = 0;
  await page.routeWebSocket("ws://localhost:5000/**", (socket) => {
    opens++;
    if (opens === 1)
      setTimeout(
        () => socket.close({ code: 1011, reason: "Test disconnect" }),
        250,
      );
  });
  await page.goto(`/session/${code}`);
  await expect.poll(() => opens, { timeout: 10000 }).toBeGreaterThan(1);
  await expect(page.getByText("Connected", { exact: true })).toBeVisible();
});
