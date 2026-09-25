import { chromium } from "playwright";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const mockWorkshops = [
  {
    id: "ws-01",
    title: "Contemporary Masterclass",
    danceStyle: "CONTEMPORARY",
    workshopDate: "2026-10-15T18:00:00Z",
    startTime: "18:00:00",
    endTime: "20:00:00",
    venue: "Ethos Main Studio",
    area: "Jubilee Hills",
    city: "Hyderabad",
    startingPrice: 699,
    price: 699,
    status: "published",
    capacity: 40,
    imageUrl: "/assets/sample1.jpg",
  },
  {
    id: "ws-02",
    title: "Hip Hop Intensive",
    danceStyle: "HIP HOP",
    workshopDate: "2026-10-22T17:00:00Z",
    startTime: "17:00:00",
    endTime: "19:00:00",
    venue: "Ethos Studio 2",
    area: "Madhapur",
    city: "Hyderabad",
    startingPrice: 599,
    price: 599,
    status: "published",
    capacity: 35,
    imageUrl: "/assets/sample2.jpg",
  },
  {
    id: "ws-03",
    title: "Jazz Funk Workshop",
    danceStyle: "JAZZ FUNK",
    workshopDate: "2026-10-29T18:30:00Z",
    startTime: "18:30:00",
    endTime: "20:30:00",
    venue: "Ethos Main Studio",
    area: "Jubilee Hills",
    city: "Hyderabad",
    startingPrice: 649,
    price: 649,
    status: "approved",
    capacity: 30,
    imageUrl: "/assets/sample3.jpg",
  },
  {
    id: "ws-04",
    title: "Salsa Social Foundation",
    danceStyle: "SALSA",
    workshopDate: "2026-11-05T19:00:00Z",
    startTime: "19:00:00",
    endTime: "21:00:00",
    venue: "Ethos Main Studio",
    area: "Jubilee Hills",
    city: "Hyderabad",
    startingPrice: 799,
    price: 799,
    status: "published",
    capacity: 25,
    imageUrl: "/assets/sample4.jpg",
  },
  {
    id: "ws-05",
    title: "Heels Technique & Flow",
    danceStyle: "HEELS",
    workshopDate: "2026-11-12T18:00:00Z",
    startTime: "18:00:00",
    endTime: "20:00:00",
    venue: "Ethos Studio 2",
    area: "Madhapur",
    city: "Hyderabad",
    startingPrice: 749,
    price: 749,
    status: "published",
    capacity: 30,
    imageUrl: "/assets/sample5.jpg",
  },
];

const viewports = [
  { width: 320, height: 700, name: "320px" },
  { width: 360, height: 740, name: "360px" },
  { width: 375, height: 812, name: "375px" },
  { width: 390, height: 844, name: "390px" },
  { width: 414, height: 896, name: "414px" },
  { width: 430, height: 932, name: "430px" },
  { width: 1280, height: 800, name: "1280px" },
  { width: 1440, height: 900, name: "1440px" },
];

