import { chromium } from "playwright";
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
    status: "published",
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
    status: "published",
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
    status: "approved",
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
    status: "published",
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
    status: "published",
  },
];

async function captureCarouselVisuals() {
  const browser = await chromium.launch({ headless: true });

  // 1. Desktop 1440px
  const desktopCtx = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const desktopPage = await desktopCtx.newPage();
  await desktopPage.route("**/api/workshops*", (route) => {
    const url = route.request().url();
    if (url.includes("/ws-01") || url.includes("contemporary-masterclass")) {
      return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(mockWorkshops[0]) });
    }
    return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(mockWorkshops) });
  });

  await desktopPage.goto("http://localhost:4173/workshops/contemporary-masterclass", { waitUntil: "domcontentloaded" });
  const desktopSection = desktopPage.locator(".more-workshops-section");
  await desktopSection.scrollIntoViewIfNeeded();
  await desktopPage.waitForTimeout(500);
  await desktopSection.screenshot({ path: path.resolve(__dirname, "more_workshops_desktop_carousel.png") });
  await desktopCtx.close();

  // 2. Mobile 390px
  const mobileCtx = await browser.newContext({ viewport: { width: 390, height: 844 } });
  const mobilePage = await mobileCtx.newPage();
  await mobilePage.route("**/api/workshops*", (route) => {
    const url = route.request().url();
    if (url.includes("/ws-01") || url.includes("contemporary-masterclass")) {
      return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(mockWorkshops[0]) });
    }
    return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(mockWorkshops) });
  });

  await mobilePage.goto("http://localhost:4173/workshops/contemporary-masterclass", { waitUntil: "domcontentloaded" });
  const mobileSection = mobilePage.locator(".more-workshops-section");
  await mobileSection.scrollIntoViewIfNeeded();
  await mobilePage.waitForTimeout(500);
  await mobileSection.screenshot({ path: path.resolve(__dirname, "more_workshops_mobile_carousel.png") });
  await mobileCtx.close();

  await browser.close();
  console.log("Carousel element screenshots captured successfully!");
}

captureCarouselVisuals();
