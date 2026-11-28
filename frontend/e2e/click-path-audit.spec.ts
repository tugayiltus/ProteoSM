/**
 * @input A fresh E2E database (tools/e2e.sh) and AUDIT_OUT, the folder for screenshots and the findings report.
 * @output Per role (employee, IT staff, admin) and viewport: every menu route opened by menu, by URL and after a reload,
 * with console errors, page and inner overflow, headings, a11y quick checks and screenshots; plus the critical flows
 * walked button by button. Findings go to findings-<viewport>.json; only console errors and unreachable routes fail.
 * @position Role-based click-path and UX audit (docs/release/proteosm-audit.md). The spec skips itself without AUDIT_OUT.
 */
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';

import { expect, type Page, test } from '@playwright/test';

import { chooseOption, openCard } from './helpers';
import {
  adminSession, seedChange, seedChangeTask, seedIncidents, seedProblem, seedPublishedArticle, seedService, seedWorld, setProblemTarget, submitChange, userSession,
  type Session, type World,
} from './world';

type Role = 'employee' | 'it' | 'admin';

interface Finding {
  role: Role | 'all';
  view: string;
  theme: string;
  route: string;
  kind: string;
  detail: string;
}

interface PageMetrics {
  pageOverflow: number;
  inner: string[];
  offscreen: string[];
  h1: number;
  headings: number;
  main: number;
  unnamedControls: string[];
  unlabelledInputs: number;
  imagesWithoutAlt: number;
  smallTargets: number;
  title: string;
  text: string;
}

const AUDIT_OUT = process.env.AUDIT_OUT ?? '';
const COLOR_MODE_STORAGE_KEY = 'proteone-theme';
const TEST_TIMEOUT_MS = 1_500_000;
const SETTLE_MS = 600;
const OVERFLOW_TOLERANCE_PX = 1;
const MIN_TARGET_PX = 24;
const TAB_PRESSES = 6;
const SAMPLE_LENGTH = 160;
const MAX_LIST = 6;
const FILE_NAME_LENGTH = 90;
const BREADCRUMB_LABEL = 'Sayfa yolu';
const REPORT_TABS = ['incidents', 'requests', 'operations', 'transfers', 'assets', 'warranty', 'changes', 'tasks', 'problems', 'licences'] as const;
const PROBLEM_TARGET_MINUTES = 480;
const ERROR_TEXT = /Hata|hata oluştu|yüklenemedi|Bir şeyler ters/;
const EMPTY_TEXT = /bulunamadı|kayıt yok|Henüz|sonuç yok|yok\./i;

const findings: Finding[] = [];
const consoleErrors: string[] = [];
const unreachable: string[] = [];

function note(role: Finding['role'], view: string, theme: string, route: string, kind: string, detail: string): void {
  findings.push({ role, view, theme, route, kind, detail });
}

