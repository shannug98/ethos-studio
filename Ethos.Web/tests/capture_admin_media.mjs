import { chromium } from 'playwright';
import path from 'path';

async function captureScreenshots() {
  const artifactDir = path.resolve('C:/Users/yuvar/.gemini/antigravity/brain/a1b1c899-ff0c-4ed7-bf8f-fa9cce9626e9');

  let browser;
  try {
    browser = await chromium.launch({
      headless: true,
      args: ['--no-sandbox', '--disable-setuid-sandbox']
    });
  } catch (err) {
    browser = await chromium.launch({ channel: 'msedge', headless: true });
  }

  const context = await browser.newContext({
    viewport: { width: 1440, height: 960 }
  });

  const page = await context.newPage();

  // Set authenticated admin session in localStorage
  await page.addInitScript(() => {
    localStorage.setItem('ethos_admin_token', 'mock-admin-token');
    localStorage.setItem('ethos_admin_user', JSON.stringify({
      id: 'admin-1',
      name: 'Studio Director',
      email: 'admin@ethosdancestudio.com',
      roles: ['ADMIN']
    }));
  });

  // Real items uploaded by user in Neon DB
  const sampleMediaItems = [
    {
      id: "9ef3622d-316e-4201-bb5c-6f5392fdda0c",
      title: "BOMMALI",
      caption: "",
      mediaType: "Video",
      fileSizeBytes: 90074704,
      layoutType: "Landscape",
      publicUrl: "https://media.ethosdancestudio.com/homepagescrolling/videos/2026/09/debf5e435d8048919e89c1fde5be81b5.mp4",
      thumbnailUrl: "https://media.ethosdancestudio.com/homepagescrolling/videos/2026/09/debf5e435d8048919e89c1fde5be81b5.mp4",
      placements: [
        { id: "p-hero-1", section: "HomepageScrolling", displayOrder: 1, isPublished: true }
      ]
    },
    {
      id: "6e451328-e83d-4738-9ca7-bd39eeb24dfa",
      title: "WhatsApp Image 2026 09 11 at 11.39.42 PM.jpg",
      caption: "",
      mediaType: "Image",
      fileSizeBytes: 13856,
      layoutType: "Landscape",
      publicUrl: "https://media.ethosdancestudio.com/homepagescrolling/images/2026/09/284ba62bea664d438de4dec6136219d4.jpeg",
      thumbnailUrl: "https://media.ethosdancestudio.com/homepagescrolling/images/2026/09/284ba62bea664d438de4dec6136219d4.jpeg",
      placements: [
        { id: "p-hero-2", section: "HomepageScrolling", displayOrder: 2, isPublished: true }
      ]
    }
  ];

  // Intercept admin API calls
  await page.route(/.*\/api\/admin\/.*/, async (route) => {
    const url = route.request().url();
    if (url.includes('/api/admin/media')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: sampleMediaItems,
          totalCount: sampleMediaItems.length,
          page: 1,
          pageSize: 200
        })
      });
    }
    if (url.includes('/api/admin/sessions')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([{ id: 'sess-1', isActive: true }])
      });
    }
    if (url.includes('/api/admin/dashboard')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ attention: [], health: { status: 'healthy' } })
      });
    }
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ success: true })
    });
  });

  // Forward media content to live backend, or fallback to SVG if offline
  await page.route(/.*\/api\/media\/content\/(.*)/, async (route) => {
    try {
      return await route.continue();
    } catch {
      const svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 600 400" width="600" height="400"><rect width="100%" height="100%" fill="#13121a"/><circle cx="300" cy="200" r="60" fill="#e0a96d" opacity="0.4"/><text x="50%" y="70%" text-anchor="middle" fill="#94a3b8" font-family="sans-serif" font-size="16" font-weight="600">ETHOS MEDIA</text></svg>`;
      return route.fulfill({
        status: 200,
        contentType: 'image/svg+xml',
        body: svgContent
      });
    }
  });

  console.log('Navigating to http://localhost:5173/admin_portal/videos...');
  await page.goto('http://localhost:5173/admin_portal/videos', { waitUntil: 'networkidle', timeout: 15000 });
  await page.waitForTimeout(1000);

  // 1. Capture landing dashboard screenshot
  const dashboardShot = path.join(artifactDir, 'admin_media_library_rebuilt_dashboard.png');
  await page.screenshot({ path: dashboardShot, fullPage: false });
  console.log('Saved Dashboard screenshot:', dashboardShot);

  // 2. Scroll content independently to prove sidebar stays fixed at 100vh
  await page.evaluate(() => {
    const content = document.querySelector('.admin-app-content');
    if (content) content.scrollTop = 600;
  });
  await page.waitForTimeout(600);
  const scrolledShot = path.join(artifactDir, 'admin_media_library_rebuilt_scrolled.png');
  await page.screenshot({ path: scrolledShot, fullPage: false });
  console.log('Saved Scrolled screenshot:', scrolledShot);

  // Scroll back to top
  await page.evaluate(() => {
    const content = document.querySelector('.admin-app-content');
    if (content) content.scrollTop = 0;
  });
  await page.waitForTimeout(400);

  // 3. Click Manage on Hero Banner to open drawer
  const manageHeroBtn = page.locator('button.btn-manage-placement').first();
  if (await manageHeroBtn.count() > 0) {
    await manageHeroBtn.click();
    await page.waitForTimeout(600);
    const drawerShot = path.join(artifactDir, 'admin_media_library_manage_drawer.png');
    await page.screenshot({ path: drawerShot, fullPage: false });
    console.log('Saved Manage Drawer screenshot:', drawerShot);

    // Close drawer
    const backBtn = page.locator('.btn-back-library');
    if (await backBtn.count() > 0) await backBtn.click();
    await page.waitForTimeout(400);
  }

  // 4. Capture Trainers tab
  const trainersTab = page.locator('button.section-nav-tab:has-text("Trainers")');
  if (await trainersTab.count() > 0) {
    await trainersTab.click();
    await page.waitForTimeout(600);
    const trainersTabShot = path.join(artifactDir, 'admin_media_library_trainers_tab.png');
    await page.screenshot({ path: trainersTabShot, fullPage: false });
    console.log('Saved Trainers Tab screenshot:', trainersTabShot);

    // Open Manage on Trainers card
    const manageTrainersBtn = page.locator('button.btn-manage-placement');
    if (await manageTrainersBtn.count() > 0) {
      await manageTrainersBtn.first().click();
      await page.waitForTimeout(600);
      const trainersDrawerShot = path.join(artifactDir, 'admin_media_library_trainers_drawer.png');
      await page.screenshot({ path: trainersDrawerShot, fullPage: false });
      console.log('Saved Trainers Drawer screenshot:', trainersDrawerShot);

      // Close drawer
      const backBtn = page.locator('.btn-back-library');
      if (await backBtn.count() > 0) await backBtn.click();
      await page.waitForTimeout(400);
    }

    // Switch back to All tab
    const allTab = page.locator('button.section-nav-tab:has-text("All Sections")');
    if (await allTab.count() > 0) await allTab.click();
    await page.waitForTimeout(400);
  }

  // 5. Capture fullpage
  const fullPageShot = path.join(artifactDir, 'admin_media_library_fullpage.png');
  await page.screenshot({ path: fullPageShot, fullPage: true });
  console.log('Saved FullPage screenshot:', fullPageShot);

  await browser.close();
  console.log('All screenshots captured successfully!');
}

captureScreenshots().catch(err => {
  console.error('Screenshot capture failed:', err);
  process.exit(1);
});
