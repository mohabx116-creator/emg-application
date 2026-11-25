// Run only after proving that the canonical launcher uses disposable PostgreSQL.
// Supply the isolated px1-admin password. For its first login, also supply the next password.
const assert = require('node:assert/strict');
const { randomBytes } = require('node:crypto');
const { mkdirSync, writeFileSync } = require('node:fs');
const path = require('node:path');
const { setTimeout: delay } = require('node:timers/promises');
const { chromium } = require('playwright');

const origin = 'http://127.0.0.1:3100';
const projectListPath = '/emg/projects';
const projectsApi = '/api/projects';
const tag = randomBytes(4).toString('hex');
const password = () => `Px1!${randomBytes(20).toString('hex')}`;
const temporaryPassword = password();
const evidence = { scenarios: [], api: [], pageErrors: [] };
const evidencePath = process.env.EMG_PX1_EVIDENCE;
const adminPassword = process.env.EMG_PX1_ADMIN_PASSWORD;
assert(process.env.EMG_PX1_ISOLATED_QA === '1' && adminPassword && evidencePath, 'Verified QA runtime, temporary admin and evidence path required');

const requestPath = (request) => new URL(request.url()).pathname;
const onPath = (pathname) => (url) => url.pathname === pathname;
const projectPath = (id) => `${projectListPath}/${id}`;
const isReport = (pathname) => pathname.startsWith('/api/reports/projects/');
const observed = (name, data) => evidence.scenarios.push({ name, ...data });
const screenshot = (page, name) => page.screenshot({ path: path.join(evidencePath, `${name}.png`), animations: 'disabled' });

async function api(context, method, pathname, status, data) {
  const csrf = (await context.cookies()).find((cookie) => cookie.name === 'emg_csrf');
  const response = await context.request.fetch(origin + pathname, {
    method,
    data,
    headers: csrf && method !== 'GET' ? { 'X-CSRF-TOKEN': csrf.value } : {}
  });
  assert.equal(response.status(), status, `${method} ${pathname}`);
  evidence.api.push({ method, pathname, status });
  return status === 200 || status === 201 ? response.json() : null;
}

// Auth V2 intentionally allows five login calls per minute per client IP.
let loginCount = 0;
let loginWindowStarted = 0;
async function loginSlot() {
  if (loginCount === 5) {
    const cooldown = Math.max(0, 61_000 - (Date.now() - loginWindowStarted));
    if (cooldown > 0) console.log('Waiting for the isolated Auth V2 login window to renew.');
    await delay(cooldown);
    loginCount = 0;
  }
  if (loginCount === 0) loginWindowStarted = Date.now();
  loginCount++;
}

async function loginApi(context, email, secret) {
  await loginSlot();
  await api(context, 'POST', '/api/account/login', 200, { email, password: secret, rememberMe: true });
  await api(context, 'GET', '/api/account/csrf', 200);
}

async function rotatePassword(context, currentPassword, newPassword) {
  assert.equal((await api(context, 'GET', '/api/account/me', 200)).user.mustChangePassword, true);
  await api(context, 'POST', '/api/account/change-password', 200, { currentPassword, newPassword });
  await api(context, 'GET', '/api/account/me', 401);
}

async function browserPage(context, i18n = 'en') {
  await context.addInitScript((language) => {
    localStorage.setItem('berry-config-vite-ts', JSON.stringify({
      i18n: language,
      themeDirection: language === 'ar' ? 'rtl' : 'ltr',
      menuOrientation: 'vertical',
      miniDrawer: false,
      fontFamily: language === 'ar' ? "'Cairo', 'Roboto', sans-serif" : "'Roboto', sans-serif",
      borderRadius: 8,
      outlinedFilled: true,
      presetColor: 'default',
      container: true
    }));
  }, i18n);
  const page = await context.newPage();
  page.setDefaultTimeout(20_000);
  page.on('pageerror', (error) => evidence.pageErrors.push(error.message));
  return page;
}

async function rotateAndLogin(context, email, initialPassword) {
  const newPassword = password();
  await loginApi(context, email, initialPassword);
  await rotatePassword(context, initialPassword, newPassword);
  await loginApi(context, email, newPassword);
  const { user } = await api(context, 'GET', '/api/account/me', 200);
  assert.equal(user.mustChangePassword, false);
  return { user, newPassword };
}

function assertViewport(page, direction) {
  return page.evaluate(() => ({
    lang: document.documentElement.lang,
    dir: document.documentElement.dir,
    viewport: document.documentElement.clientWidth,
    content: document.documentElement.scrollWidth
  })).then((size) => {
    assert.equal(size.dir, direction);
    assert.equal(size.lang, direction === 'rtl' ? 'ar' : 'en');
    assert(size.content <= size.viewport + 1, `Horizontal page overflow: ${size.content} over ${size.viewport}`);
    return size;
  });
}

