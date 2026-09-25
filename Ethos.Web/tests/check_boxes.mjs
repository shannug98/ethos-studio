import { chromium } from 'playwright';

async function check() {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1440, height: 960 } });
  const page = await context.newPage();
  await page.addInitScript(() => {
    localStorage.setItem('ethos_admin_token', 'mock');
    localStorage.setItem('ethos_admin_user', JSON.stringify({ roles: ['ADMIN'] }));
  });
  await page.route(/.*\/api\/admin\/.*/, async (route) => {
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ items: [] }) });
  });
  await page.goto('http://localhost:5173/admin_portal/videos', { waitUntil: 'networkidle' });
  const elements = await page.$$('.placement-section-container');
  console.log('Found placement-section-containers:', elements.length);
  for (let i = 0; i < elements.length; i++) {
    const text = await elements[i].$eval('.section-header-title', el => el.innerText).catch(() => 'no title');
    const box = await elements[i].boundingBox();
    console.log('Section', i, 'title:', text, 'box:', JSON.stringify(box));
  }

  const row = await page.$('.lower-sections-row');
  if (row) {
    const box = await row.boundingBox();
    console.log('lower-sections-row box:', JSON.stringify(box));
    const children = await row.$$(':scope > *');
    console.log('lower-sections-row children count:', children.length);
    for (let j = 0; j < children.length; j++) {
      const cBox = await children[j].boundingBox();
      const tag = await children[j].evaluate(el => el.tagName + '.' + el.className);
      console.log('Child', j, tag, JSON.stringify(cBox));
      const subChildren = await children[j].$$(':scope > *');
      for (let k = 0; k < subChildren.length; k++) {
        const sBox = await subChildren[k].boundingBox();
        const sClass = await subChildren[k].evaluate(el => el.className);
        console.log('  SubChild', k, sClass, JSON.stringify(sBox));
      }
    }
  }
  await browser.close();
}

check().catch(console.error);
