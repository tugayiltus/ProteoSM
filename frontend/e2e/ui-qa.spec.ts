/**
 * @input A fresh E2E database (tools/e2e.sh) and UI_QA_OUT, the folder for the results.
 * @output A full-page screenshot of every screen in light and dark mode per viewport, and problems.txt with sideways
 * overflow, console errors and page errors.
 * @position Browser QA sweep; run it with tools/ui-qa.sh, the regular E2E run ignores it.
 */
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';

import { expect, type Page, test } from '@playwright/test';

import { chooseOption, requiredEnv } from './helpers';
import { layoutProblems } from './layoutProbe';
import { adminSession, seedChange, seedChangeTask, seedIncidents, seedProblem, seedPublishedArticle, seedWorld, setProblemTarget, submitChange, userSession } from './world';

const OUT = requiredEnv('UI_QA_OUT');
const MODES = ['light', 'dark'] as const;
const COLOR_MODE_STORAGE_KEY = 'proteone-theme';
const SWEEP_TIMEOUT_MS = 600_000;
const SETTLE_MS = 800;
const MESSAGE_LENGTH = 200;
const FILE_NAME_LENGTH = 80;
const SEARCH_BUTTON = 'Ara';
const PROBLEM_TARGET_MINUTES = 480;
const RATING_SECTION = 'Memnuniyet';
/** File name prefix of the employee's screens, so they do not overwrite the admin's view of the same path. */
const EMPLOYEE_PREFIX = 'employee';

/** The desk claims and completes the ticket, so it becomes rateable for its requester. */
async function completeTicket(desk: Page, number: string): Promise<void> {
  await desk.goto(`/tickets/${number}`);
  await desk.getByRole('button', { name: 'Üzerime al' }).click();
  // Wait for the claim to land, so the completion sends the new ticket version.
  await expect(desk.getByRole('button', { name: 'Üzerime al' })).toBeHidden();
  await desk.getByRole('button', { name: 'Tamamla' }).click();
  const dialog = desk.getByRole('dialog');
  await dialog.getByLabel('Çözüm (kullanıcı görür)').fill('Kablo değiştirildi');
  await chooseOption(dialog, 'Sonuç', 'Çözüldü');
  await dialog.getByRole('button', { name: 'Onayla' }).click();
  await expect(dialog).toBeHidden();
}

/** The requester gives the ticket a score through the ticket page. */
async function rateTicket(requester: Page, number: string, score: string): Promise<void> {
  await requester.goto(`/tickets/${number}?rate=1`);
  const rating = requester.getByRole('region', { name: RATING_SECTION });
  await rating.getByRole('radio', { name: score }).check();
  await rating.getByRole('button', { name: 'Gönder' }).click();
  await expect(rating.getByRole('button', { name: 'Değiştir' })).toBeVisible();
}

/** Screenshots each path in light and dark mode and records sideways overflow under the mode and path. */
async function sweep(page: Page, dir: string, prefix: string, paths: readonly string[], problems: string[]): Promise<void> {
  for (const mode of MODES) {
    await page.evaluate(({ key, value }) => localStorage.setItem(key, value), { key: COLOR_MODE_STORAGE_KEY, value: mode });
    for (const target of paths) {
      await page.goto(target);
      await page.waitForLoadState('load');
      await page.waitForTimeout(SETTLE_MS);
      for (const problem of await layoutProblems(page, target)) {
        problems.push(`layout ${mode}${prefix} ${target}: ${problem}`);
      }
      const name = `${mode}${prefix}${target.replaceAll('/', '_') || '_home'}`.slice(0, FILE_NAME_LENGTH);
      await page.screenshot({ path: path.join(dir, `${name}.png`), fullPage: true });
    }
  }
}

/** Collects console errors and page errors of one page into the problem list. */
function watchErrors(page: Page, problems: string[]): void {
  page.on('console', (message) => {
    if (message.type() === 'error') {
      problems.push(`console ${page.url()}: ${message.text().slice(0, MESSAGE_LENGTH)}`);
    }
  });
  page.on('pageerror', (error) => problems.push(`pageerror ${page.url()}: ${error.message.slice(0, MESSAGE_LENGTH)}`));
}

