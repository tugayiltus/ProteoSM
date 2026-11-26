import { createHmac } from 'node:crypto';

import { expect, test } from '@playwright/test';

import { chooseOption, clickOption, expectNoHorizontalScroll } from './helpers';
import { adminSession, seedChange, seedWorld, userSession } from './world';

const MODULES_TEST_TIMEOUT_MS = 180_000;
const MS_PER_SECOND = 1000;

test('licence: IT assigns the only seat, the employee sees the software, a second seat is not offered', async ({ browser }) => {
  test.setTimeout(MODULES_TEST_TIMEOUT_MS);
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const staff = await userSession(browser, world.itEmail, world.itPassword);
  const employee = await userSession(browser, world.employeeEmail, world.employeePassword);
  const software = `Çizim ${Date.now().toString()}`;

  await staff.page.goto('/software');
  await staff.page.getByRole('button', { name: 'Yeni yazılım' }).click();
  let dialog = staff.page.getByRole('dialog');
  await dialog.getByRole('textbox', { name: /^Üretici/ }).fill('Örnek Yazılım');
  await dialog.getByRole('textbox', { name: /^Yazılım/ }).fill(software);
  await dialog.getByRole('button', { name: 'Oluştur' }).click();
  await expect(dialog).toHaveCount(0);

  await staff.page.goto('/licences');
  await staff.page.getByRole('button', { name: 'Yeni lisans' }).click();
  dialog = staff.page.getByRole('dialog');
  await dialog.getByRole('searchbox', { name: 'Yazılım ara' }).fill(software);
  await chooseOption(dialog, /^Yazılım(?! ara)/, `Örnek Yazılım ${software}`);
  await dialog.getByRole('textbox', { name: /^Lisans/ }).fill('Tek kişilik');
  await chooseOption(dialog, /^Sahip ekip/, world.teamName);
  await dialog.getByRole('button', { name: 'Oluştur' }).click();
  await expect(dialog).toHaveCount(0);

  await staff.page.goto(`/licences?q=${encodeURIComponent(software)}`);
  await staff.page.getByRole('link', { name: `Aç ${software} Tek kişilik` }).filter({ visible: true }).click();
  await staff.page.getByRole('button', { name: 'Ata', exact: true }).click();
  dialog = staff.page.getByRole('dialog');
  await dialog.getByRole('searchbox', { name: 'Kullanıcı ara' }).fill(world.employeeEmail);
  await chooseFirstUser(dialog);
  await dialog.getByRole('button', { name: 'Ata', exact: true }).click();
  await expect(dialog).toHaveCount(0);
  await expect(staff.page.getByText('1 / 1').first()).toBeVisible();
  await expect(staff.page.getByRole('button', { name: 'Ata', exact: true })).toHaveCount(0);
  await expectNoHorizontalScroll(staff.page);

  await employee.page.goto('/software/mine');
  await expect(employee.page.getByText(software)).toBeVisible();
  await expectNoHorizontalScroll(employee.page);
  await Promise.all([admin.context.close(), staff.context.close(), employee.context.close()]);

  async function chooseFirstUser(scope: typeof dialog) {
    await scope.getByRole('combobox', { name: 'Kullanıcı' }).click();
    await clickOption(staff.page.getByRole('option').first());
  }
});

