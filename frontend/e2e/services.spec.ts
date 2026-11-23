import { expect, test } from '@playwright/test';

import { chooseOption, expectNoHorizontalScroll } from './helpers';
import { adminSession, seedIncidents, seedService, seedWorld, userSession } from './world';

const SERVICE_TEST_TIMEOUT_MS = 180_000;
const SERVICE_LABEL = 'Hizmet';
const CHOOSE_SERVICE = 'Hizmet seç';
const SAVE = 'Kaydet';
const ASSETS_SECTION = 'Varlıklar';
const OPEN_RECORDS_SECTION = 'Açık kayıtlar';
const META_SEPARATOR = ' · ';

test('service: admin links a device, IT sets it on an incident, the service shows the incident, the employee sees no service', async ({ browser }) => {
  test.setTimeout(SERVICE_TEST_TIMEOUT_MS);
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const employee = await userSession(browser, world.employeeEmail, world.employeePassword);
  const staff = await userSession(browser, world.itEmail, world.itPassword);

  // The run suffix keeps the service name unique across runs on the same database.
  const suffix = Date.now().toString();
  const serviceName = `SAP ${suffix}`;
  const subject = `SAP girişi açılmıyor ${suffix}`;
  const serviceId = await seedService(admin, serviceName, world.teamId, world.assetId);
  const [ticket = ''] = await seedIncidents(employee, [subject]);

  // IT staff set the service on the incident from its detail page.
  await staff.page.goto(`/tickets/${ticket}`);
  await staff.page.getByRole('button', { name: CHOOSE_SERVICE }).click();
  const dialog = staff.page.getByRole('dialog');
  await chooseOption(dialog, SERVICE_LABEL, serviceName);
  await dialog.getByRole('button', { name: SAVE }).click();
  await expect(dialog).toHaveCount(0);
  await expect(staff.page.getByText(serviceName).first()).toBeVisible();
  await expectNoHorizontalScroll(staff.page);

  // The service detail lists the linked device and the open incident.
  await staff.page.goto(`/services/${serviceId}`);
  await expect(staff.page.getByRole('heading', { name: serviceName })).toBeVisible();
  await expect(staff.page.getByRole('list', { name: ASSETS_SECTION }).getByText(world.assetTag)).toBeVisible();
  await expect(staff.page.getByRole('list', { name: OPEN_RECORDS_SECTION }).getByText(`${ticket}${META_SEPARATOR}${subject}`)).toBeVisible();
  await expectNoHorizontalScroll(staff.page);

  // The employee view of the same ticket has no service field.
  await employee.page.goto(`/tickets/${ticket}`);
  await expect(employee.page.getByText(subject).first()).toBeVisible();
  await expect(employee.page.getByText(SERVICE_LABEL, { exact: true })).toHaveCount(0);
  await expect(employee.page.getByText(serviceName)).toHaveCount(0);

  await Promise.all([admin.context.close(), employee.context.close(), staff.context.close()]);
});