async function engineerJourney(browser, storageState, project, inaccessible) {
  for (const i18n of ['ar', 'en']) {
    for (const width of [1366, 390]) {
      const context = await browser.newContext({ baseURL: origin, storageState, viewport: { width, height: 900 } });
      try {
        const page = await browserPage(context, i18n);
        const requests = [];
        page.on('request', (request) => requests.push(requestPath(request)));
        await page.goto(projectListPath);
        const card = page.locator(`a[href="${projectPath(project.id)}"]`);
        await card.waitFor();
        assert.equal(await page.locator(`a[href="${projectPath(inaccessible.id)}"]`).count(), 0);
        assert.equal(await page.getByRole('button', { name: /Create project|إضافة مشروع/ }).count(), 0);
        await card.click();
        await page.getByRole('heading', { name: project.name, exact: true }).waitFor();
        const movementPath = `${projectPath(project.id)}/movements`;
        const movementLink = page.locator(`a[href="${movementPath}"]`);
        await movementLink.waitFor();
        assert.equal((await movementLink.textContent()).trim(), i18n === 'ar' ? 'حركة الموقع اليومية' : 'Site Movements');
        assert.equal(await page.getByRole('link', { name: /Final Account Report|كشف الحساب النهائي/ }).count(), 0);
        assert.equal(await page.getByRole('button', { name: /Export Excel|تصدير Excel/ }).count(), 0);
        const layout = await assertViewport(page, i18n === 'ar' ? 'rtl' : 'ltr');
        await screenshot(page, `engineer-${i18n}-${width}`);
        const [movementResponse] = await Promise.all([
          page.waitForResponse((response) => new URL(response.url()).pathname === '/api/site-movements'),
          page.waitForURL(onPath(movementPath)),
          movementLink.click()
        ]);
        assert.equal(movementResponse.status(), 200);
        await page.getByRole('heading', { level: 2, name: 'حركة الموقع اليومية', exact: true }).waitFor();
        await page.getByLabel('الوصف', { exact: false }).waitFor();
        const reportRequests = requests.filter(isReport);
        assert.deepEqual(reportRequests, []);
        observed('Engineer opens assigned project and Site Movements', { i18n, width, layout, siteMovementsStatus: 200, reportRequests: 0 });

        if (i18n === 'en' && width === 390) {
          const [response] = await Promise.all([
            page.waitForResponse((item) => new URL(item.url()).pathname === `${projectsApi}/${inaccessible.id}`),
            page.goto(projectPath(inaccessible.id))
          ]);
          assert.equal(response.status(), 404);
          await page.getByText('This project is unavailable to your account.', { exact: true }).waitFor();
          assert.equal(await page.getByRole('heading', { name: inaccessible.name, exact: true }).count(), 0);
          assert.deepEqual(requests.filter(isReport), []);
          observed('Engineer direct URL for unassigned project', { status: 404, reportRequests: 0 });
          await api(context, 'GET', `/api/reports/projects/${project.id}`, 403);
          await api(context, 'POST', '/api/account/logout', 204);
          await api(context, 'GET', '/api/account/me', 401);
        }
      } finally {
        await context.close();
      }
    }
  }
}

async function readerJourney(context, project, admin, userId) {
  const page = await browserPage(context);
  await page.goto(projectListPath);
  await page.locator(`a[href="${projectPath(project.id)}"]`).waitFor();
  assert.equal(await page.getByRole('button', { name: 'Create project' }).count(), 0);
  await page.goto(projectPath(project.id));
  await page.getByRole('link', { name: 'Site Movements' }).waitFor();
  for (const section of ['excavation', 'concrete', 'steel', 'finishing']) {
    assert.equal(await page.locator(`a[href="${projectPath(project.id)}/${section}"]`).count(), 0);
  }

  await api(admin, 'DELETE', `/api/users/${userId}/projects/${project.id}`, 204);
  await page.goto(projectListPath);
  const empty = page.getByText('No projects are available to your account yet', { exact: true });
  await empty.waitFor();
  assert.equal(await page.getByRole('button', { name: 'Create project' }).count(), 0);

  const failure = onPath(projectsApi);
  let release;
  const pending = new Promise((resolve) => { release = resolve; });
  await page.route(failure, async (route) => {
    await pending;
    await route.abort('failed');
  }, { times: 1 });
  try {
    await page.reload();
    await page.getByRole('status', { name: 'Loading projects' }).waitFor();
    assert.equal(await empty.count(), 0);
  } finally {
    release();
  }
  await page.getByText('Could not load projects.', { exact: true }).waitFor();
  assert.equal(await empty.count(), 0);
  await page.getByRole('button', { name: 'Try again' }).click();
  await empty.waitFor();
  await page.route(failure, (route) => route.fulfill({ status: 403, body: '' }), { times: 1 });
  await page.reload();
  await page.getByText('You do not have permission to view projects.', { exact: true }).waitFor();
  assert.equal(await empty.count(), 0);
  observed('Read-only user and list states', { createButtons: 0, loading: true, empty: true, networkErrorRetry: true, empty403Denied: true });
}