/** Runs in the page: layout, overflow and quick accessibility measurements of the current screen. */
function measureInPage(args: { tolerance: number; minTarget: number; sample: number; max: number }): PageMetrics {
  const doc = document.documentElement;
  const main = document.querySelector('main, [role="main"]') ?? document.body;
  const describe = (el: Element): string => `${el.tagName.toLowerCase()}${el.getAttribute('role') ? `[${String(el.getAttribute('role'))}]` : ''} "${(el.textContent ?? '').trim().replace(/\s+/g, ' ').slice(0, 30)}"`;
  const inner: string[] = [];
  const offscreen: string[] = [];
  const unnamed: string[] = [];
  let smallTargets = 0;
  const all = Array.from(main.querySelectorAll('*')).slice(0, 4000);
  for (const el of all) {
    const style = getComputedStyle(el);
    if (style.display === 'none' || style.visibility === 'hidden') {
      continue;
    }
    if (el.scrollWidth > el.clientWidth + args.tolerance && style.overflowX !== 'visible' && el.clientWidth > 1 && !el.matches('a[href="#main-content"], a[href^="#"]') && inner.length < args.max) {
      const mode = style.overflowX === 'auto' || style.overflowX === 'scroll' ? 'scrolls sideways' : 'clips content';
      inner.push(`${mode}: ${describe(el)} (${el.scrollWidth}>${el.clientWidth})`);
    }
    if (el.tagName === 'TABLE' || el.getAttribute('role') === 'table') {
      let holder: Element | null = el.parentElement;
      while (holder !== null && !['auto', 'scroll'].includes(getComputedStyle(holder).overflowX) && holder !== document.body) {
        holder = holder.parentElement;
      }
      const room = holder?.clientWidth ?? doc.clientWidth;
      if (el.scrollWidth > room + args.tolerance && inner.length < args.max) {
        inner.push(`table wider than its container: ${describe(el)} (${el.scrollWidth}>${room})`);
      }
    }
    const interactive = el.matches('a[href], button, input, select, textarea, [role="button"], [role="tab"], [role="menuitem"]');
    if (interactive) {
      const rect = el.getBoundingClientRect();
      if (rect.width > 0 && rect.height > 0) {
        if ((rect.right > window.innerWidth + args.tolerance || rect.left < -args.tolerance) && offscreen.length < args.max) {
          offscreen.push(`outside viewport: ${describe(el)}`);
        }
        if ((rect.width < args.minTarget || rect.height < args.minTarget) && !el.matches('input[type="checkbox"], input[type="radio"]')) {
          smallTargets += 1;
        }
        if (el.matches('a[href], button, [role="button"]')) {
          const named = (el.getAttribute('aria-label') ?? '').trim() !== '' || (el.getAttribute('aria-labelledby') ?? '') !== ''
            || (el.textContent ?? '').trim() !== '' || (el.getAttribute('title') ?? '') !== '' || el.querySelector('img[alt]:not([alt=""])') !== null;
          if (!named && unnamed.length < args.max) {
            unnamed.push(`${el.tagName.toLowerCase()} without name (${el.className.toString().slice(0, 40)})`);
          }
        }
      }
    }
  }
  let unlabelled = 0;
  for (const input of Array.from(main.querySelectorAll('input:not([type="hidden"]), select, textarea'))) {
    const id = input.getAttribute('id');
    const labelled = (input.getAttribute('aria-label') ?? '') !== '' || (input.getAttribute('aria-labelledby') ?? '') !== ''
      || input.closest('label') !== null || (id !== null && document.querySelector(`label[for="${CSS.escape(id)}"]`) !== null);
    if (!labelled) {
      unlabelled += 1;
    }
  }
  return {
    pageOverflow: doc.scrollWidth - doc.clientWidth,
    inner,
    offscreen,
    h1: document.querySelectorAll('h1').length,
    headings: document.querySelectorAll('h1, h2, h3, [role="heading"]').length,
    main: document.querySelectorAll('main, [role="main"]').length,
    unnamedControls: unnamed,
    unlabelledInputs: unlabelled,
    imagesWithoutAlt: document.querySelectorAll('main img:not([alt])').length,
    smallTargets,
    title: document.title,
    text: (main.textContent ?? '').trim().replace(/\s+/g, ' ').slice(0, args.sample * 4),
  };
}

function screenshotName(role: Role, view: string, theme: string, route: string): string {
  const slug = route.replaceAll(/[/?=&]/g, '_').replace(/^_/, '') || 'home';
  return `${role}-${view}-${theme}-${slug}`.slice(0, FILE_NAME_LENGTH);
}

async function setTheme(page: Page, theme: string): Promise<void> {
  await page.evaluate(({ key, value }) => localStorage.setItem(key, value), { key: COLOR_MODE_STORAGE_KEY, value: theme });
}

const MENU_WAIT_MS = 3000;
const HEADING_WAIT_MS = 5000;

async function opensMenu(page: Page): Promise<void> {
  const opener = page.getByRole('button', { name: 'Menüyü aç' });
  if (await opener.waitFor({ state: 'visible', timeout: MENU_WAIT_MS }).then(() => true, () => false)) {
    await opener.click();
  }
}