test('every screen in light and dark mode', async ({ browser }, info) => {
  test.setTimeout(SWEEP_TIMEOUT_MS);
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const [ticket] = await seedIncidents(admin, ['Ekran titriyor, monitör kablosu gevşek olabilir']);
  const article = await seedPublishedArticle(admin, 'VPN bağlantısı nasıl kurulur', 'employees');
  // A change waiting for the admin's approval, with a task in its Hazırlık phase (the task screen shows the phase).
  const owner = await userSession(browser, world.itEmail, world.itPassword);
  const changeNumber = await seedChange(owner, world, 'Dosya sunucusu güncellemesi', null);
  const changeTask = await seedChangeTask(owner, changeNumber, 'preparation', 'Yedek al', world.teamId);
  await seedChangeTask(owner, changeNumber, 'implementation', 'Yamayı kur', world.teamId);
  await submitChange(owner, changeNumber);
  // Satisfaction rating: one completed ticket waits for a score (form, home card, staff "Puan yok"), one is rated
  // (rated state and the report average).
  const employee = await userSession(browser, world.employeeEmail, world.employeePassword);
  const [toRate, rated] = await seedIncidents(employee, ['Monitör görüntü vermiyor', 'Fare çift tıklıyor']);
  await completeTicket(owner.page, toRate ?? '');
  await completeTicket(owner.page, rated ?? '');
  await rateTicket(employee.page, rated ?? '', 'İyi');
  // A problem with a running known-error timer: the SLA card on its detail page and the badge in the list.
  await setProblemTarget(admin, 'high', PROBLEM_TARGET_MINUTES);
  const problemNumber = await seedProblem(owner, 'VPN bağlantısı sık sık kopuyor', 'high', world.teamId);
  await owner.context.close();
  const page = admin.page;
  const found = (await (await admin.context.request.get(`/api/v1/assets?search=${encodeURIComponent(world.assetTag)}`)).json()) as { items: { id: string }[] };
  const assetUrl = `/assets/${found.items[0]?.id ?? ''}`;

  const paths = [
    '/', '/workspace', '/workspace/queue', '/tickets', `/tickets/${ticket ?? ''}`, `/tickets/${toRate ?? ''}`, `/tickets/${rated ?? ''}`, '/tickets/new/incident', '/tickets/new/request', '/approvals',
    '/assets', assetUrl, '/assets/mine', '/assets/import', '/kb', `/kb/${article.id}`, `/kb/${article.id}/edit`, '/kb/new',
    '/changes', `/changes/${changeNumber}`, `/tasks/${changeTask}`, '/changes/calendar', '/releases', '/licences', '/software', '/software/mine', '/admin/monitoring', '/reports/licences', '/reports', '/reports/incidents', '/reports/changes', '/reports/tasks', '/reports/exports', '/profile', '/change-password',
    '/admin', '/admin/users', '/admin/departments', '/admin/teams', '/admin/locations', '/admin/categories', '/admin/request-types',
    '/admin/brands', '/admin/models', '/admin/sla/policy', '/admin/sla/calendar', '/admin/email-templates', '/admin/email-deliveries', '/admin/email-sending',
    '/admin/audit', '/admin/system', '/admin/settings', '/admin/announcements', '/admin/login-method', '/admin/services', '/tasks', '/problems', `/problems/${problemNumber}`, '/services', '/nope-404',
  ];
  const dir = path.join(OUT, info.project.name);
  mkdirSync(dir, { recursive: true });
  const problems: string[] = [];
  watchErrors(page, problems);
  watchErrors(employee.page, problems);

  await sweep(page, dir, '', paths, problems);
  await sweep(employee.page, dir, `_${EMPLOYEE_PREFIX}`, ['/', `/tickets/${toRate ?? ''}?rate=1`, `/tickets/${rated ?? ''}`], problems);
  // The search palette in its open state, with a result list, per mode.
  for (const mode of MODES) {
    await page.evaluate(({ key, value }) => localStorage.setItem(key, value), { key: COLOR_MODE_STORAGE_KEY, value: mode });
    await page.goto('/workspace');
    await page.waitForLoadState('load');
    await page.getByRole('button', { name: SEARCH_BUTTON, exact: true }).first().click();
    await page.keyboard.type('Dosya');
    await page.waitForTimeout(SETTLE_MS);
    await page.screenshot({ path: path.join(dir, `${mode}_search-open.png`) });
    await page.keyboard.press('Escape');
  }
  writeFileSync(path.join(dir, 'problems.txt'), problems.join('\n'));
  // The sweep fails on any layout problem, so a clipped column or a page wider than the viewport cannot ship unseen.
  expect(problems.filter((problem) => problem.startsWith('layout '))).toEqual([]);
});
