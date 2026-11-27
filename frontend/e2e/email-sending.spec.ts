import { expect, test, type Page } from '@playwright/test';

import { expectNoHorizontalScroll, requiredEnv } from './helpers';
import { adminSession, waitForEmail } from './world';

const SENDER = 'no-reply@firma.com';
const RELAY_HOST = 'smtp.firma.local';
/** A phrase from the fixed test mail body (`EmailTestMessageText`). */
const TEST_MAIL_PHRASE = 'giden posta ayarlarını denemek';
const ADMIN_MENU = 'Yönetim menüsü';
const MENU_PANEL = 'Menü';
const SETTINGS_API = '/api/v1/admin/email-settings';

/** Pickup keeps the status line on "Dosyaya yazma", so the stored switch is read from the API. */
async function isPanelActive(page: Page): Promise<boolean> {
  const response = await page.request.get(SETTINGS_API);
  expect(response.ok()).toBe(true);
  const body = (await response.json()) as { isActive: boolean };
  return body.isActive;
}

/** The admin menu: always visible from 768 px, behind the menu button on phones. */
async function openMenu(page: Page) {
  const button = page.getByRole('button', { name: 'Menüyü aç' });
  const menu = page.getByRole('navigation', { name: ADMIN_MENU });
  // isVisible does not wait: wait until the shell shows the menu button or the menu before choosing a branch.
  await expect(button.or(menu).first()).toBeVisible();
  if (await button.isVisible()) {
    await button.click();
    return page.getByRole('dialog', { name: MENU_PANEL });
  }
  return menu;
}

test('admin_tests_and_activates_the_panel_sender_and_returns_to_env', async ({ browser }) => {
  const admin = await adminSession(browser);
  const page = admin.page;
  const adminEmail = requiredEnv('E2E_ADMIN_EMAIL');

  await page.goto('/admin');
  const menu = await openMenu(page);
  await menu.getByRole('group', { name: 'Sistem ve denetim' }).getByRole('link', { name: 'E-posta gönderimi' }).click();
  await expect(page).toHaveURL(/\/admin\/email-sending$/);

  const status = page.getByRole('region', { name: 'Gönderim durumu' });
  const form = page.getByRole('region', { name: 'Panel ayarları' });
  await expect(status.getByText('Bu kurulum e-postaları dosyaya yazar (Pickup).', { exact: false })).toBeVisible();
  await expect(status.getByText('Gönderim şu an: Dosyaya yazma (Pickup)')).toBeVisible();

  await form.getByRole('radio', { name: 'Diğer SMTP' }).check();
  await form.getByLabel(/^Gönderen adres/).fill(SENDER);
  await form.getByLabel(/^Sunucu/).fill(RELAY_HOST);
  await form.getByLabel(/^Kullanıcı/).fill('');
  await form.getByRole('radio', { name: 'Yok' }).check();
  await form.getByRole('button', { name: 'Kaydet' }).click();
  await expect(page.getByText('E-posta gönderim ayarları kaydedildi.').first()).toBeVisible();

  const activate = form.getByRole('button', { name: 'Kullanıma al' });
  await expect(activate).toHaveAttribute('aria-disabled', 'true');
  await expectNoHorizontalScroll(page);

  await form.getByRole('button', { name: 'Test maili gönder' }).click();
  await expect(form.getByText('Test maili gönderildi.')).toBeVisible();
  const mail = await waitForEmail(adminEmail, TEST_MAIL_PHRASE);
  expect(mail.from).toBe(SENDER);

  // Pickup keeps the status line on "Dosyaya yazma": the switch shows in the buttons that follow the panel state.
  await expect(activate).not.toHaveAttribute('aria-disabled', 'true');
  await activate.click();
  const useEnvironment = status.getByRole('button', { name: '.env ayarlarına dön' });
  await expect(useEnvironment).toBeVisible();
  await expect(activate).toHaveCount(0);
  expect(await isPanelActive(page)).toBe(true);
  await expectNoHorizontalScroll(page);

  await useEnvironment.click();
  const dialog = page.getByRole('dialog', { name: '.env ayarlarına dön' });
  await expect(dialog.getByText('Panel ayarları silinmez.', { exact: false })).toBeVisible();
  await dialog.getByRole('button', { name: 'Onayla' }).click();
  await expect(dialog).toHaveCount(0);
  await expect(useEnvironment).toHaveCount(0);
  await expect(activate).toBeVisible();
  await expect(form.getByLabel(/^Gönderen adres/)).toHaveValue(SENDER);
  expect(await isPanelActive(page)).toBe(false);
  await expectNoHorizontalScroll(page);
});