/** Opens a route through the sidebar link (the phone drawer first) and reports whether it landed on the target. */
async function openByMenu(page: Page, route: string): Promise<string> {
  await page.goto(route.startsWith('/admin') ? '/admin' : '/');
  await page.waitForLoadState('load');
  await opensMenu(page);
  const link = page.locator(`nav:not([aria-label="${BREADCRUMB_LABEL}"]) a[href="${route}"]`).first();
  if (!(await link.isVisible().catch(() => false))) {
    return 'menu link not visible';
  }
  await link.click();
  try {
    await expect(page).toHaveURL(new RegExp(`${route.replaceAll('/', '\\/')}(\\?.*)?$`));
  } catch {
    return `menu click ended on ${new URL(page.url()).pathname}`;
  }
  return '';
}

interface WalkOptions {
  page: Page;
  role: Role;
  view: string;
  themes: readonly string[];
  routes: readonly string[];
  menuRoutes: ReadonlySet<string>;
  /** Routes the role must not see: recorded, no screenshot. */
  denied?: readonly string[];
}

/** Opens each route by menu, by URL and after a reload, and records what the screen shows. */
async function walk(options: WalkOptions): Promise<void> {
  const { page, role, view, themes, routes, menuRoutes } = options;
  let current = '';
  page.on('console', (message) => {
    if (message.type() === 'error' && !message.text().includes('status of 40')) {
      consoleErrors.push(`${role}/${view} ${current}: ${message.text().slice(0, SAMPLE_LENGTH)}`);
    }
  });
  page.on('pageerror', (error) => consoleErrors.push(`${role}/${view} ${current} pageerror: ${error.message.slice(0, SAMPLE_LENGTH)}`));
  for (const theme of themes) {
    await setTheme(page, theme);
    for (const route of routes) {
      current = route;
      const isFirstTheme = theme === themes[0];
      if (isFirstTheme && menuRoutes.has(route)) {
        const problem = await openByMenu(page, route);
        if (problem !== '') {
          note(role, view, theme, route, 'menu', problem);
        }
      }
      await page.goto(route);
      await page.waitForLoadState('load');
      await page.waitForTimeout(SETTLE_MS);
      if (isFirstTheme) {
        await page.reload();
        await page.waitForLoadState('load');
        await page.waitForTimeout(SETTLE_MS);
        if (new URL(page.url()).pathname !== new URL(route, page.url()).pathname) {
          note(role, view, theme, route, 'redirect', `reload ended on ${new URL(page.url()).pathname}`);
        }
      }
      const m = await page.evaluate(measureInPage, { tolerance: OVERFLOW_TOLERANCE_PX, minTarget: MIN_TARGET_PX, sample: SAMPLE_LENGTH, max: MAX_LIST });
      const heading = await page.getByRole('heading').first().waitFor({ state: 'visible', timeout: HEADING_WAIT_MS }).then(() => true, () => false);
      if (!heading || /bulunamadı|404/i.test(m.title + m.text.slice(0, 80))) {
        unreachable.push(`${role}/${view} ${route}: no heading or not-found page (${m.text.slice(0, 60)})`);
        note(role, view, theme, route, 'unreachable', `no visible heading; text: ${m.text.slice(0, 80)}`);
      }
      if (isFirstTheme) {
        if (m.h1 !== 1) {
          note(role, view, theme, route, 'a11y', `${m.h1} h1 elements (want 1)`);
        }
        if (m.main !== 1) {
          note(role, view, theme, route, 'a11y', `${m.main} main landmarks (want 1)`);
        }
        if (m.unnamedControls.length > 0) {
          note(role, view, theme, route, 'a11y', `controls without name: ${m.unnamedControls.join('; ')}`);
        }
        if (m.unlabelledInputs > 0) {
          note(role, view, theme, route, 'a11y', `${m.unlabelledInputs} inputs without label`);
        }
        if (m.imagesWithoutAlt > 0) {
          note(role, view, theme, route, 'a11y', `${m.imagesWithoutAlt} images without alt`);
        }
        if (view === 'phone' && m.smallTargets > 0) {
          note(role, view, theme, route, 'target-size', `${m.smallTargets} controls under ${MIN_TARGET_PX}px`);
        }
        if (ERROR_TEXT.test(m.text)) {
          note(role, view, theme, route, 'error-state', m.text.slice(0, SAMPLE_LENGTH));
        }
        if (EMPTY_TEXT.test(m.text)) {
          note(role, view, theme, route, 'empty-state', m.text.slice(0, SAMPLE_LENGTH));
        }
        // Keyboard: Tab must move focus into the page, and reach a control of the main region within a few presses.
        await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur());
        const reached: string[] = [];
        for (let press = 0; press < TAB_PRESSES; press += 1) {
          await page.keyboard.press('Tab');
          reached.push(await page.evaluate(() => {
            const el = document.activeElement;
            return el === null || el === document.body ? 'body' : `${el.tagName.toLowerCase()}${el.closest('main') ? '@main' : ''}`;
          }));
        }
        if (reached.every((entry) => entry === 'body')) {
          note(role, view, theme, route, 'keyboard', 'Tab never moves focus into the page');
        }
      }
      if (m.pageOverflow > OVERFLOW_TOLERANCE_PX) {
        note(role, view, theme, route, 'overflow', `page scrolls sideways by ${m.pageOverflow}px`);
      }
      if (isFirstTheme || m.pageOverflow > OVERFLOW_TOLERANCE_PX) {
        for (const item of [...m.inner, ...m.offscreen]) {
          note(role, view, theme, route, 'inner-overflow', item);
        }
      }
      await page.screenshot({ path: path.join(AUDIT_OUT, 'screens', `${screenshotName(role, view, theme, route)}.png`), fullPage: true });
    }
  }
  for (const route of options.denied ?? []) {
    current = `denied ${route}`;
    await page.goto(route);
    await page.waitForLoadState('load');
    await page.waitForTimeout(SETTLE_MS);
    const m = await page.evaluate(measureInPage, { tolerance: OVERFLOW_TOLERANCE_PX, minTarget: MIN_TARGET_PX, sample: SAMPLE_LENGTH, max: MAX_LIST });
    note(role, view, 'light', route, 'denied-route', `ends on ${new URL(page.url()).pathname}; text: ${m.text.slice(0, 100)}`);
  }
}

