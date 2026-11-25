const { chromium } = require('playwright');

const ROLES = [
  { id: 'USR-GM', role: 'general_manager', route: '/construction/executive', enTitle: 'Executive' },
  { id: 'USR-OM', role: 'operations_manager', route: '/construction/operations', enTitle: 'Operations' },
  { id: 'USR-PM', role: 'project_manager', route: '/construction/projects', enTitle: 'Project Manager' },
  { id: 'USR-ENG-A', role: 'site_engineer', route: '/construction/my-site', enTitle: 'Site' },
  { id: 'USR-ACC', role: 'cost_accountant', route: '/construction/accounting', enTitle: 'Audit' },
  { id: 'USR-ADMIN', role: 'system_admin', route: '/construction/admin', enTitle: 'Administration' }
];

async function switchPersona(page, personaId) {
  const switcher = page.locator('[data-testid="persona-switcher-button"]').first();
  await switcher.click();
  await page.waitForTimeout(400);

  const opt = page.locator(`[data-testid="persona-option-${personaId}"]`);
  await opt.click();
  await page.waitForTimeout(800);
}

async function setDarkMode(page, isDark) {
  const targetMode = isDark ? 'dark' : 'light';
  console.log(`Setting dark mode to ${targetMode}...`);
  const currentScheme = await page.evaluate(() => document.documentElement.getAttribute('data-color-scheme'));
  if (currentScheme === targetMode) {
    console.log(`Already ${targetMode}`);
    return;
  }

  const fab = page.locator('.MuiFab-root').first();
  await fab.click({ timeout: 5000 });
  await page.waitForTimeout(500);

  const modeBtn = page.locator(`[data-testid="theme-mode-${targetMode}-btn"]`);
  await modeBtn.click({ timeout: 5000 });
  await page.waitForTimeout(500);

  const closeBtn = page.locator('[data-testid="customization-close-btn"]');
  await closeBtn.click({ timeout: 5000 });
  await page.waitForTimeout(500);
  console.log(`Finished setting ${targetMode}`);
}

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
  await page.goto('http://localhost:3005/construction/executive');
  await page.waitForTimeout(1000);

  await setDarkMode(page, true);

  for (const r of ROLES) {
    console.log(`Testing role ${r.id}...`);
    await switchPersona(page, r.id);
    const text = await page.evaluate(() => document.body.innerText.slice(0, 100));
    console.log(`Role ${r.id} text: ${text.replace(/\n/g, ' ')}`);
  }

  await setDarkMode(page, false);
  console.log('All roles passed dark mode test!');
  await browser.close();
})();