async function financeJourney(context, project) {
  const page = await browserPage(context);
  const heading = page.getByRole('heading', { name: project.name, exact: true });
  const reportPath = `/api/reports/projects/${project.id}`;
  const auditPath = `${reportPath}/audit`;
  await Promise.all([
    page.waitForResponse((r) => new URL(r.url()).pathname === reportPath && r.status() === 200),
    page.waitForResponse((r) => new URL(r.url()).pathname === auditPath && r.status() === 200),
    page.goto(projectPath(project.id))
  ]);
  await heading.waitFor();
  await page.getByRole('link', { name: 'Final Account Report' }).waitFor();
  await page.getByText('إجمالي الداخل', { exact: true }).waitFor();
  await page.getByText('توريدات الموردين', { exact: true }).waitFor();
  assert.equal(await page.getByRole('link', { name: 'Site Movements' }).count(), 0);
  await screenshot(page, 'accountant-reports');
  observed('Accountant summary and audit', { reportStatus: 200, auditStatus: 200 });

  for (const entry of [
    { endpoint: reportPath, loading: 'Loading financial report', error: 'Could not load the financial report.', sibling: 'توريدات الموردين' },
    { endpoint: auditPath, loading: 'Loading operational audit', error: 'Could not load the operational audit.', sibling: 'إجمالي الداخل' }
  ]) {
    let release;
    const pending = new Promise((resolve) => { release = resolve; });
    await page.route(onPath(entry.endpoint), async (route) => {
      await pending;
      await route.fulfill({ status: 503, contentType: 'application/json', body: '{ "message": "Simulated PX1 optional request failure" }' });
    }, { times: 1 });
    try {
      await page.reload();
      await heading.waitFor();
      await page.getByRole('progressbar', { name: entry.loading }).waitFor();
      await page.getByText(entry.sibling, { exact: true }).waitFor();
    } finally {
      release();
    }
    await page.getByText(entry.error, { exact: true }).waitFor();
    await heading.waitFor();
    await page.getByText(entry.sibling, { exact: true }).waitFor();
    observed('Optional panel failure is isolated', { endpoint: entry.endpoint, injectedStatus: 503, projectVisible: true, siblingVisible: true });
  }
}

async function createJourney(context, engineer, allowAssignment) {
  const page = await browserPage(context);
  const requests = [];
  page.on('request', (request) => requests.push({ route: requestPath(request), method: request.method() }));
  await page.goto(projectListPath);
  await page.getByRole('button', { name: 'Create project', exact: true }).first().click();
  const dialog = page.getByRole('dialog', { name: 'Create a project' });
  await dialog.getByLabel('Project name').fill(`PX1 Create ${tag} ${allowAssignment ? 'with assignment' : 'without assignment'}`);
  await dialog.getByLabel('Project code').fill(`PX1-${tag}-${allowAssignment ? 'A' : 'B'}`);
  if (allowAssignment) {
    await dialog.getByRole('combobox', { name: 'Assign project' }).click();
    await page.getByRole('option', { name: `${engineer.displayName} (${engineer.userName})`, exact: true }).click();
    await page.route((url) => url.pathname.startsWith(`/api/users/${engineer.id}/projects/`), (route) =>
      route.fulfill({ status: 503, body: '' }), { times: 1 });
  } else {
    assert.equal(await dialog.getByRole('combobox', { name: 'Assign project' }).count(), 0);
  }
  const [response] = await Promise.all([
    page.waitForResponse((item) => new URL(item.url()).pathname === projectsApi && item.request().method() === 'POST'),
    dialog.getByRole('button', { name: 'Create project', exact: true }).click()
  ]);
  assert.equal(response.status(), 201);
  await dialog.waitFor({ state: 'hidden' });
  await page.getByText(allowAssignment ? /Project created, but access could not be assigned/ : 'Project created without a user assignment.').waitFor();
  assert.equal(requests.filter(({ route, method }) => route === projectsApi && method === 'POST').length, 1);
  assert.equal(requests.filter(({ route }) => route === '/api/users').length, allowAssignment ? 1 : 0);
  assert.equal(requests.filter(({ route }) => route.startsWith(`/api/users/${engineer.id}/projects/`)).length, allowAssignment ? 1 : 0);
  observed('Creation reports assignment separately', { projectStatus: 201, assignmentAttempted: allowAssignment, simulatedAssignmentFailure: allowAssignment });
}

