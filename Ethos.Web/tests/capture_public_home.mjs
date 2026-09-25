import { chromium } from 'playwright';
import path from 'path';

async function capturePublicHome() {
  const artifactDir = path.resolve('C:/Users/yuvar/.gemini/antigravity/brain/a1b1c899-ff0c-4ed7-bf8f-fa9cce9626e9');

  let browser;
  try {
    browser = await chromium.launch({ headless: true, args: ['--no-sandbox'] });
  } catch {
    browser = await chromium.launch({ channel: 'msedge', headless: true });
  }

  const context = await browser.newContext({ viewport: { width: 1440, height: 960 } });
  const page = await context.newPage();

  console.log('Navigating to http://localhost:5173/...');
  await page.goto('http://localhost:5173/', { waitUntil: 'domcontentloaded', timeout: 15000 });
  await page.waitForTimeout(2500);

  const heroShot = path.join(artifactDir, 'public_homepage_hero_uploaded.png');
  await page.screenshot({ path: heroShot, fullPage: false });
  console.log('Saved Public Homepage Hero screenshot:', heroShot);

  await browser.close();
}

capturePublicHome().catch(err => {
  console.error('Error capturing public home:', err);
  process.exit(1);
});
