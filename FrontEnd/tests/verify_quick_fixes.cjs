const { chromium } = require('playwright');

function parseRgb(str) {
  if (!str) return [255, 255, 255];
  const match = str.match(/rgba?\((\d+),\s*(\d+),\s*(\d+)/);
  if (!match) return [255, 255, 255];
  return [parseInt(match[1], 10), parseInt(match[2], 10), parseInt(match[3], 10)];
}

function luminance([r, g, b]) {
  const a = [r, g, b].map(v => {
    v /= 255;
    return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
  });
  return a[0] * 0.2126 + a[1] * 0.7152 + a[2] * 0.0722;
}

function contrastRatio(rgb1, rgb2) {
  const lum1 = luminance(rgb1);
  const lum2 = luminance(rgb2);
  const brightest = Math.max(lum1, lum2);
  const darkest = Math.min(lum1, lum2);
  return (brightest + 0.05) / (darkest + 0.05);
}

(async () => {
  const browser = await chromium.launch({ headless: true });
  console.log('--- Testing Quick Fixes ---');

  // Test 1: Mobile English switching
  {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    await page.goto('http://localhost:3005/construction/engineer');
    await page.waitForTimeout(1000);

    const initialDir = await page.evaluate(() => document.documentElement.dir);
    console.log('Initial mobile dir:', initialDir);

    const locBtn = page.locator('[data-testid="localization-button"]').first();
    const isLocBtnVisible = await locBtn.isVisible();
    console.log('Localization button visible on 390px mobile:', isLocBtnVisible);

    if (isLocBtnVisible) {
      await locBtn.click();
      await page.waitForTimeout(300);
      const enOption = page.locator('text=English').or(page.locator('[data-value="en"]')).or(page.locator('text=الإنجليزية')).first();
      await enOption.click();
      await page.waitForTimeout(1000);

      const afterDir = await page.evaluate(() => document.documentElement.dir);
      const docLang = await page.evaluate(() => document.documentElement.lang);
      const pageTitle = await page.evaluate(() => {
        const h = document.querySelector('h1, h2, h3, h4, h5, h6, .MuiTypography-subtitle1');
        return h ? h.textContent.trim() : null;
      });
      console.log('After switching on mobile:', { afterDir, docLang, sampleTitle: pageTitle });
    }

    // Test 360px engineer identity card
    await page.setViewportSize({ width: 360, height: 800 });
    await page.waitForTimeout(500);

    const cardBox = await page.evaluate(() => {
      const nameEl = document.querySelector('.MuiTypography-subtitle1');
      const chipEl = document.querySelector('[data-testid="attendance-chip"]');
      const headerCard = nameEl ? nameEl.closest('.MuiPaper-root') : null;
      return {
        nameText: nameEl ? nameEl.textContent.trim() : null,
        nameWidth: nameEl ? nameEl.getBoundingClientRect().width : null,
        chipWidth: chipEl ? chipEl.getBoundingClientRect().width : null,
        cardWidth: headerCard ? headerCard.getBoundingClientRect().width : null
      };
    });
    console.log('Engineer Identity Card at 360px:', cardBox);
    await page.close();
  }

  // Test 2: Dark mode contrast on Executive, Accounting, Admin
  {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await page.goto('http://localhost:3005/construction/executive');
    await page.waitForTimeout(1000);

    // Toggle Dark Mode
    const fab = page.locator('.MuiFab-root').first();
    await fab.click();
    await page.waitForTimeout(300);
    await page.evaluate(() => {
      const r = document.querySelector('input[value="dark"]');
      if (r) r.click();
    });
    await page.waitForTimeout(1000);

    const evalResult = await page.evaluate(() => {
      // Find cards and measure text vs bg
      const cards = Array.from(document.querySelectorAll('.MuiPaper-root'));
      return cards.slice(0, 8).map((c, i) => {
        const bg = window.getComputedStyle(c).backgroundColor;
        const text = c.querySelector('h1, h2, h3, h4, .MuiTypography-root');
        const color = text ? window.getComputedStyle(text).color : null;
        return { index: i, bg, color, textSnippet: text ? text.textContent.trim().slice(0, 30) : null };
      });
    });

    console.log('\n--- Dark Mode Executive Cards Contrast ---');
    for (const item of evalResult) {
      if (item.bg && item.color) {
        const rgbBg = parseRgb(item.bg);
        const rgbText = parseRgb(item.color);
        const ratio = contrastRatio(rgbBg, rgbText);
        console.log(`Card ${item.index}: ratio=${ratio.toFixed(2)}:1 (bg=${item.bg}, text=${item.color}) - "${item.textSnippet}"`);
      }
    }

    // Now navigate to accounting in dark mode
    await page.goto('http://localhost:3005/construction/accounting');
    await page.waitForTimeout(1000);

    const accContrast = await page.evaluate(() => {
      const header = document.querySelector('.MuiTypography-h2');
      const headerBox = header ? header.closest('.MuiPaper-root') : null;
      const bg = headerBox ? window.getComputedStyle(headerBox).backgroundColor : null;
      const color = header ? window.getComputedStyle(header).color : null;
      return { bg, color, title: header ? header.textContent.trim() : null };
    });
    console.log('\n--- Dark Mode Accounting Header Contrast ---');
    if (accContrast.bg && accContrast.color) {
      const ratio = contrastRatio(parseRgb(accContrast.bg), parseRgb(accContrast.color));
      console.log(`Accounting Header: ratio=${ratio.toFixed(2)}:1 (bg=${accContrast.bg}, text=${accContrast.color}) - "${accContrast.title}"`);
    }

    await page.close();
  }

  await browser.close();
  console.log('--- Quick Verification Completed ---');
})();