/** Runs one flow step; a failure becomes a finding and the audit goes on. */
async function step(role: Role, view: string, name: string, action: () => Promise<void>): Promise<void> {
  try {
    await action();
    note(role, view, 'light', name, 'flow-ok', 'passed');
  } catch (error) {
    note(role, view, 'light', name, 'flow-fail', String(error instanceof Error ? error.message : error).split('\n').slice(0, 3).join(' | ').slice(0, 400));
  }
}

async function postJson(session: Session, route: string, data: unknown): Promise<Record<string, unknown>> {
  const csrf = (await (await session.context.request.get('/api/v1/auth/csrf')).json()) as { token: string };
  const response = await session.context.request.post(`/api/v1${route}`, { data, headers: { 'X-CSRF-TOKEN': csrf.token } });
  if (!response.ok()) {
    throw new Error(`POST ${route} -> ${response.status()} ${(await response.text()).slice(0, 200)}`);
  }
  return (await response.json()) as Record<string, unknown>;
}

interface Seeds {
  ticketOpen: string;
  ticketMine: string;
  problem: string;
  change: string;
  task: string;
  release: string;
  licence: string;
  service: string;
  asset: string;
  article: string;
}

async function seedModules(admin: Session, owner: Session, employee: Session, world: World): Promise<Seeds> {
  const [ticketOpen, ticketMine] = await seedIncidents(employee, ['Yazıcı kağıt sıkıştırıyor', 'VPN bağlantısı kopuyor']);
  await setProblemTarget(admin, 'high', PROBLEM_TARGET_MINUTES);
  const problem = await seedProblem(owner, 'VPN bağlantısı sık sık kopuyor', 'high', world.teamId);
  const change = await seedChange(owner, world, 'Dosya sunucusu güncellemesi', world.assetId);
  const task = await seedChangeTask(owner, change, 'preparation', 'Yedek al', world.teamId);
  await seedChangeTask(owner, change, 'implementation', 'Yamayı kur', world.teamId);
  await submitChange(owner, change);
  const article = await seedPublishedArticle(owner, 'VPN bağlantısı nasıl kurulur', 'employees');
  const service = await seedService(admin, `E-posta ${Date.now()}`, world.teamId, world.assetId);
  const software = await postJson(owner, '/software', { name: `Ofis paketi ${Date.now()}`, vendor: 'Acme' });
  const licence = await postJson(owner, '/licences', { softwareId: software.id, name: 'Ofis 100 kullanıcı', kind: 'per_user', quantity: 100, ownerTeamId: world.teamId, expiresOn: '2027-12-31', notes: 'Yıllık' });
  await postJson(owner, `/licences/${String(licence.id)}/assignments`, { userId: world.itUserId, assetId: null }).catch(() => undefined);
  const release = await postJson(owner, '/releases', { title: 'Aralık güncelleme paketi', description: 'Sunucu yamaları', ownerTeamId: world.teamId, ownerId: null, windowStart: null, windowEnd: null, notes: null });
  await postJson(admin, '/admin/announcements', { title: 'Planlı bakım', body: 'Cumartesi gece sunucular kapalı.', level: 'warning', startsAt: new Date(Date.now() - 3_600_000).toISOString(), endsAt: null });
  return {
    ticketOpen: ticketOpen ?? '', ticketMine: ticketMine ?? '', problem, change, task, release: String(release.number ?? ''), licence: String(licence.id), service,
    asset: world.assetId, article: article.id,
  };
}

