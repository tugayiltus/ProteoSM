import { expect, test } from '@playwright/test';

import { chooseOption, expectNoHorizontalScroll } from './helpers';
import { adminSession, seedIncidents, seedSecondTeam, seedWorld, transferTicket, userSession, waitForEmail } from './world';

const PROBLEM_SLA_TEST_TIMEOUT_MS = 240_000;
const PROBLEM_URL = /\/problems\/PRB-\d{4}-\d{6}$/;
const TARGETS_SAVED = 'Problem hedefleri kaydedildi.';
const HIGH_TARGET_MINUTES = '480';
const OTHER_TARGET_MINUTES = '960';
const WARNING_PERCENT = '80';
const PROBLEM_NUMBER_PREFIX = 'PRB-';
const ROOT_CAUSE = 'Eski VPN istemcisi sertifikayı yenilemiyor';
const WORKAROUND = 'VPN istemcisini yeniden başlatın';

test('problem SLA: admin sets the High target, the badge runs and is met at known error, team and requester mails arrive', async ({ browser }) => {
  test.setTimeout(PROBLEM_SLA_TEST_TIMEOUT_MS);
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const desk = await seedSecondTeam(admin);
  const employee = await userSession(browser, world.employeeEmail, world.employeePassword);
  const field = await userSession(browser, world.itEmail, world.itPassword);
  const deskStaff = await userSession(browser, desk.email, desk.password);

  // Admin sets the targets in the "Problem hedefleri" section.
  const adminPage = admin.page;
  await adminPage.goto('/admin/sla/policy');
  await expect(adminPage.getByText('Problem hedefleri (iş dakikası)')).toBeVisible();
  const targets = adminPage.locator('form').filter({ hasText: 'Problem hedefleri (iş dakikası)' });
  await targets.getByRole('spinbutton', { name: 'Yüksek Hedef (dk)' }).fill(HIGH_TARGET_MINUTES);
  await targets.getByRole('spinbutton', { name: 'Yüksek Uyarı (%)' }).fill(WARNING_PERCENT);
  await targets.getByRole('spinbutton', { name: 'Orta Hedef (dk)' }).fill(OTHER_TARGET_MINUTES);
  await targets.getByRole('spinbutton', { name: 'Orta Uyarı (%)' }).fill(WARNING_PERCENT);
  await targets.getByRole('spinbutton', { name: 'Düşük Hedef (dk)' }).fill(OTHER_TARGET_MINUTES);
  await targets.getByRole('spinbutton', { name: 'Düşük Uyarı (%)' }).fill(WARNING_PERCENT);
  await targets.getByRole('button', { name: 'Kaydet', exact: true }).click();
  await expect(adminPage.getByRole('status').filter({ hasText: TARGETS_SAVED })).toBeAttached();
  await expectNoHorizontalScroll(adminPage);

  const suffix = Date.now().toString();
  const problemTitle = `VPN kopuyor ${suffix}`;
  const [incident = ''] = await seedIncidents(employee, [`VPN bağlantısı kopuyor ${suffix}`]);
  await transferTicket(field, incident, desk.teamId);

  // Desk opens a High problem from the incident, hands it to the field team: the badge runs and the field team gets the mail.
  const deskPage = deskStaff.page;
  await deskPage.goto(`/tickets/${incident}`);
  await deskPage.getByRole('button', { name: 'Problem oluştur' }).click();
  const create = deskPage.getByRole('dialog');
  await create.getByLabel('Başlık').fill(problemTitle);
  await chooseOption(create, 'Etki', 'Yüksek');
  await chooseOption(create, 'Sahip ekip', desk.teamName);
  await create.getByRole('button', { name: 'Oluştur' }).click();
  await expect(create).toBeHidden();
  await deskPage.getByRole('link', { name: new RegExp(problemTitle) }).click();
  await expect(deskPage).toHaveURL(PROBLEM_URL);
  const problemPath = new URL(deskPage.url()).pathname;
  await expect(deskPage.locator('[data-sla-state="running"]').first()).toBeVisible();

  await deskPage.getByRole('button', { name: 'Aktar' }).click();
  const transfer = deskPage.getByRole('dialog');
  await chooseOption(transfer, 'Yeni ekip', world.teamName);
  await transfer.getByLabel('Neden').fill('Saha ekibi inceleyecek');
  await transfer.getByRole('button', { name: 'Onayla' }).click();
  await expect(transfer).toBeHidden();
  await expect(deskPage.locator('[data-sla-state="running"]').first()).toBeVisible();

  const assigned = await waitForEmail(world.itEmail, problemTitle);
  expect(assigned.subject).toContain(PROBLEM_NUMBER_PREFIX);

  // Field team marks the known error: the badge is met.
  const fieldPage = field.page;
  await fieldPage.goto(problemPath);
  await expect(fieldPage.locator('[data-sla-state="running"]').first()).toBeVisible();
  await fieldPage.getByRole('button', { name: 'Düzenle' }).click();
  const edit = fieldPage.getByRole('dialog');
  await edit.getByLabel('Kök neden').fill(ROOT_CAUSE);
  await edit.getByLabel('Geçici çözüm').fill(WORKAROUND);
  await edit.getByRole('button', { name: 'Kaydet' }).click();
  await expect(edit).toBeHidden();
  await fieldPage.getByRole('button', { name: 'Bilinen hata olarak işaretle' }).click();
  await expect(fieldPage.locator('[data-sla-state="met"]').first()).toBeVisible();
  await expect(fieldPage.getByText('Karşılandı').first()).toBeVisible();
  await expectNoHorizontalScroll(fieldPage);

  // The desk member of the linked incident gets the team mail; the requester gets the notice without problem data.
  const team = await waitForEmail(desk.email, problemTitle);
  expect(team.subject).toContain(PROBLEM_NUMBER_PREFIX);
  const requester = await waitForEmail(world.employeeEmail, incident);
  expect(requester.subject).toContain(incident);
  expect(requester.subject).not.toContain(PROBLEM_NUMBER_PREFIX);
  expect(requester.body).not.toContain(PROBLEM_NUMBER_PREFIX);
  expect(requester.body).not.toContain(problemTitle);

  await Promise.all([admin.context.close(), employee.context.close(), field.context.close(), deskStaff.context.close()]);
});
