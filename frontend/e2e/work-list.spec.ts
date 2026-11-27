import { expect, test } from '@playwright/test';

import { adminSession, makeTicketSlaWarnFast, seedIncidents, seedWorld, userSession } from './world';

const WORK_LIST_TEST_TIMEOUT_MS = 300_000;
const RISK_WAIT_MS = 180_000;
const RISK_POLL_INTERVAL_MS = 5_000;
const SUBJECT_PREFIX = 'VPN bağlantısı kurulamıyor';
const SLA_LINE = /SLA'sı risk altında · Son tarih /;
const TICKET_URL = /\/tickets\/INC-\d{4}-\d{6}$/;
const RISK_VIEW_TITLE = 'SLA riski olan arıza ve talepler';

test('a technician finds the risky ticket in the SLA risk view, sees its due time, replies, and returns to the same view', async ({ browser }) => {
  test.setTimeout(WORK_LIST_TEST_TIMEOUT_MS);
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const restoreSla = await makeTicketSlaWarnFast(admin);
  try {
    const employee = await userSession(browser, world.employeeEmail, world.employeePassword);
    const it = await userSession(browser, world.itEmail, world.itPassword);
    const subject = `${SUBJECT_PREFIX} ${Date.now()}`;
    await seedIncidents(employee, [subject]);

    // The view keeps the search, the status filter and the page in the URL; the same URL must come back from the detail.
    const viewUrl = `/workspace/queue?queue=sla_risk&status=open&q=${encodeURIComponent(subject)}&page=1`;
    const page = it.page;
    const counter = page.getByRole('link', { name: /^SLA riski/ });
    await page.goto('/workspace');
    await expect(counter).toBeVisible();
    const row = page.getByRole('link', { name: /Aç.*INC-/ });
    await expect(async () => {
      await page.goto(viewUrl);
      await expect(page.getByRole('heading', { name: RISK_VIEW_TITLE })).toBeVisible();
      await expect(page.getByText(subject).first()).toBeVisible({ timeout: RISK_POLL_INTERVAL_MS });
    }).toPass({ timeout: RISK_WAIT_MS, intervals: [RISK_POLL_INTERVAL_MS] });
    await expect(page.locator('[data-sla-state="warned"]').first()).toBeVisible();
    const listUrl = page.url();

    await row.first().click();
    await expect(page).toHaveURL(TICKET_URL);
    const heading = page.getByRole('heading', { level: 1, name: subject });
    await expect(heading).toBeVisible();
    await expect(page.getByText(SLA_LINE)).toBeVisible();
    const claim = page.getByRole('button', { name: 'Üzerime al' });
    await expect(claim).toBeVisible();
    // The first screen holds the subject, the due date and the main action, also on a phone.
    await expect(heading).toBeInViewport();
    await expect(page.getByText(SLA_LINE)).toBeInViewport();
    await expect(claim).toBeInViewport();

    await claim.click();
    const reply = page.getByRole('button', { name: 'Yanıtla' });
    await expect(reply).toBeVisible();
    await expect(reply).toBeInViewport();
    await page.getByRole('textbox', { name: 'Mesajınız' }).fill('Taslak yanıt');
    await reply.click();
    const field = page.getByRole('textbox', { name: 'Mesajınız' });
    await expect(field).toBeFocused();
    await expect(field).toHaveValue('Taslak yanıt');

    await page.getByRole('link', { name: 'Kayıtlara dön' }).click();
    await expect(page).toHaveURL(listUrl);
    await expect(page.getByRole('searchbox', { name: 'Numara veya konu ara' })).toHaveValue(subject);
    await expect(page.getByText(subject).first()).toBeVisible();
    await Promise.all([employee.context.close(), it.context.close()]);
  } finally {
    await restoreSla();
    await admin.context.close();
  }
});
