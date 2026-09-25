import { chromium } from "playwright";

const widths = [320, 360, 375, 390, 414, 430, 440];

async function measureHeroTypography() {
  const browser = await chromium.launch({ headless: true });

  for (const w of widths) {
    const page = await browser.newPage({ viewport: { width: w, height: 800 } });
    await page.goto("http://localhost:4173/", { waitUntil: "domcontentloaded" });
    await page.waitForTimeout(500);

    const measurements = await page.evaluate(() => {
      const container = document.querySelector(".ethos-hero__content");
      const title = document.querySelector(".ethos-hero__title");
      const line1 = document.querySelector(".ethos-hero__title-line:first-child");
      const line2 = document.querySelector(".ethos-hero__title-line--accent");

      const cRect = container ? container.getBoundingClientRect() : null;
      const tRect = title ? title.getBoundingClientRect() : null;
      const l1Rect = line1 ? line1.getBoundingClientRect() : null;
      const l2Rect = line2 ? line2.getBoundingClientRect() : null;

      const computedStyle = line1 ? window.getComputedStyle(line1) : null;

      return {
        viewportWidth: window.innerWidth,
        fontSize: computedStyle?.fontSize,
        containerWidth: cRect?.width,
        containerRight: cRect?.right,
        line1Width: l1Rect?.width,
        line1Right: l1Rect?.right,
        line2Width: l2Rect?.width,
        line2Right: l2Rect?.right,
        isLine1Clipped: l1Rect ? l1Rect.right > window.innerWidth : false,
        overflowAmount: l1Rect ? Math.max(0, l1Rect.right - window.innerWidth) : 0,
      };
    });

    console.log(`[${w}px] Font: ${measurements.fontSize} | Line1 Width: ${measurements.line1Width}px | Container: ${measurements.containerWidth}px | Clipped: ${measurements.isLine1Clipped} (Overflow: ${measurements.overflowAmount}px)`);
    await page.close();
  }

  await browser.close();
}

measureHeroTypography();
