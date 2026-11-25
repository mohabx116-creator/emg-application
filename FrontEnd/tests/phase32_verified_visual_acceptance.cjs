/**
 * Phase 3.2: Verified Visual Defect Correction & Acceptance Suite
 * Real assertions testing actual UI state, DOM direction, contrast ratios, and responsive layout.
 */
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

const ROLES = [
  { id: 'USR-GM', role: 'general_manager', route: '/construction/executive', enTitle: 'Executive', arTitle: 'القيادة التنفيذية' },
  { id: 'USR-OM', role: 'operations_manager', route: '/construction/operations', enTitle: 'Operations', arTitle: 'العمليات' },
  { id: 'USR-PM', role: 'project_manager', route: '/construction/my-projects', enTitle: 'Project Manager', arTitle: 'إدارة ومتابعة المشروعات' },
  { id: 'USR-ENG-A', role: 'site_engineer', route: '/construction/my-site', enTitle: 'Site', arTitle: 'مهندس موقع' },
  { id: 'USR-ACC', role: 'cost_accountant', route: '/construction/accounting', enTitle: 'Audit', arTitle: 'المراجعة والتدقيق المالي' },
  { id: 'USR-ADMIN', role: 'system_admin', route: '/construction/administration', enTitle: 'Administration', arTitle: 'إدارة المنظومة' }
];

async function switchLanguage(page, lang) {
  const locBtn = page.locator('[data-testid="localization-button"]').first();
  await locBtn.waitFor({ state: 'visible', timeout: 5000 });
  await locBtn.click();
  await page.waitForTimeout(300);

  const targetOption = page.locator(`[data-testid="lang-${lang}"]`);
  await targetOption.waitFor({ state: 'visible', timeout: 5000 });
  await targetOption.click();
  await page.waitForTimeout(600);
}

async function switchPersona(page, personaId) {
  const switcher = page.locator('[data-testid="persona-switcher-button"]').first();
  await switcher.waitFor({ state: 'visible', timeout: 5000 });
  await switcher.click();
  await page.waitForTimeout(400);

  const opt = page.locator(`[data-testid="persona-option-${personaId}"]`);
  await opt.waitFor({ state: 'visible', timeout: 5000 });
  await opt.click();
  await page.waitForTimeout(800);
}

async function setDarkMode(page, isDark) {
  const targetMode = isDark ? 'dark' : 'light';
  const currentScheme = await page.evaluate(() => document.documentElement.getAttribute('data-color-scheme'));
  if (currentScheme === targetMode) return;

  const fab = page.locator('.MuiFab-root').first();
  await fab.click({ timeout: 5000 });
  await page.waitForTimeout(500);

  const modeBtn = page.locator(`[data-testid="theme-mode-${targetMode}-btn"]`);
  await modeBtn.click({ timeout: 5000 });
  await page.waitForTimeout(500);

  const closeBtn = page.locator('[data-testid="customization-close-btn"]');
  await closeBtn.click({ timeout: 5000 });
  await page.waitForTimeout(500);
}