async function runVerification() {
  console.log("Launching browser for comprehensive More Workshops verification...");
  const browser = await chromium.launch({ headless: true });
  const results = [];

  for (const vp of viewports) {
    const context = await browser.newContext({
      viewport: { width: vp.width, height: vp.height },
      deviceScaleFactor: 1,
    });
    const page = await context.newPage();

    // Route intercept for API mock
    await page.route("**/api/workshops*", async (route) => {
      const url = route.request().url();
      if (url.includes("/pricing")) {
        return route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify({
            currentPrice: 699,
            currentTier: 1,
            currentTierName: "Early Bird",
            ticketsSold: 12,
            tierCapacity: 20,
            ticketsRemainingInTier: 8,
            progressPercentage: 60,
          }),
        });
      }
      if (url.includes("/ws-01") || url.includes("contemporary-masterclass")) {
        return route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify(mockWorkshops[0]),
        });
      }
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(mockWorkshops),
      });
    });

    // Navigate to the workshop detail page directly
    await page.goto("http://localhost:4173/workshops/contemporary-masterclass", {
      waitUntil: "domcontentloaded",
      timeout: 10000,
    });

    // Wait for the detail view and more-workshops to appear
    await page.waitForSelector(".more-workshops-section", { timeout: 8000 });

    // 1. Verify Page-level Horizontal Overflow
    const overflowMetrics = await page.evaluate(() => {
      const docW = document.documentElement.scrollWidth;
      const winW = window.innerWidth;
      const bodyW = document.body.scrollWidth;
      return {
        docScrollWidth: docW,
        winInnerWidth: winW,
        bodyScrollWidth: bodyW,
        hasPageOverflow: docW > winW || bodyW > winW,
      };
    });

    // 2. Verify Carousel Elements & Exclusion Invariant
    const carouselData = await page.evaluate(() => {
      const heading = document.querySelector(".more-workshops-title")?.textContent?.trim();
      const sub = document.querySelector(".more-workshops-sub")?.textContent?.trim();
      const controls = document.querySelector(".more-carousel-controls");
      const controlsVisible = controls ? window.getComputedStyle(controls).display !== "none" : false;
      const track = document.querySelector(".more-workshops-track");
      const cards = Array.from(document.querySelectorAll(".more-workshop-card"));

      const titles = cards.map((c) => c.querySelector(".more-workshop-name")?.textContent?.trim());
      const hasCurrentWorkshop = titles.some(
        (t) => t?.toLowerCase().includes("contemporary masterclass")
      );

      const cardRects = cards.map((c) => {
        const r = c.getBoundingClientRect();
        return { width: Math.round(r.width), height: Math.round(r.height) };
      });

      // Test scroll buttons if controls visible
      let scrolledAmount = 0;
      if (controlsVisible && track) {
        const initialScroll = track.scrollLeft;
        const nextBtn = document.querySelector(".more-carousel-btn.next");
        if (nextBtn) {
          nextBtn.click();
        }
        scrolledAmount = track.scrollLeft - initialScroll;
      }

      return {
        heading,
        sub,
        controlsVisible,
        cardCount: cards.length,
        titles,
        hasCurrentWorkshop,
        cardRects,
        scrolledAmount,
      };
    });

    // 3. Take screenshot artifact
    const screenshotPath = path.resolve(__dirname, `more_workshops_view_${vp.name}.png`);
    await page.screenshot({ path: screenshotPath, fullPage: false });

    // 4. Test clicking on a card navigates to new workshop
    let clickTestResult = null;
    if (vp.width >= 1280) {
      // On desktop, scroll down, click the first related card
      const firstCard = page.locator(".more-workshop-card").first();
      await firstCard.scrollIntoViewIfNeeded();
      await firstCard.click();
      await page.waitForTimeout(600);

      clickTestResult = await page.evaluate(() => {
        return {
          currentUrl: window.location.href,
          scrollY: window.scrollY,
        };
      });
    }

    results.push({
      viewport: vp.name,
      overflow: overflowMetrics,
      carousel: carouselData,
      clickTest: clickTestResult,
      screenshot: screenshotPath,
    });

    console.log(
      `[${vp.name}] Overflow: ${overflowMetrics.hasPageOverflow ? "FAIL" : "PASS"} (${overflowMetrics.docScrollWidth}px / ${overflowMetrics.winInnerWidth}px) | Cards: ${carouselData.cardCount} | Current Excluded: ${!carouselData.hasCurrentWorkshop ? "PASS" : "FAIL"} | Controls: ${carouselData.controlsVisible ? "Desktop (Visible)" : "Mobile (Touch/Swipe)"}`
    );

    await context.close();
  }

  await browser.close();
  return results;
}

runVerification()
  .then((res) => {
    fs.writeFileSync(path.resolve(__dirname, "more_workshops_full_audit.json"), JSON.stringify(res, null, 2));
    console.log("Full audit complete! Results saved.");
    process.exit(0);
  })
  .catch((err) => {
    console.error("Full audit failed:", err);
    process.exit(1);
  });