test('release: a change joins, the release starts and frees the change when it is cancelled', async ({ browser }) => {
  test.setTimeout(MODULES_TEST_TIMEOUT_MS);
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const staff = await userSession(browser, world.itEmail, world.itPassword);
  const change = await seedChange(staff, world, 'Sürüm değişikliği', null);

  await staff.page.goto('/releases');
  await staff.page.getByRole('button', { name: 'Yeni sürüm' }).click();
  let dialog = staff.page.getByRole('dialog');
  await dialog.getByRole('textbox', { name: /^Başlık/ }).fill('Kasım sürümü');
  await dialog.getByRole('textbox', { name: /^Açıklama/ }).fill('Birlikte çıkanlar');
  await chooseOption(dialog, /^Sahip ekip/, world.teamName);
  await dialog.getByRole('button', { name: 'Oluştur' }).click();
  await expect(staff.page).toHaveURL(/\/releases\/REL-/);

  await staff.page.getByRole('button', { name: 'Değişiklik ekle' }).click();
  dialog = staff.page.getByRole('dialog');
  await dialog.getByLabel('Değişiklik numarası').fill(change);
  await dialog.getByRole('button', { name: 'Değişiklik ekle' }).click();
  await expect(staff.page.getByRole('link', { name: new RegExp(change) })).toBeVisible();
  await staff.page.getByRole('button', { name: 'Başlat' }).click();
  await expect(staff.page.getByText('Sürüyor').first()).toBeVisible();
  await expectNoHorizontalScroll(staff.page);

  await staff.page.getByRole('button', { name: 'Sürümü iptal et' }).click();
  dialog = staff.page.getByRole('dialog');
  await dialog.getByLabel('İptal nedeni').fill('Ertelendi');
  await dialog.getByRole('button', { name: 'Sürümü iptal et' }).click();
  await expect(staff.page.getByText('Sürümde değişiklik yok.')).toBeVisible();
  await Promise.all([admin.context.close(), staff.context.close()]);
});

test('monitoring: a signed event opens an incident, a wrong signature does not', async ({ browser, playwright, baseURL }) => {
  test.setTimeout(MODULES_TEST_TIMEOUT_MS);
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const staff = await userSession(browser, world.itEmail, world.itPassword);
  const name = `İzleme ${Date.now().toString()}`;

  await admin.page.goto('/admin/monitoring');
  await admin.page.getByRole('button', { name: 'Yeni kaynak' }).click();
  let dialog = admin.page.getByRole('dialog');
  await dialog.getByRole('textbox', { name: /^Ad/ }).fill(name);
  await chooseOption(dialog, /^Ekip/, world.teamName);
  await dialog.getByRole('combobox', { name: /^Talep sahibi/ }).click();
  await clickOption(admin.page.getByRole('option', { name: new RegExp(world.itEmail) }));
  await dialog.getByRole('button', { name: 'Oluştur' }).click();
  dialog = admin.page.getByRole('dialog', { name: `${name} anahtarı` });
  const secret = (await dialog.getByText(/^[0-9a-f]{64}$/).textContent()) ?? '';
  await dialog.getByRole('button', { name: 'Kapat' }).last().click();
  await expect(admin.page.getByText(secret)).toHaveCount(0);
  await expectNoHorizontalScroll(admin.page);

  const sources = (await (await admin.context.request.get('/api/v1/admin/monitoring-sources')).json()) as { id: string; name: string }[];
  const path = `/api/v1/monitoring/${sources.find((s) => s.name === name)?.id ?? ''}/events`;
  // A context without cookies, as a monitoring tool calls it.
  const tool = await playwright.request.newContext({ baseURL, ignoreHTTPSErrors: true });
  const body = JSON.stringify({ eventId: name, alertKey: 'disk', state: 'firing', title: `Disk doldu ${name}`, description: 'sda1' });
  const timestamp = Math.floor(Date.now() / MS_PER_SECOND).toString();
  const signature = createHmac('sha256', secret).update(`${timestamp}.${body}`).digest('hex');
  const headers = { 'Content-Type': 'application/json', 'X-Proteone-Timestamp': timestamp };
  const refused = await tool.post(path, { data: body, headers: { ...headers, 'X-Proteone-Signature': signature.replace(/^./, (c) => (c === '0' ? '1' : '0')) } });
  expect(refused.status()).toBe(401);
  const accepted = await tool.post(path, { data: body, headers: { ...headers, 'X-Proteone-Signature': signature } });
  expect(accepted.status()).toBe(200);
  await tool.dispose();

  const found = (await (await staff.context.request.get(`/api/v1/search?q=${encodeURIComponent(`Disk doldu ${name}`)}`)).json()) as { groups: { type: string; hits: { key: string }[] }[] };
  await staff.page.goto(`/tickets/${found.groups.find((group) => group.type === 'ticket')?.hits[0]?.key ?? ''}`);
  await expect(staff.page.getByText(`Disk doldu ${name}`).first()).toBeVisible();
  await expect(staff.page.getByText(name, { exact: true })).toBeVisible();
  await Promise.all([admin.context.close(), staff.context.close()]);
});
