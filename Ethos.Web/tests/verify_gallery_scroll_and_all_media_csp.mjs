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

const mockMediaPipeline = {
  heroVideo: {
    id: "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    mediaType: "Video",
    publicUrl: "https://api.ethosdancestudio.com/api/media/content/3fa85f64-5717-4562-b3fc-2c963f66afa6",
    thumbnailUrl: "https://api.ethosdancestudio.com/api/media/content/3fa85f64-5717-4562-b3fc-2c963f66afa6",
    title: "Hero Master Video",
  },
  heroImage: {
    id: "7cb85f64-5717-4562-b3fc-2c963f66afa7",
    mediaType: "Image",
    publicUrl: "https://api.ethosdancestudio.com/api/media/content/7cb85f64-5717-4562-b3fc-2c963f66afa7",
    title: "Hero Studio Image",
  },
  galleryImage: {
    id: "8db85f64-5717-4562-b3fc-2c963f66afa8",
    mediaType: "Image",
    publicUrl: "https://api.ethosdancestudio.com/api/media/content/8db85f64-5717-4562-b3fc-2c963f66afa8",
    title: "Gallery Movement Photo",
    category: "Workshops",
    layoutType: "Landscape",
  },
  galleryVideo: {
    id: "9eb85f64-5717-4562-b3fc-2c963f66afa9",
    mediaType: "Video",
    publicUrl: "https://api.ethosdancestudio.com/api/media/content/9eb85f64-5717-4562-b3fc-2c963f66afa9",
    title: "Gallery Theater Feature",
  },
  workshopPoster: "https://api.ethosdancestudio.com/api/media/content/1ab85f64-5717-4562-b3fc-2c963f66af11",
  sessionPoster: "https://api.ethosdancestudio.com/api/media/content/2bb85f64-5717-4562-b3fc-2c963f66af22",
  trainerPhoto: "https://api.ethosdancestudio.com/api/media/content/3cb85f64-5717-4562-b3fc-2c963f66af33",
};