function routesFor(role: Role, s: Seeds): { routes: string[]; menu: Set<string> } {
  const common = ['/', '/tickets', `/tickets/${s.ticketOpen}`, '/tickets/new/incident', '/tickets/new/request', '/kb', `/kb/${s.article}`, '/assets/mine', '/software/mine', '/profile', '/change-password'];
  const staff = [
    '/workspace', '/workspace/queue', '/tasks', `/tasks/${s.task}`, '/problems', `/problems/${s.problem}`, '/changes', '/changes/calendar', `/changes/${s.change}`, '/releases', `/releases/${s.release}`,
    '/assets', `/assets/${s.asset}`, '/assets/import', '/services', `/services/${s.service}`, '/licences', `/licences/${s.licence}`, '/software', '/kb/new', `/kb/${s.article}/edit`,
    ...REPORT_TABS.map((tab) => `/reports/${tab}`), '/reports/incidents/list?metric=open_backlog', '/reports/exports',
  ];
  const admin = [
    '/approvals', '/admin', '/admin/users', '/admin/departments', '/admin/locations', '/admin/teams', '/admin/categories', '/admin/request-types', '/admin/services', '/admin/brands', '/admin/models',
    '/admin/email-templates', '/admin/email-deliveries', '/admin/email-sending', '/admin/announcements', '/admin/sla/calendar', '/admin/sla/policy', '/admin/settings', '/admin/login-method', '/admin/monitoring',
    '/admin/system', '/admin/audit',
  ];
  const menu = new Set(['/', '/tickets', '/kb', '/assets/mine', '/software/mine', '/workspace', '/tasks', '/problems', '/changes', '/releases', '/assets', '/services', '/licences', '/reports', '/approvals', ...admin.filter((r) => r.startsWith('/admin'))]);
  if (role === 'employee') {
    return { routes: common, menu };
  }
  return { routes: role === 'it' ? [...common, ...staff] : [...common, ...staff, ...admin], menu };
}

