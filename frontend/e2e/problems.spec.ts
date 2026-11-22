import { expect, test } from '@playwright/test';

import { chooseOption, expectNoHorizontalScroll } from './helpers';
import { adminSession, seedChange, seedIncidents, seedSecondTeam, seedWorld, transferTicket, userSession } from './world';

const PROBLEM_TEST_TIMEOUT_MS = 240_000;
const PROBLEM_URL = /\/problems\/PRB-\d{4}-\d{6}$/;
const ROOT_CAUSE = 'Eski VPN istemcisi sertifikayı yenilemiyor';
const WORKAROUND = 'VPN istemcisini yeniden başlatın';
const RESOLUTION = 'İstemci yeni sürüme güncellendi';

test('problem: desk opens it from an incident, field team works it to resolved, a second incident sees the workaround', async ({ browser }) => {
  test.setTimeout(PROBLEM_TEST_TIMEOUT_MS);
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const desk = await seedSecondTeam(admin);
  const employee = await userSession(browser, world.employeeEmail, world.employeePassword);
  const field = await userSession(browser, world.itEmail, world.itPassword);
  const deskStaff = await userSession(browser, desk.email, desk.password);

  const suffix = Date.now().toString();
  const problemTitle = `VPN kopuyor ${suffix}`;
  const [first = '', second = ''] = await seedIncidents(employee, [`VPN bağlantısı kopuyor ${suffix}`, `VPN yine kopuyor ${suffix}`]);
  await transferTicket(field, first, desk.teamId);
  await transferTicket(field, second, desk.teamId);

  // Desk opens a problem from the first incident, then hands it to the field team.
  const deskPage = deskStaff.page;
  await deskPage.goto(`/tickets/${first}`);
  await deskPage.getByRole('button', { name: 'Problem oluştur' }).click();
  const create = deskPage.getByRole('dialog');
  await create.getByLabel('Başlık').fill(problemTitle);
  await chooseOption(create, 'Sahip ekip', desk.teamName);
  await create.getByRole('button', { name: 'Oluştur' }).click();
  await expect(create).toBeHidden();
  const problemLink = deskPage.getByRole('link', { name: new RegExp(problemTitle) });
  await expect(problemLink).toBeVisible();
  await expectNoHorizontalScroll(deskPage);
  await problemLink.click();
  await expect(deskPage).toHaveURL(PROBLEM_URL);
  const problemPath = new URL(deskPage.url()).pathname;
  await deskPage.getByRole('button', { name: 'Aktar' }).click();
  const transfer = deskPage.getByRole('dialog');
  await chooseOption(transfer, 'Yeni ekip', world.teamName);
  await transfer.getByLabel('Neden').fill('Saha ekibi inceleyecek');
  await transfer.getByRole('button', { name: 'Onayla' }).click();
  await expect(transfer).toBeHidden();

  // Field team fills root cause and workaround, marks the known error and creates the article.
  const fieldPage = field.page;
  await fieldPage.goto(problemPath);
  await expect(fieldPage.getByRole('heading', { name: new RegExp(problemTitle) }).first()).toBeVisible();
  await fieldPage.getByRole('button', { name: 'Düzenle' }).click();
  const edit = fieldPage.getByRole('dialog');
  await edit.getByLabel('Kök neden').fill(ROOT_CAUSE);
  await edit.getByLabel('Geçici çözüm').fill(WORKAROUND);
  await edit.getByRole('button', { name: 'Kaydet' }).click();
  await expect(edit).toBeHidden();
  await fieldPage.getByRole('button', { name: 'Bilinen hata olarak işaretle' }).click();
  await expect(fieldPage.getByText('Bilinen hata', { exact: true }).first()).toBeVisible();
  await fieldPage.getByRole('button', { name: 'Makale oluştur' }).click();
  await expect(fieldPage.getByRole('link', { name: new RegExp(`Bilinen hata: ${problemTitle}`) })).toBeVisible();
  await expect(fieldPage.getByRole('button', { name: 'Makale oluştur' })).toHaveCount(0);
  await expectNoHorizontalScroll(fieldPage);

  // Field team links a fixing change, then resolves the problem.
  const change = await seedChange(field, world, `VPN istemcisi güncelleme ${suffix}`, null, { affectedService: `VPN ${suffix}` });
  await fieldPage.getByRole('button', { name: 'Değişiklik bağla' }).click();
  const link = fieldPage.getByRole('dialog');
  await link.getByLabel('Değişiklik numarası').fill(change);
  await link.getByRole('button', { name: 'Bağla' }).click();
  await expect(fieldPage.getByText(new RegExp(change))).toBeVisible();
  await fieldPage.getByRole('button', { name: 'Çözüldü olarak kapat' }).click();
  const resolve = fieldPage.getByRole('dialog');
  await resolve.getByLabel('Kalıcı çözüm').fill(RESOLUTION);
  await resolve.getByRole('button', { name: 'Onayla' }).click();
  await expect(fieldPage.getByText('Çözüldü', { exact: true }).first()).toBeVisible();
  await expectNoHorizontalScroll(fieldPage);

  // Desk links a second incident and sees the workaround on it.
  await deskPage.goto(`/tickets/${second}`);
  await deskPage.getByRole('button', { name: 'Probleme bağla' }).click();
  const bind = deskPage.getByRole('dialog');
  await bind.getByLabel('Problem numarası').fill(problemPath.split('/').pop() ?? '');
  await bind.getByRole('button', { name: 'Bağla' }).click();
  await expect(deskPage.getByText(WORKAROUND)).toBeVisible();
  await expectNoHorizontalScroll(deskPage);

  // The employee who opened the incident sees no problem card.
  await employee.page.goto(`/tickets/${second}`);
  await expect(employee.page.getByText(`VPN yine kopuyor ${suffix}`).first()).toBeVisible();
  await expect(employee.page.getByRole('heading', { name: 'Problem', exact: true })).toHaveCount(0);
  await expect(employee.page.getByText(WORKAROUND)).toHaveCount(0);

  await Promise.all([admin.context.close(), employee.context.close(), field.context.close(), deskStaff.context.close()]);
});