async function run() {
  mkdirSync(evidencePath, { recursive: true });
  const browser = await chromium.launch({ channel: 'chrome' });
  try {
    const admin = await browser.newContext({ baseURL: origin });
    await loginApi(admin, 'px1-admin@example.test', adminPassword);
    if ((await api(admin, 'GET', '/api/account/me', 200)).user.mustChangePassword) {
      const nextPassword = process.env.EMG_PX1_ADMIN_NEXT_PASSWORD;
      assert(nextPassword, 'Supply the next synthetic QA admin password for rotation');
      await rotatePassword(admin, adminPassword, nextPassword);
      await loginApi(admin, 'px1-admin@example.test', nextPassword);
    }
    const createProject = (label) => api(admin, 'POST', projectsApi, 201, { name: `PX1 QA ${label} ${tag}`, code: `QA-${label}-${tag}` });
    const createUser = (name, role) => api(admin, 'POST', '/api/users', 200, {
      userName: `px1-${name}-${tag}`, email: `px1-${name}-${tag}@example.test`,
      displayName: `PX1 QA ${name}`, password: temporaryPassword, role
    });
    const project = await createProject('Assigned');
    const inaccessible = await createProject('Unassigned');
    const engineer = await createUser('engineer', 'engineer');
    const accountant = await createUser('accountant', 'accountant');
    const manager = await createUser('manager', 'project_manager');
    const readerRole = `px1_reader_${tag}`;
    const reader = await createUser('reader', readerRole);
    const roles = await api(admin, 'GET', '/api/roles', 200);
    const permissions = await api(admin, 'GET', '/api/roles/permissions', 200);
    await api(admin, 'PUT', `/api/roles/${roles.find((role) => role.name === readerRole).id}/permissions`, 204, {
      permissionIds: permissions.filter((item) => ['projects.view', 'site-movements.view', 'attendance.view', 'notifications.view'].includes(item.code)).map((item) => item.id)
    });
    for (const user of [engineer, manager, reader]) {
      await api(admin, 'POST', `/api/users/${user.id}/projects/${project.id}`, 204);
    }

    const engineerContext = await browser.newContext({ baseURL: origin });
    await loginApi(engineerContext, engineer.email, temporaryPassword);
    await api(engineerContext, 'GET', `${projectsApi}/${project.id}`, 403);
    const engineerPassword = password();
    await rotatePassword(engineerContext, temporaryPassword, engineerPassword);
    const loginPage = await browserPage(engineerContext);
    await loginPage.goto('/login');
    await loginPage.locator('input[name="email"]').fill(engineer.email);
    await loginPage.locator('input[name="password"]').fill(engineerPassword);
    await loginSlot();
    await loginPage.locator('button[type="submit"]').click();
    await loginPage.waitForURL((url) => !url.pathname.startsWith('/login'));
    const { user: engineerProfile } = await api(engineerContext, 'GET', '/api/account/me', 200);
    assert.equal(engineerProfile.mustChangePassword, false);
    assert(engineerProfile.projectIds.includes(project.id));
    assert(!engineerProfile.permissions.includes('reports.view'));
    assert((await engineerContext.cookies()).some((item) => item.name === 'emg_session' && item.httpOnly));
    await engineerJourney(browser, await engineerContext.storageState(), project, inaccessible);
    console.log('PASS engineer entry, layout, server-side scope and Auth V2 session');
    await engineerContext.close();

    const readerContext = await browser.newContext({ baseURL: origin });
    await rotateAndLogin(readerContext, reader.email, temporaryPassword);
    await readerJourney(readerContext, project, admin, reader.id);
    await readerContext.close();
    console.log('PASS read-only actions, loading, empty, access denied, network failure and retry');

    const financeContext = await browser.newContext({ baseURL: origin });
    await rotateAndLogin(financeContext, accountant.email, temporaryPassword);
    await financeJourney(financeContext, project);
    await financeContext.close();
    console.log('PASS finance report, audit and independent optional failures');

    const managerContext = await browser.newContext({ baseURL: origin });
    const { user: managerProfile } = await rotateAndLogin(managerContext, manager.email, temporaryPassword);
    assert(managerProfile.permissions.includes('projects.create') && !managerProfile.permissions.includes('users.assign-project'));
    await createJourney(managerContext, engineer, false);
    await createJourney(admin, engineer, true);
    assert.deepEqual(evidence.pageErrors, []);
    console.log('PASS creator without assignment privileges and admin assignment failure after HTTP 201');
  } finally {
    await browser.close();
    writeFileSync(path.join(evidencePath, 'browser-evidence.json'), JSON.stringify(evidence, null, 2));
  }
}

run().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
