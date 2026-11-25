const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 360, height: 800 } });
  await page.goto('http://localhost:3005/construction/executive');
  await page.waitForTimeout(1000);

  const btn = page.locator('[data-testid="persona-switcher-button"]');
  await btn.click();
  await page.waitForTimeout(500);

  const info = await page.evaluate(() => {
    const popper = document.querySelector('.MuiPopper-root');
    if (!popper) return { found: false };
    const items = Array.from(popper.querySelectorAll('[data-testid], button, [role="button"], li')).map(el => ({
      tag: el.tagName,
      testid: el.getAttribute('data-testid'),
      text: el.innerText ? el.innerText.trim().slice(0, 50) : null
    }));
    return {
      found: true,
      innerText: popper.innerText,
      items
    };
  });

  console.log('Popper info:', JSON.stringify(info, null, 2));
  await browser.close();
})();
