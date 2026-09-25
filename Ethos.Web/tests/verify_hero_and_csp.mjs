import { chromium } from "playwright";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Read CSP from public/_headers
const headersContent = fs.readFileSync(path.resolve(__dirname, "../public/_headers"), "utf8");
const cspMatch = headersContent.split("\n").find((l) => l.trim().startsWith("Content-Security-Policy:"));
const cspHeader = cspMatch ? cspMatch.replace("Content-Security-Policy:", "").trim() : "";

const widths = [320, 360, 375, 390, 414, 430, 440, 1280, 1440];

const mockHeroMedia = [
  {
    id: "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    mediaType: "Video",
    publicUrl: "https://api.ethosdancestudio.com/api/media/content/3fa85f64-5717-4562-b3fc-2c963f66afa6",
    thumbnailUrl: "https://api.ethosdancestudio.com/api/media/content/3fa85f64-5717-4562-b3fc-2c963f66afa6",
    title: "Ethos Hero Cinematic Video",
    altText: "Ethos Dance Faculty Movement Showcase",
  },
  {
    id: "7cb85f64-5717-4562-b3fc-2c963f66afa7",
    mediaType: "Image",
    publicUrl: "https://api.ethosdancestudio.com/api/media/content/7cb85f64-5717-4562-b3fc-2c963f66afa7",
    title: "Ethos Studio Portrait",
    altText: "Ethos Dance Studio Main Floor",
  },
];

async function runHeroAndCspVerification() {
  console.log("Starting Hero Video & Mobile Typography CSP Verification...");
  console.log("Production CSP Header being applied:\n", cspHeader);

  const browser = await chromium.launch({ headless: true });
  const results = [];
  const cspViolations = [];

  for (const w of widths) {
    const isMobile = w <= 600;
    const context = await browser.newContext({
      viewport: { width: w, height: isMobile ? 800 : 900 },
      deviceScaleFactor: 1,
    });
    const page = await context.newPage();

    // Listen for any CSP violations or console warnings/errors
    page.on("console", (msg) => {
      const text = msg.text();
      if (
        text.toLowerCase().includes("content security policy") ||
        text.toLowerCase().includes("violates the following") ||
        text.toLowerCase().includes("blocked by csp")
      ) {
        cspViolations.push({ viewport: `${w}px`, error: text });
        console.error(`[CSP VIOLATION @ ${w}px]:`, text);
      }
    });

    // Intercept HTML response to inject the exact production CSP header meta tag
    await page.route("http://localhost:4173/", async (route) => {
      const response = await route.fetch();
      let body = await response.text();
      // Inject CSP meta tag right at head
      const cspMeta = `<meta http-equiv="Content-Security-Policy" content="${cspHeader}">`;
      body = body.replace("<head>", `<head>\n    ${cspMeta}`);
      await route.fulfill({
        response,
        body,
        headers: {
          ...response.headers(),
          "content-security-policy": cspHeader,
        },
      });
    });

    // Mock API media feed to return the video and image pointing to https://api.ethosdancestudio.com/api/media/content/...
    await page.route("**/api/media/public*", async (route) => {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(mockHeroMedia),
      });
    });

    // Mock the actual media binary streams to return valid 200/206 responses
    await page.route("https://api.ethosdancestudio.com/api/media/content/*", async (route) => {
      return route.fulfill({
        status: 200,
        contentType: "video/mp4",
        body: Buffer.from("mock-binary-video-content"),
      });
    });

    await page.goto("http://localhost:4173/", { waitUntil: "domcontentloaded" });
    // Wait for BrandIntro splash (2.2s) to complete so hero is active
    await page.waitForTimeout(2600);

    // Measure typography and layout
    const measurement = await page.evaluate(() => {
      const line1 = document.querySelector(".ethos-hero__title-line:first-child");
      const line2 = document.querySelector(".ethos-hero__title-line--accent");
      const container = document.querySelector(".ethos-hero__content");
      const title = document.querySelector(".ethos-hero__title");
      const video = document.querySelector(".ethos-hero__video");
      const image = document.querySelector(".ethos-hero__image");

      const range = document.createRange();
      range.selectNodeContents(line1);
      const textRect = range.getBoundingClientRect();
      const cRect = container ? container.getBoundingClientRect() : null;

      const range2 = document.createRange();
      range2.selectNodeContents(line2);
      const text2Rect = range2.getBoundingClientRect();

      const docW = document.documentElement.scrollWidth;
      const winW = window.innerWidth;

      return {
        viewportWidth: window.innerWidth,
        fontSize: window.getComputedStyle(line1).fontSize,
        textWidth: Math.round(textRect.width),
        accentWidth: Math.round(text2Rect.width),
        containerWidth: Math.round(cRect ? cRect.width : 0),
        overflow: Math.round(textRect.width - (cRect ? cRect.width : 0)),
        hasPageOverflow: docW > winW,
        videoSrc: video ? video.getAttribute("src") : null,
        videoRendered: Boolean(video),
      };
    });

    // Capture screenshot
    const screenshotPath = path.resolve(__dirname, `hero_verified_${w}px.png`);
    await page.screenshot({ path: screenshotPath, fullPage: false });

    results.push({
      width: w,
      ...measurement,
      screenshot: screenshotPath,
    });

    console.log(
      `[${w}px] Font: ${measurement.fontSize} | Text Width: ${measurement.textWidth}px | Container: ${measurement.containerWidth}px | Overflow: ${measurement.overflow <= 0 ? "NONE (PASS)" : `${measurement.overflow}px (FAIL)`} | Video: ${measurement.videoRendered ? "Rendered (PASS)" : "MISSING"} | PageOverflow: ${measurement.hasPageOverflow ? "FAIL" : "PASS"}`
    );

    await context.close();
  }

  await browser.close();

  console.log("\n=== CSP AUDIT SUMMARY ===");
  console.log(`Total CSP violations detected: ${cspViolations.length}`);

  return { results, cspViolations };
}

runHeroAndCspVerification()
  .then(({ results, cspViolations }) => {
    fs.writeFileSync(path.resolve(__dirname, "hero_csp_audit_results.json"), JSON.stringify({ results, cspViolations }, null, 2));
    console.log("Results saved to hero_csp_audit_results.json");
    if (cspViolations.length > 0) {
      console.error("CSP Violations detected!");
      process.exit(1);
    }
    const mobileResults = results.filter((r) => r.width <= 600);
    const hasMobileOverflow = mobileResults.some((r) => r.overflow > 0 || r.hasPageOverflow);
    if (hasMobileOverflow) {
      console.error("Mobile typography overflow detected!");
      process.exit(1);
    }
    const desktopResults = results.filter((r) => r.width > 600);
    const hasDesktopPageOverflow = desktopResults.some((r) => r.hasPageOverflow);
    if (hasDesktopPageOverflow) {
      console.error("Desktop page overflow detected!");
      process.exit(1);
    }
    console.log("All Hero mobile typography (320px-440px), desktop preservation, and CSP tests PASSED flawlessly!");
    process.exit(0);
  })
  .catch((err) => {
    console.error("Verification failed:", err);
    process.exit(1);
  });