(async () => {
  console.log('===============================================================');
  console.log('   PHASE 3.2 VERIFIED VISUAL DEFECT CORRECTION ACCEPTANCE');
  console.log('===============================================================\n');

  const browser = await chromium.launch({ headless: true });
  let totalTests = 0;
  let passedTests = 0;
  let failedTests = 0;

  function assert(condition, message) {
    totalTests++;
    if (condition) {
      passedTests++;
      console.log(`  [PASS] ${message}`);
    } else {
      failedTests++;
      console.error(`  [FAIL] ${message}`);
    }
  }

  const pageErrors = [];

  try {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    page.on('pageerror', err => pageErrors.push(err.message));

    // Initial navigation
    await page.goto('http://localhost:3005/construction/executive');
    await page.waitForTimeout(1000);

    // -------------------------------------------------------------------------
    console.log('Assertion 1 & 2: English mobile switching & LTR direction / visible translation');
    // -------------------------------------------------------------------------
    await switchLanguage(page, 'en');
    const mobileDir = await page.evaluate(() => document.documentElement.dir);
    const mobileLang = await page.evaluate(() => document.documentElement.lang);
    assert(mobileDir === 'ltr', `Mobile HTML dir is strictly 'ltr' (observed: '${mobileDir}')`);
    assert(mobileLang === 'en', `Mobile HTML lang is 'en' (observed: '${mobileLang}')`);

    const headerTextEn = await page.evaluate(() => {
      const h = document.querySelector('[data-testid="executive-dashboard-title"]');
      return h ? h.textContent.trim() : '';
    });
    assert(headerTextEn.includes('Executive') || headerTextEn.includes('General Manager'),
      `Mobile heading rendered in English: "${headerTextEn}"`);

    // -------------------------------------------------------------------------
    console.log('\nAssertion 3 & 4: English remains selectable across all roles & Arabic restoration');
    // -------------------------------------------------------------------------
    for (const r of ROLES) {
      await switchPersona(page, r.id);
      await page.goto('http://localhost:3005' + r.route);
      await page.waitForTimeout(500);

      const curDir = await page.evaluate(() => document.documentElement.dir);
      assert(curDir === 'ltr', `Role ${r.id} maintains LTR in English`);

      const content = await page.evaluate(() => document.body.innerText);
      assert(content.includes(r.enTitle) || content.includes(r.id), `Role ${r.id} renders English text content`);
    }

    // Restore Arabic
    await switchLanguage(page, 'ar');
    const restoredDir = await page.evaluate(() => document.documentElement.dir);
    assert(restoredDir === 'rtl', `Arabic restored with RTL direction (observed: '${restoredDir}')`);

    // Return to Executive dashboard
    await switchPersona(page, 'USR-GM');
    await page.goto('http://localhost:3005/construction/executive');
    await page.waitForTimeout(500);

    // -------------------------------------------------------------------------
    console.log('\nAssertion 5: Dark-mode contrast verification (WCAG AA >= 4.5:1 / 3:1)');
    // -------------------------------------------------------------------------
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.waitForTimeout(500);
    await setDarkMode(page, true);

    const contrastAudits = [];
    for (const r of ROLES) {
      await switchPersona(page, r.id);
      await page.goto('http://localhost:3005' + r.route);
      await page.waitForTimeout(500);

      const audit = await page.evaluate(() => {
        const cards = Array.from(document.querySelectorAll('.MuiPaper-root')).slice(0, 5);
        return cards.map(c => {
          const bg = window.getComputedStyle(c).backgroundColor;
          const textEl = c.querySelector('h1, h2, h3, h4, .MuiTypography-root');
          const color = textEl ? window.getComputedStyle(textEl).color : null;
          return { bg, color, text: textEl ? textEl.textContent.trim().slice(0, 35) : '' };
        });
      });
      contrastAudits.push({ role: r.id, cards: audit });
    }

    let minContrast = 999;
    for (const ca of contrastAudits) {
      for (const card of ca.cards) {
        if (card.bg && card.color && card.bg !== 'rgba(0, 0, 0, 0)') {
          const ratio = contrastRatio(parseRgb(card.bg), parseRgb(card.color));
          if (ratio < minContrast) minContrast = ratio;
        }
      }
    }
    assert(minContrast >= 4.5, `Dark mode measured minimum text contrast meets WCAG AA (minimum observed: ${minContrast.toFixed(2)}:1)`);

    // Restore light mode for remaining tests
    await setDarkMode(page, false);

    // -------------------------------------------------------------------------
    console.log('\nAssertion 6: Engineer mobile identity card (360px & 390px responsive flex)');
    // -------------------------------------------------------------------------
    await switchPersona(page, 'USR-ENG-A');
    await page.goto('http://localhost:3005/construction/my-site');
    await page.waitForTimeout(500);

    for (const width of [360, 390]) {
      await page.setViewportSize({ width, height: 800 });
      await page.waitForTimeout(400);

      const idCard = await page.evaluate(() => {
        const chip = document.querySelector('[data-testid="attendance-chip"]');
        const card = chip ? chip.closest('.MuiPaper-root') : null;
        const nameEl = card ? card.querySelector('.MuiTypography-subtitle1') : null;
        return {
          name: nameEl ? nameEl.textContent.trim() : null,
          nameHeight: nameEl ? nameEl.getBoundingClientRect().height : 999,
          nameWidth: nameEl ? nameEl.getBoundingClientRect().width : 0,
          cardWidth: card ? card.getBoundingClientRect().width : 0
        };
      });

      assert(idCard.nameHeight <= 35, `Engineer name at ${width}px is compact without awkward multi-line wrap (height: ${idCard.nameHeight.toFixed(1)}px)`);
      assert(idCard.nameWidth >= 120, `Engineer name container width is generous at ${width}px (${idCard.nameWidth.toFixed(1)}px / card ${idCard.cardWidth.toFixed(1)}px)`);
    }

    // -------------------------------------------------------------------------
    console.log('\nAssertion 7: Fixed elements do not obscure primary controls');
    // -------------------------------------------------------------------------
    const fabBox = await page.evaluate(() => {
      const fab = document.querySelector('.MuiFab-root');
      if (!fab) return null;
      const rect = fab.getBoundingClientRect();
      const header = document.querySelector('header, .MuiAppBar-root');
      const headerBottom = header ? header.getBoundingClientRect().bottom : 0;
      return {
        top: rect.top,
        bottom: rect.bottom,
        windowHeight: window.innerHeight,
        headerBottom,
        overlapsHeader: rect.top < headerBottom
      };
    });
    assert(fabBox && !fabBox.overlapsHeader, `Live Customize FAB does not overlap header (top: ${fabBox.top}px, header bottom: ${fabBox.headerBottom}px)`);
    assert(fabBox && fabBox.top > 400, `Live Customize FAB is positioned in the lower half of screen on mobile (top: ${fabBox.top}px)`);

    // -------------------------------------------------------------------------
    console.log('\nAssertion 8: Critical dialogs remain functional');
    // -------------------------------------------------------------------------
    const attendanceChip = page.locator('[data-testid="attendance-chip"]');
    await attendanceChip.click();
    await page.waitForTimeout(400);
    const dialogVisible = await page.locator('.MuiDialog-root').isVisible();
    assert(dialogVisible, 'Attendance Quick Dialog opens upon chip interaction');

    const dialogCloseBtn = page.locator('.MuiDialog-root button:has-text("إغلاق"), .MuiDialog-root button:has-text("Close")').first();
    await dialogCloseBtn.click();
    await page.waitForTimeout(400);

    // -------------------------------------------------------------------------
    console.log('\nAssertion 9: No page-level horizontal overflow across all widths');
    // -------------------------------------------------------------------------
    for (const w of [360, 390, 768, 1440]) {
      await page.setViewportSize({ width: w, height: 800 });
      await page.waitForTimeout(300);
      const overflow = await page.evaluate(() => {
        const docWidth = document.documentElement.scrollWidth;
        const winWidth = window.innerWidth;
        return { docWidth, winWidth, hasOverflow: docWidth > winWidth + 1 };
      });
      assert(!overflow.hasOverflow, `Zero horizontal page overflow at ${w}px (scrollWidth: ${overflow.docWidth}px, window: ${overflow.winWidth}px)`);
    }

    // -------------------------------------------------------------------------
    console.log('\nAssertion 10: Role security & unauthorized navigation');
    // -------------------------------------------------------------------------
    // While as engineer (USR-ENG-A), attempt to visit /construction/administration
    await page.goto('http://localhost:3005/construction/administration');
    await page.waitForTimeout(1000);
    const isAccessDeniedVisible = await page.locator('[data-testid="access-denied-view"]').isVisible();
    assert(isAccessDeniedVisible, 'Site Engineer blocked from unauthorized /construction/administration (Access Denied view rendered)');

    // -------------------------------------------------------------------------
    console.log('\nAssertion 11: Expense workflow operational');
    // -------------------------------------------------------------------------
    await page.goto('http://localhost:3005/construction/my-site');
    await page.waitForTimeout(800);
    const expenseBtn = page.locator('[data-testid="btn-add-expense"]').first();
    const hasExpenseAction = await expenseBtn.isVisible();
    assert(hasExpenseAction, 'Engineer has accessible expense recording workflow action');

    // -------------------------------------------------------------------------
    console.log('\nAssertion 12: Zero uncaught browser errors');
    // -------------------------------------------------------------------------
    assert(pageErrors.length === 0, `Zero uncaught browser runtime errors (errors logged: ${pageErrors.length})`);
    if (pageErrors.length > 0) {
      console.error('Page errors:', pageErrors);
    }

    // -------------------------------------------------------------------------
    console.log('\nAssertion 13: Persona switcher metadata integrity');
    // -------------------------------------------------------------------------
    await switchPersona(page, 'USR-GM');
    await page.goto('http://localhost:3005/construction/executive');
    await page.waitForTimeout(500);

    const finalRoleTitle = await page.evaluate(() => {
      const switcher = document.querySelector('[data-testid="persona-switcher-button"]');
      return switcher ? switcher.textContent.trim() : '';
    });
    assert(finalRoleTitle.includes('طارق المنياوي') || finalRoleTitle.includes('General Manager'),
      `Switcher reflects active persona ("${finalRoleTitle}")`);

    await page.close();
  } finally {
    await browser.close();
  }

  console.log('\n===============================================================');
  console.log(`   SUITE COMPLETE: ${passedTests}/${totalTests} Passed (${failedTests} Failed)`);
  console.log('===============================================================');

  if (failedTests > 0) {
    process.exit(1);
  } else {
    process.exit(0);
  }
})();