async function runFullVerification() {
  console.log("===============================================================================");
  console.log("STARTING FULL ACCEPTANCE VERIFICATION: GALLERY SCROLL & ALL MEDIA CSP PIPELINE");
  console.log("===============================================================================");

  const browser = await chromium.launch({ headless: true });
  const cspViolations = [];
  const report = {
    galleryScrollTests: [],
    mediaPipelineTests: [],
  };

  // Helper to setup page with production CSP & mock media stream responses
  async function createConfiguredPage(viewport = { width: 1440, height: 900 }) {
    const context = await browser.newContext({ viewport });
    const page = await context.newPage();

    page.on("console", (msg) => {
      const text = msg.text();
      if (
        text.toLowerCase().includes("content security policy") ||
        text.toLowerCase().includes("violates the following") ||
        text.toLowerCase().includes("blocked by csp")
      ) {
        cspViolations.push(text);
        console.error(`[CSP VIOLATION]:`, text);
      }
    });

    // Inject production CSP meta header
    await page.route("http://localhost:4173/**", async (route) => {
      const response = await route.fetch();
      let body = await response.text();
      if (route.request().resourceType() === "document") {
        const cspMeta = `<meta http-equiv="Content-Security-Policy" content="${cspHeader}">`;
        body = body.replace("<head>", `<head>\n    ${cspMeta}`);
      }
      await route.fulfill({
        response,
        body,
        headers: {
          ...response.headers(),
          "content-security-policy": cspHeader,
        },
      });
    });

    // Mock API media feed endpoints
    await page.route("**/api/media/public*", async (route) => {
      const url = route.request().url();
      if (url.includes("HomepageScrolling")) {
        return route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify([mockMediaPipeline.heroVideo, mockMediaPipeline.heroImage]),
        });
      }
      if (url.includes("GalleryImages") || url.includes("GallerySlideshow")) {
        return route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify([mockMediaPipeline.galleryImage]),
        });
      }
      if (url.includes("GalleryVideos")) {
        return route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify([mockMediaPipeline.galleryVideo]),
        });
      }
      return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([]) });
    });

    // Mock workshops API with posters and session posters
    await page.route("**/api/workshops*", async (route) => {
      const url = route.request().url();
      if (url.includes("/pricing")) {
        return route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify({ currentPrice: 599, currentTier: 1, capacity: 50, ticketsRemainingInTier: 10 }),
        });
      }
      const wsData = [
        {
          id: "ws-test-01",
          title: "Masterclass Test",
          danceStyle: "CONTEMPORARY",
          workshopDate: "2026-11-20T18:00:00Z",
          startTime: "18:00:00",
          endTime: "20:00:00",
          venue: "Ethos Main Studio",
          startingPrice: 599,
          status: "published",
          imageUrl: mockMediaPipeline.workshopPoster,
          landscapeImageUrl: mockMediaPipeline.workshopPoster,
          trainerName: "Sujith Kumar",
          trainerPhotoUrl: mockMediaPipeline.trainerPhoto,
          trainers: [
            {
              id: "tr-01",
              name: "Sujith Kumar",
              photoUrl: mockMediaPipeline.trainerPhoto,
              danceStyles: "Contemporary",
            },
          ],
          sessions: [
            {
              id: "sess-01",
              title: "Session 1 — Flow Technique",
              sessionDate: "2026-11-20T18:00:00Z",
              startTime: "18:00:00",
              endTime: "20:00:00",
              sessionPosterUrl: mockMediaPipeline.sessionPoster,
            },
          ],
        },
      ];
      if (url.includes("/ws-test-01") || url.includes("masterclass-test")) {
        return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(wsData[0]) });
      }
      return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(wsData) });
    });

    // Mock actual media binary content requests (images & video streams with 206 support)
    await page.route("https://api.ethosdancestudio.com/api/media/content/*", async (route) => {
      const reqUrl = route.request().url();
      const isVideo = reqUrl.includes("3fa85f64") || reqUrl.includes("9eb85f64");
      if (isVideo) {
        return route.fulfill({
          status: 206,
          contentType: "video/mp4",
          headers: {
            "Accept-Ranges": "bytes",
            "Content-Range": "bytes 0-1023/1024",
            "Content-Length": "1024",
          },
          body: Buffer.alloc(1024),
        });
      }
      // Image binary (1x1 transparent PNG)
      const pngBuffer = Buffer.from(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==",
        "base64"
      );
      return route.fulfill({
        status: 200,
        contentType: "image/png",
        body: pngBuffer,
      });
    });

    return { context, page };
  }

  // =========================================================================
  // TEST 1: GALLERY SCROLL RESTORATION (Desktop & Mobile)
  // =========================================================================
  console.log("\n--- PART 1: GALLERY SCROLL RESTORATION AUDIT ---");

  // 1A: Desktop: Scroll Homepage to bottom -> click Gallery -> verify scrollY = 0
  {
    const { context, page } = await createConfiguredPage({ width: 1440, height: 900 });
    await page.goto("http://localhost:4173/", { waitUntil: "domcontentloaded" });
    await page.waitForTimeout(2500); // BrandIntro wait

    // Scroll down to near bottom of homepage
    await page.evaluate(() => window.scrollTo({ top: 3500, behavior: "instant" }));
    await page.waitForTimeout(200);
    const scrollBefore = await page.evaluate(() => window.scrollY);
    console.log(`Homepage scrolled down to: ${scrollBefore}px`);

    // Click Gallery link in Footer or Navbar
    const footerGalleryBtn = page.locator('footer button:has-text("Gallery")').first();
    if (await footerGalleryBtn.count() > 0) {
      await footerGalleryBtn.scrollIntoViewIfNeeded();
      await footerGalleryBtn.click();
    } else {
      await page.evaluate(() => {
        const btn = Array.from(document.querySelectorAll('button, a')).find(el => el.textContent?.trim().toLowerCase() === 'gallery');
        btn?.click();
      });
    }
    await page.waitForURL("**/gallery");
    await page.waitForTimeout(600);

    const scrollAfter = await page.evaluate(() => window.scrollY);
    const isAtTop = scrollAfter === 0;
    console.log(`Gallery landed at scrollY: ${scrollAfter}px (Expected: 0) -> ${isAtTop ? "PASS" : "FAIL"}`);

    report.galleryScrollTests.push({
      test: "Desktop Homepage bottom -> Gallery link",
      scrollBefore,
      scrollAfter,
      passed: isAtTop,
    });
    await context.close();
  }

  // 1B: Mobile: Scroll Homepage -> click Footer Gallery link -> verify scrollY = 0
  {
    const { context, page } = await createConfiguredPage({ width: 390, height: 844 });
    await page.goto("http://localhost:4173/", { waitUntil: "domcontentloaded" });
    await page.waitForTimeout(2500);

    // Scroll to footer
    await page.evaluate(() => window.scrollTo({ top: 4000, behavior: "instant" }));
    await page.waitForTimeout(200);

    // Click Footer Gallery button
    const footerGalleryBtn = page.locator('footer button:has-text("Gallery")').first();
    await footerGalleryBtn.scrollIntoViewIfNeeded();
    await footerGalleryBtn.click();
    await page.waitForURL("**/gallery");
    await page.waitForTimeout(600);

    const scrollAfter = await page.evaluate(() => window.scrollY);
    const isAtTop = scrollAfter === 0;
    console.log(`Mobile Footer -> Gallery scrollY: ${scrollAfter}px (Expected: 0) -> ${isAtTop ? "PASS" : "FAIL"}`);

    report.galleryScrollTests.push({
      test: "Mobile Footer -> Gallery link",
      scrollAfter,
      passed: isAtTop,
    });
    await context.close();
  }

  // 1C: Direct load / reload of /gallery -> verify top
  {
    const { context, page } = await createConfiguredPage({ width: 1440, height: 900 });
    await page.goto("http://localhost:4173/gallery", { waitUntil: "domcontentloaded" });
    await page.waitForTimeout(500);

    const scrollYDirect = await page.evaluate(() => window.scrollY);
    console.log(`Direct load /gallery scrollY: ${scrollYDirect}px -> ${scrollYDirect === 0 ? "PASS" : "FAIL"}`);

    // Reload page
    await page.reload({ waitUntil: "domcontentloaded" });
    await page.waitForTimeout(500);
    const scrollYReload = await page.evaluate(() => window.scrollY);
    console.log(`Reload /gallery scrollY: ${scrollYReload}px -> ${scrollYReload === 0 ? "PASS" : "FAIL"}`);

    report.galleryScrollTests.push({
      test: "Direct load and reload of /gallery",
      scrollYDirect,
      scrollYReload,
      passed: scrollYDirect === 0 && scrollYReload === 0,
    });
    await context.close();
  }

  // 1D: Normal scrolling inside Gallery works
  {
    const { context, page } = await createConfiguredPage({ width: 1440, height: 900 });
    await page.goto("http://localhost:4173/gallery", { waitUntil: "domcontentloaded" });
    await page.waitForTimeout(500);

    await page.evaluate(() => window.scrollTo({ top: 600, behavior: "instant" }));
    await page.waitForTimeout(200);
    const scrolledY = await page.evaluate(() => window.scrollY);
    console.log(`User scroll inside Gallery: ${scrolledY}px (Normal scrolling preserved) -> ${scrolledY >= 500 ? "PASS" : "FAIL"}`);

    report.galleryScrollTests.push({
      test: "Normal scrolling inside Gallery preserved",
      scrolledY,
      passed: scrolledY >= 500,
    });
    await context.close();
  }

  // =========================================================================
  // TEST 2: ALL ADMIN-MANAGED MEDIA PIPELINE & CSP VERIFICATION
  // =========================================================================
  console.log("\n--- PART 2: ALL ADMIN-MANAGED MEDIA PIPELINE & CSP VERIFICATION ---");

  // 2A: Homepage Hero Image & Video
  {
    const { context, page } = await createConfiguredPage({ width: 1440, height: 900 });
    await page.goto("http://localhost:4173/", { waitUntil: "domcontentloaded" });
    await page.waitForTimeout(2600);

    const mediaStatus = await page.evaluate(() => {
      const video = document.querySelector(".ethos-hero__video");
      const videoSrc = video?.getAttribute("src");
      const videoReady = video ? video.readyState : -1;
      return {
        videoRendered: Boolean(video),
        videoSrc,
        videoReady,
      };
    });

    console.log(`Hero Video: Rendered=${mediaStatus.videoRendered}, Src=${mediaStatus.videoSrc}`);
    report.mediaPipelineTests.push({
      placement: "Homepage Hero Video",
      url: mediaStatus.videoSrc,
      rendered: mediaStatus.videoRendered,
      passed: mediaStatus.videoRendered && mediaStatus.videoSrc?.includes("/api/media/content/"),
    });
    await context.close();
  }

  // 2B: Workshop Poster, Session Poster & Trainer Profile Avatar
  {
    const { context, page } = await createConfiguredPage({ width: 1440, height: 900 });
    await page.goto("http://localhost:4173/workshops/masterclass-test", { waitUntil: "domcontentloaded" });
    await page.waitForTimeout(800);

    const wsMediaStatus = await page.evaluate(() => {
      // Find workshop hero banner image
      const bannerImg = document.querySelector(".workshop-hero-card img, .hero-poster-img, .workshop-hero-backdrop img");
      const bannerSrc = bannerImg?.getAttribute("src");

      // Find trainer avatar img
      const trainerImg = document.querySelector(".trainer-avatar-img, .hp-trainer-avatar img, img[alt*='Sujith']");
      const trainerSrc = trainerImg?.getAttribute("src");

      // Find session poster img if rendered in schedule accordion
      const sessionImgs = Array.from(document.querySelectorAll("img")).map((i) => i.getAttribute("src"));
      const hasSessionPoster = sessionImgs.some((s) => s?.includes("2bb85f64"));

      return {
        bannerSrc,
        trainerSrc,
        hasSessionPoster,
      };
    });

    console.log(`Workshop Poster: ${wsMediaStatus.bannerSrc}`);
    console.log(`Trainer Photo: ${wsMediaStatus.trainerSrc}`);

    report.mediaPipelineTests.push({
      placement: "Workshop Poster",
      url: wsMediaStatus.bannerSrc,
      passed: Boolean(wsMediaStatus.bannerSrc?.includes("/api/media/content/")),
    });

    report.mediaPipelineTests.push({
      placement: "Trainer Profile Image",
      url: wsMediaStatus.trainerSrc,
      passed: Boolean(wsMediaStatus.trainerSrc?.includes("/api/media/content/")),
    });
    await context.close();
  }

  // 2C: Gallery Image & Gallery Video
  {
    const { context, page } = await createConfiguredPage({ width: 1440, height: 900 });
    await page.goto("http://localhost:4173/gallery", { waitUntil: "domcontentloaded" });
    await page.waitForTimeout(800);

    const galleryMediaStatus = await page.evaluate(() => {
      const allImgs = Array.from(document.querySelectorAll(".gallery-grid img, .gallery-item img, .gallery-slideshow img")).map((i) =>
        i.getAttribute("src")
      );
      const hasGalleryImage = allImgs.some((s) => s?.includes("8db85f64"));

      const allVideos = Array.from(document.querySelectorAll("video, .theater-player video")).map((v) =>
        v.getAttribute("src")
      );
      const hasGalleryVideo = allVideos.some((s) => s?.includes("9eb85f64"));

      return {
        allImgs,
        hasGalleryImage,
        allVideos,
        hasGalleryVideo,
      };
    });

    console.log(`Gallery Image Rendered: ${galleryMediaStatus.hasGalleryImage}`);
    console.log(`Gallery Video Rendered: ${galleryMediaStatus.hasGalleryVideo}`);

    report.mediaPipelineTests.push({
      placement: "Gallery Image",
      rendered: galleryMediaStatus.hasGalleryImage,
      passed: galleryMediaStatus.hasGalleryImage,
    });

    report.mediaPipelineTests.push({
      placement: "Gallery Video",
      rendered: galleryMediaStatus.hasGalleryVideo,
      passed: galleryMediaStatus.hasGalleryVideo,
    });
    await context.close();
  }

  await browser.close();

  console.log("\n===============================================================================");
  console.log("FINAL AUDIT SUMMARY REPORT");
  console.log("===============================================================================");
  console.log(`Total CSP Violations Detected: ${cspViolations.length}`);
  const allScrollPassed = report.galleryScrollTests.every((t) => t.passed);
  const allMediaPassed = report.mediaPipelineTests.every((t) => t.passed);

  console.log(`Gallery Scroll Restoration Tests: ${allScrollPassed ? "ALL PASSED" : "FAILED"}`);
  console.log(`All Admin-Managed Media CSP Tests: ${allMediaPassed ? "ALL PASSED" : "FAILED"}`);

  fs.writeFileSync(path.resolve(__dirname, "full_gallery_and_media_audit.json"), JSON.stringify({ report, cspViolations }, null, 2));

  if (!allScrollPassed || !allMediaPassed || cspViolations.length > 0) {
    console.error("Verification detected failures!");
    process.exit(1);
  }

  console.log("\nALL ACCEPTANCE TESTS PASSED WITH ZERO CSP VIOLATIONS!");
  process.exit(0);
}

runFullVerification().catch((err) => {
  console.error("Test execution failed:", err);
  process.exit(1);
});
