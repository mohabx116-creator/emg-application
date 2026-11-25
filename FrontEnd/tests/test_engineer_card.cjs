const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 360, height: 800 } });
  await page.goto('http://localhost:3005/construction/executive');
  await page.waitForTimeout(1000);

  // Switch to Engineer persona via data-testid
  const switcher = page.locator('[data-testid="persona-switcher-button"]');
  await switcher.click();
  await page.waitForTimeout(400);

  // Click persona-option-USR-ENG-A
  const engineerOpt = page.locator('[data-testid="persona-option-USR-ENG-A"]');
  await engineerOpt.click();
  await page.waitForTimeout(1200);

  console.log('Current URL after persona switch:', page.url());

  const cardDetails = await page.evaluate(() => {
    const chip = document.querySelector('[data-testid="attendance-chip"]');
    const headerCard = chip ? chip.closest('.MuiPaper-root') : null;
    const nameEl = headerCard ? headerCard.querySelector('.MuiTypography-subtitle1') : null;
    const titleEl = headerCard ? headerCard.querySelector('.MuiTypography-caption') : null;

    return {
      name: nameEl ? nameEl.textContent.trim() : null,
      nameRect: nameEl ? { width: nameEl.getBoundingClientRect().width, height: nameEl.getBoundingClientRect().height } : null,
      title: titleEl ? titleEl.textContent.trim() : null,
      chipRect: chip ? { width: chip.getBoundingClientRect().width, height: chip.getBoundingClientRect().height } : null,
      cardWidth: headerCard ? headerCard.getBoundingClientRect().width : null
    };
  });

  console.log('Engineer Card Details at 360px:', JSON.stringify(cardDetails, null, 2));
  await browser.close();
})();