test('role click-path audit', async ({ browser }, info) => {
  test.skip(AUDIT_OUT === '', 'Run through tools/e2e.sh with AUDIT_OUT set (docs/release/proteosm-audit.md).');
  test.setTimeout(TEST_TIMEOUT_MS);
  const view = info.project.name === 'mobile-360' ? 'phone' : 'desktop';
  const themes = view === 'phone' ? ['light'] : ['light', 'dark'];
  mkdirSync(path.join(AUDIT_OUT, 'screens'), { recursive: true });

  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const owner = await userSession(browser, world.itEmail, world.itPassword);
  const employee = await userSession(browser, world.employeeEmail, world.employeePassword);
  const seeds = await seedModules(admin, owner, employee, world);

  for (const role of ['employee', 'it', 'admin'] as const) {
    const session = role === 'employee' ? employee : role === 'it' ? owner : admin;
    const { routes, menu } = routesFor(role, seeds);
    // Dark mode is checked once per area on desktop: the employee and the admin cover every screen family.
    const roleThemes = role === 'it' ? themes.filter((t) => t === 'light') : themes;
    await walk({
      page: session.page, role, view, themes: roleThemes, routes, menuRoutes: menu,
      denied: role === 'employee' ? ['/workspace', '/reports', '/admin', '/licences', '/changes', '/approvals'] : role === 'it' ? ['/admin', '/approvals', '/admin/users'] : [],
    });
  }

  // Critical flows, as the people would click them (action-tested).
  const e = employee.page;
  await step('employee', view, 'flow: incident form shows validation when empty', async () => {
    await e.goto('/');
    await openCard(e, 'Bir şey çalışmıyor');
    await e.getByRole('button', { name: 'Gönder' }).click();
    await expect(e.getByRole('alert').or(e.getByText(/zorunlu|gerekli/i)).first()).toBeVisible();
    const focused = await e.evaluate(() => document.activeElement?.tagName ?? 'none');
    if (focused === 'BODY') {
      throw new Error('focus stays on body after a failed submit; first invalid field not focused');
    }
  });
  let incidentUrl = '';
  await step('employee', view, 'flow: create incident -> ticket page', async () => {
    await e.goto('/');
    await openCard(e, 'Bir şey çalışmıyor');
    await e.getByLabel(/^Konu/).fill('Monitör titriyor');
    await e.getByLabel('Açıklama').fill('Ekran sürekli titriyor');
    await e.getByRole('button', { name: 'Gönder' }).click();
    await expect(e).toHaveURL(/\/tickets\/INC-\d{4}-\d{6}$/);
    incidentUrl = e.url();
    await expect(e.getByRole('heading').first()).toBeVisible();
  });
  let requestNumber = '';
  await step('employee', view, 'flow: create request needing approval', async () => {
    await e.goto('/');
    await openCard(e, 'Bir şey istiyorum');
    await chooseOption(e, /^Talep tipi/, world.requestTypeName);
    await e.getByLabel(/^Konu/).fill('Yeni dizüstü bilgisayar');
    await e.getByLabel('Açıklama').fill('Yeni başlayan için');
    await e.getByRole('button', { name: 'Gönder' }).click();
    await expect(e).toHaveURL(/\/tickets\/REQ-\d{4}-\d{6}$/);
    requestNumber = e.url().split('/').pop() ?? '';
  });
  await step('admin', view, 'flow: approve request from /approvals', async () => {
    await admin.page.goto('/approvals');
    await admin.page.getByRole('button', { name: `Onayla ${requestNumber}` }).click();
    await admin.page.getByRole('dialog').getByRole('button', { name: 'Onayla' }).click();
    await expect(admin.page.getByRole('dialog')).toBeHidden();
    const active = await admin.page.evaluate(() => document.activeElement?.tagName ?? 'none');
    if (active === 'BODY') {
      note('admin', view, 'light', '/approvals', 'focus', 'focus falls to body after the approve dialog closes');
    }
  });
  const it = owner.page;
  await step('it', view, 'flow: claim, internal note, reply, complete', async () => {
    await it.goto(incidentUrl);
    await it.getByRole('button', { name: 'Üzerime al' }).click();
    await expect(it.getByRole('button', { name: 'Üzerime al' })).toBeHidden();
    const box = it.getByRole('form', { name: 'Yazışmalar' });
    await box.getByRole('tab', { name: 'İç not' }).click();
    await box.getByLabel('Mesajınız').fill('Kablo değişecek');
    await box.getByRole('button', { name: 'Gönder' }).click();
    await expect(it.getByText('Kablo değişecek')).toBeVisible();
    await box.getByRole('tab', { name: 'Yanıt' }).click();
    await box.getByLabel('Mesajınız').fill('Kabloyu değiştiriyoruz');
    await box.getByRole('button', { name: 'Gönder' }).click();
    await expect(it.getByText('Kabloyu değiştiriyoruz')).toBeVisible();
    await it.getByRole('button', { name: 'Tamamla' }).click();
    const dialog = it.getByRole('dialog');
    await dialog.getByLabel('Çözüm (kullanıcı görür)').fill('Kablo değiştirildi');
    await chooseOption(dialog, 'Sonuç', 'Çözüldü');
    await dialog.getByRole('button', { name: 'Onayla' }).click();
    await expect(dialog).toBeHidden();
    const active = await it.evaluate(() => document.activeElement?.tagName ?? 'none');
    if (active === 'BODY') {
      note('it', view, 'light', 'ticket detail', 'focus', 'focus falls to body after the complete dialog closes');
    }
  });
  await step('employee', view, 'flow: employee sees resolution, not the internal note', async () => {
    await e.goto(incidentUrl);
    await expect(e.getByText('Kablo değiştirildi').first()).toBeVisible();
    await expect(e.getByText('Kablo değişecek')).toHaveCount(0);
  });
  await step('it', view, 'flow: link problem from ticket detail', async () => {
    await it.goto(`/tickets/${seeds.ticketOpen}`);
    await it.getByRole('button', { name: 'Üzerime al' }).click();
    const link = it.getByRole('button', { name: /Problem/i }).first();
    await expect(link).toBeVisible();
    await link.click();
    await expect(it.getByRole('dialog').or(it.getByRole('menu')).first()).toBeVisible();
    await it.keyboard.press('Escape');
  });
  await step('it', view, 'flow: create change from /changes', async () => {
    await it.goto('/changes');
    await it.getByRole('button', { name: /Yeni/ }).or(it.getByRole('link', { name: /Yeni/ })).first().click();
    await expect(it.getByLabel(/^Başlık|^Konu/).first()).toBeVisible();
  });
  await step('it', view, 'flow: search palette opens a result', async () => {
    await it.goto('/workspace');
    await it.getByRole('button', { name: 'Ara', exact: true }).first().click();
    await it.keyboard.type('Dosya');
    await it.waitForTimeout(SETTLE_MS * 2);
    await it.keyboard.press('ArrowDown');
    await it.keyboard.press('Enter');
    await expect(it).not.toHaveURL(/\/workspace$/);
  });
  await step('it', view, 'flow: export Excel from a report', async () => {
    await it.goto('/reports/incidents/list?metric=open_backlog');
    const button = it.getByRole('button', { name: "Excel'e aktar" });
    await expect(button).toBeVisible();
    const download = it.waitForEvent('download', { timeout: 15_000 });
    await button.click();
    await download;
  });
  await step('it', view, 'flow: account menu reaches profile and sign-out by keyboard', async () => {
    await it.goto('/');
    const account = it.getByRole('button', { name: /^Hesap menüsü/ });
    await account.focus();
    await it.keyboard.press('Enter');
    await expect(it.getByRole('menuitem').first()).toBeVisible();
    const names = await it.getByRole('menuitem').allInnerTexts();
    note('it', view, 'light', 'account menu', 'info', `items: ${names.join(' | ')}`);
    await it.keyboard.press('Escape');
    const back = await it.evaluate(() => document.activeElement?.getAttribute('aria-label') ?? 'none');
    if (!back.startsWith('Hesap menüsü')) {
      note('it', view, 'light', 'account menu', 'focus', `focus after Escape is on ${back}, not the account button`);
    }
  });

  writeFileSync(path.join(AUDIT_OUT, `findings-${view}.json`), JSON.stringify({ findings, consoleErrors, unreachable }, null, 2));
  await Promise.all([admin.context.close(), owner.context.close(), employee.context.close()]);
  expect(consoleErrors, 'console errors').toEqual([]);
  expect(unreachable, 'unreachable routes').toEqual([]);
});
