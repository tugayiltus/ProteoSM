import { expect, type Page, test } from '@playwright/test';

import { chooseOption, expectNoHorizontalScroll } from './helpers';
import { adminSession, seedIncidents, seedWorld, userSession } from './world';

const RATE_CARD = 'Puan bekleyen kayıtlar';
const RATING_SECTION = 'Memnuniyet';
const GIVEN_SCORE = 'Çok iyi';
const GIVEN_SCORE_TEXT = '5 / 5 · Çok iyi';
const AVERAGE_FIGURE = 'Ortalama memnuniyet puanı';
const AVERAGE_TEXT = '5,0 / 5';
const RATE_LINK = /\/tickets\/INC-\d{4}-\d{6}\?rate=1$/;
/** Seeding four users and walking three sessions through the ticket exceed the 30 s default. */
const RATINGS_TEST_TIMEOUT_MS = 90_000;

/** Filters the report by department; on phones the filter sits in the "Filtreler (n)" drawer. */
async function filterByDepartment(page: Page, departmentName: string): Promise<void> {
  const filtersButton = page.getByRole('button', { name: /^Filtreler \(\d+\)$/ });
  const moreFilters = page.getByRole('button', { name: /^Diğer filtreler/ });
  // isVisible does not wait: wait until the toolbar shows one of the two layouts before choosing a branch.
  await expect(filtersButton.or(moreFilters).first()).toBeVisible();
  if (await filtersButton.isVisible()) {
    await filtersButton.click();
    const drawer = page.getByRole('dialog', { name: 'Filtreler' });
    await drawer.getByRole('button', { name: /^Diğer filtreler/ }).click();
    await chooseOption(drawer, 'Departman', departmentName);
    await page.keyboard.press('Escape');
    // Text checks wait until the drawer is gone, so they do not read a closing dialog.
    await expect(drawer).toBeHidden();
  } else {
    await moreFilters.click();
    await chooseOption(page, 'Departman', departmentName);
  }
}

test('ratings: employee rates from the email link and desk sees it', async ({ browser }) => {
  test.setTimeout(RATINGS_TEST_TIMEOUT_MS);
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const employee = await userSession(browser, world.employeeEmail, world.employeePassword);
  const desk = await userSession(browser, world.itEmail, world.itPassword);
  const subject = `Monitör görüntü vermiyor ${Date.now()}`;
  const [number] = await seedIncidents(employee, [subject]);
  const ticketPath = `/tickets/${number}`;

  await desk.page.goto(ticketPath);
  await desk.page.getByRole('button', { name: 'Üstlen' }).click();
  // Wait for the claim to land, so the completion sends the new ticket version.
  await expect(desk.page.getByRole('button', { name: 'Üstlen' })).toBeHidden();
  await desk.page.getByRole('button', { name: 'Tamamla' }).click();
  const dialog = desk.page.getByRole('dialog');
  await dialog.getByLabel('Çözüm (kullanıcı görür)').fill('Kablo değiştirildi');
  await chooseOption(dialog, 'Sonuç', 'Çözüldü');
  await dialog.getByRole('button', { name: 'Onayla' }).click();
  await expect(dialog).toBeHidden();
  await expect(desk.page.getByText('Tamamlandı').first()).toBeVisible();

  // The home card links to the same address as the rating link in the completion email.
  await employee.page.goto('/');
  const card = employee.page.getByRole('list', { name: RATE_CARD });
  await expect(card).toBeVisible();
  await expectNoHorizontalScroll(employee.page);
  await card.getByRole('link', { name: new RegExp(subject) }).click();
  await expect(employee.page).toHaveURL(RATE_LINK);
  const rating = employee.page.getByRole('region', { name: RATING_SECTION });
  await expect(rating.getByRole('radio', { name: GIVEN_SCORE })).toBeVisible();
  await expectNoHorizontalScroll(employee.page);
  await rating.getByRole('radio', { name: GIVEN_SCORE }).check();
  await rating.getByRole('button', { name: 'Gönder' }).click();
  await expect(rating.getByRole('button', { name: 'Değiştir' })).toBeVisible();
  await expect(rating.getByText(GIVEN_SCORE_TEXT)).toBeVisible();

  await employee.page.goto('/');
  await expect(employee.page.getByRole('heading', { name: /^Merhaba/ })).toBeVisible();
  await expect(employee.page.getByRole('list', { name: RATE_CARD })).toHaveCount(0);

  await desk.page.reload();
  const deskRating = desk.page.getByRole('region', { name: RATING_SECTION });
  await expect(deskRating.getByText(GIVEN_SCORE_TEXT)).toBeVisible();
  await expect(deskRating.getByRole('radio')).toHaveCount(0);
  await expectNoHorizontalScroll(desk.page);

  await admin.page.goto('/reports/incidents');
  // The department is unique to this world, so the figure holds only the rating above.
  await filterByDepartment(admin.page, world.departmentName);
  const average = admin.page.getByRole('article', { name: AVERAGE_FIGURE, exact: true });
  await expect(average).toHaveAccessibleDescription(AVERAGE_TEXT);
  await expectNoHorizontalScroll(admin.page);
});
