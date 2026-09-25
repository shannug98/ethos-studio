import { chromium } from "playwright";

const widths = [320, 360, 375, 390, 414, 430, 440];

async function measureTextOverflow() {
  const browser = await chromium.launch({ headless: true });

  for (const w of widths) {
    const page = await browser.newPage({ viewport: { width: w, height: 800 } });
    await page.goto("http://localhost:4173/", { waitUntil: "domcontentloaded" });
    await page.waitForTimeout(500);

    const measurements = await page.evaluate(() => {
      const line1 = document.querySelector(".ethos-hero__title-line:first-child");
      const title = document.querySelector(".ethos-hero__title");
      const container = document.querySelector(".ethos-hero__content");

      // Measure range of text directly
      const range = document.createRange();
      range.selectNodeContents(line1);
      const textRect = range.getBoundingClientRect();

      return {
        fontSize: window.getComputedStyle(line1).fontSize,
        textExactWidth: Math.round(textRect.width),
        scrollWidth: line1.scrollWidth,
        clientWidth: line1.clientWidth,
        containerWidth: Math.round(container.getBoundingClientRect().width),
        viewportWidth: window.innerWidth,
        textOverflow: Math.round(textRect.width - container.getBoundingClientRect().width),
      };
    });

    console.log(`[${w}px] Font: ${measurements.fontSize} | Text Width: ${measurements.textExactWidth}px | Container: ${measurements.containerWidth}px | Overflow: ${measurements.textOverflow}px`);
    await page.close();
  }

  await browser.close();
}

measureTextOverflow();
