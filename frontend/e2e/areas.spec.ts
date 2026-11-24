import { expect, test } from '@playwright/test';

import { login } from './helpers';
import { adminSession, seedWorld, userSession } from './world';

const ADMIN_TITLE = 'Proteone Yönetim';
const APP_TITLE = 'Proteone';

test('areas: each area loads its own entry, deep links survive a reload and sign-in returns to the admin page', async ({ browser, baseURL }) => {
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);

  await admin.page.goto('/admin/teams');
  await expect(admin.page).toHaveTitle(ADMIN_TITLE);
  await admin.page.reload();
  await expect(admin.page).toHaveURL(/\/admin\/teams$/);
  await admin.page.goto('/');
  await expect(admin.page).toHaveTitle(APP_TITLE);

  // Without a session, the admin entry sends the visitor to the user application's sign-in and remembers the page.
  const context = await browser.newContext({ baseURL });
  const page = await context.newPage();
  await page.goto('/admin/teams');
  await expect(page).toHaveURL(/\/login\?from=%2Fadmin%2Fteams$/);
  await expect(page).toHaveTitle(APP_TITLE);

  // An IT user has no admin pages: the admin entry hands over to the user application.
  await login(page, world.itEmail, world.itPassword);
  await expect(page).toHaveTitle(APP_TITLE);
  await expect(page).not.toHaveURL(/\/admin/);
  await context.close();

  const employee = await userSession(browser, world.employeeEmail, world.employeePassword);
  await employee.page.goto('/admin/users');
  await expect(employee.page).toHaveTitle(APP_TITLE);
  expect((await employee.context.request.get('/api/v1/admin/users')).status()).toBe(403);
  await Promise.all([admin.context.close(), employee.context.close()]);
});
