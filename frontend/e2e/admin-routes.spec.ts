import { expect, test, type Page } from '@playwright/test';

import { ROUTES } from '../src/shared/routes';

import { adminSession } from './world';

const ADMIN_MENU = 'Yönetim menüsü';
const MENU_PANEL = 'Menü';
/** Titles of the route error screen (`routeError.*`) and of the 404 page: none may show on a working route. */
const ERROR_SCREEN_TITLES = ['Sayfa açılamadı', 'Uygulama güncellenmiş olabilir', 'Sayfa bulunamadı'];
/** The admin pages, taken from the route table: a page added to `ROUTES` is covered without editing this list. */
const ADMIN_PATHS = Object.values(ROUTES).filter((path) => path === ROUTES.admin || path.startsWith(`${ROUTES.admin}/`));
/** A route two levels below the area, used for the reload check. */
const DEEP_PATH = ROUTES.adminSlaPolicy;
/** Class of one menu entry; the menu title (a link to `/admin`) is not an entry. */
const MENU_ITEM = 'a.astryx-side-nav-item';

async function expectPageOpened(page: Page, path: string) {
  await expect(page, `${path} stays on its URL`).toHaveURL(new RegExp(`${path}$`));
  await expect(page.getByRole('heading', { level: 1 }).first(), `${path} shows its heading`).toBeVisible();
  for (const title of ERROR_SCREEN_TITLES) {
    await expect(page.getByRole('heading', { name: title }), `${path} shows no "${title}" screen`).toHaveCount(0);
  }
}

/** The admin menu: always visible from 768 px, behind the menu button on phones. */
async function openMenu(page: Page) {
  const button = page.getByRole('button', { name: 'Menüyü aç' });
  const menu = page.getByRole('navigation', { name: ADMIN_MENU });
  await expect(button.or(menu).first()).toBeVisible();
  if (await button.isVisible()) {
    await button.click();
    return page.getByRole('dialog', { name: MENU_PANEL }).getByRole('navigation', { name: ADMIN_MENU });
  }
  return menu;
}

test('every_admin_route_opens_by_its_url', async ({ browser }) => {
  expect(ADMIN_PATHS.length).toBeGreaterThan(1);
  const { page } = await adminSession(browser);
  for (const path of ADMIN_PATHS) {
    await page.goto(path);
    await expectPageOpened(page, path);
  }
});

test('every_admin_menu_item_opens_its_page_and_a_deep_route_survives_a_reload', async ({ browser }) => {
  const { page } = await adminSession(browser);
  await page.goto(ROUTES.admin);
  await expectPageOpened(page, ROUTES.admin);

  const hrefs = await (await openMenu(page)).locator(MENU_ITEM).evaluateAll((links) => links.map((link) => link.getAttribute('href') ?? ''));
  const menuPaths = hrefs.filter((href) => href.startsWith(ROUTES.admin));
  expect(menuPaths.length).toBeGreaterThan(1);

  for (const path of menuPaths) {
    const menu = await openMenu(page);
    await menu.locator(`${MENU_ITEM}[href="${path}"]`).click();
    await expectPageOpened(page, path);
  }

  await page.goto(DEEP_PATH);
  await page.reload();
  await expectPageOpened(page, DEEP_PATH);
});
