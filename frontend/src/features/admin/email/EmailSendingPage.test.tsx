import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { formatDateTime } from '../../../shared/lib/format';
import { ADMIN_ME, mockApi } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';

const PATH = '/api/v1/admin/email-settings';
const PAGE = '/admin/email-sending';
const TESTED_AT = '2026-11-20T09:30:00Z';

const SETTINGS = {
  provider: 'smtp',
  senderName: 'BT Destek',
  senderAddress: 'destek@sirket.com',
  host: 'mail.sirket.com',
  port: 587,
  security: 'start_tls',
  userName: 'destek@sirket.com',
  hasPassword: true,
  isTested: false,
  isActive: false,
  lastTestedAt: null,
  lastTestSucceeded: null,
  lastTestErrorCode: null,
  isPickup: false,
  environmentFrom: 'noreply@sirket.com',
  environmentHost: 'relay.sirket.local',
  version: 4,
};

const ACTIVE = { ...SETTINGS, isTested: true, isActive: true, lastTestedAt: TESTED_AT, lastTestSucceeded: true };

const routes = (settings: Record<string, unknown> = SETTINGS, extra: Record<string, unknown> = {}) => ({
  'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
  [`GET ${PATH}`]: { status: 200, body: settings },
  ...extra,
});

async function openPage(settings: Record<string, unknown> = SETTINGS, extra: Record<string, unknown> = {}) {
  const api = mockApi(routes(settings, extra) as never);
  const rendered = renderRoute(PAGE);
  const form = await screen.findByRole('region', { name: 'Panel ayarları' });
  return { ...rendered, api, form };
}

const bodiesOf = async (calls: Request[], method: string, url: string) =>
  Promise.all(calls.filter((c) => c.method === method && c.url.endsWith(url)).map(async (c) => (await c.clone().json()) as Record<string, unknown>));

describe('EmailSendingPage', () => {
  it('shows_panel_status_with_last_test_and_env_button', async () => {
    await openPage(ACTIVE);
    expect(screen.getByText(`Gönderim şu an: Panel ayarları (son test ${formatDateTime(TESTED_AT)}, başarılı)`)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '.env ayarlarına dön' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Kullanıma al' })).not.toBeInTheDocument();
  });

  it('shows the environment sender while the panel settings are not in use', async () => {
    await openPage();
    expect(screen.getByText('Gönderim şu an: .env ayarları (noreply@sirket.com, relay.sirket.local)')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '.env ayarlarına dön' })).not.toBeInTheDocument();
  });

  it('google_shows_preset_server_read_only', async () => {
    const { user, form } = await openPage();
    await user.click(within(form).getByRole('radio', { name: 'Google' }));
    expect(within(form).getByLabelText(/^Sunucu/)).toHaveValue('smtp.gmail.com');
    // A disabled field with a reason stays focusable, so Astryx marks it with aria-disabled.
    expect(within(form).getByLabelText(/^Sunucu/)).toHaveAttribute('aria-disabled', 'true');
    expect(within(form).getByLabelText(/^Port/)).toHaveValue('587');
    expect(within(form).getByLabelText(/^Port/)).toHaveAttribute('aria-disabled', 'true');
    expect(within(form).getByRole('radio', { name: 'STARTTLS' })).toBeChecked();
    expect(within(form).getByRole('radiogroup', { name: /^Güvenlik/ })).toHaveAttribute('aria-disabled', 'true');
    await user.click(within(form).getByRole('radio', { name: 'SSL/TLS' }));
    expect(within(form).getByRole('radio', { name: 'STARTTLS' })).toBeChecked();
    expect(within(form).getByText('Google hesabında iki adımlı doğrulama açık olmalı; Şifre alanına uygulama şifresini yazın.')).toBeInTheDocument();
  });

  it('other_smtp_security_change_suggests_its_port', async () => {
    const { user, form } = await openPage({ ...SETTINGS, userName: null, hasPassword: false });
    await user.click(within(form).getByRole('radio', { name: 'SSL/TLS' }));
    expect(within(form).getByLabelText(/^Port/)).toHaveValue('465');
    await user.click(within(form).getByRole('radio', { name: 'Yok' }));
    expect(within(form).getByLabelText(/^Port/)).toHaveValue('25');
  });

  it('allows Yok only without a user name and says why', async () => {
    const { form } = await openPage();
    expect(within(form).getByRole('radio', { name: /^Yok/ })).toBeDisabled();
    expect(within(form).getByText(/Kullanıcı adı varken seçilemez/)).toBeInTheDocument();
  });

  it('refuses a user name over Yok before it sends anything', async () => {
    const { user, api, form } = await openPage({ ...SETTINGS, userName: null, hasPassword: false });
    await user.click(within(form).getByRole('radio', { name: 'Yok' }));
    await user.type(within(form).getByLabelText(/^Kullanıcı/), 'destek@sirket.com');
    await user.click(within(form).getByRole('button', { name: 'Kaydet' }));
    expect(await within(form).findByText(/^Kullanıcı adıyla giriş için güvenli bağlantı gerekir/)).toBeInTheDocument();
    expect(api.calls.some((c) => c.method === 'PUT')).toBe(false);
  });

  it('saves Google with its preset server and the typed app password', async () => {
    const { user, api, form } = await openPage(SETTINGS, { [`PUT ${PATH}`]: { status: 200, body: SETTINGS } });
    await user.click(within(form).getByRole('radio', { name: 'Google' }));
    await user.type(within(form).getByLabelText(/^Şifre/), 'abcd efgh ijkl mnop');
    await user.click(within(form).getByRole('button', { name: 'Kaydet' }));
    await waitFor(async () => expect(await bodiesOf(api.calls, 'PUT', PATH)).toHaveLength(1));
    const [body] = await bodiesOf(api.calls, 'PUT', PATH);
    expect(body).toMatchObject({ provider: 'google', host: 'smtp.gmail.com', port: 587, security: 'start_tls', userName: 'destek@sirket.com', password: 'abcd efgh ijkl mnop' });
  });

  it('password_is_empty_with_kayitli_placeholder_and_saves_without_it', async () => {
    const { user, api, form } = await openPage(SETTINGS, { [`PUT ${PATH}`]: { status: 200, body: SETTINGS } });
    const password = within(form).getByLabelText(/^Şifre/);
    expect(password).toHaveValue('');
    expect(password).toHaveAttribute('placeholder', 'kayıtlı — değiştirmek için yazın');
    await user.clear(within(form).getByLabelText(/^Gönderen adı/));
    await user.type(within(form).getByLabelText(/^Gönderen adı/), 'Yardım Masası');
    await user.click(within(form).getByRole('button', { name: 'Kaydet' }));
    await waitFor(async () => expect(await bodiesOf(api.calls, 'PUT', PATH)).toHaveLength(1));
    const [body] = await bodiesOf(api.calls, 'PUT', PATH);
    expect(body).toMatchObject({ senderName: 'Yardım Masası', host: 'mail.sirket.com', port: 587, security: 'start_tls', version: 4 });
    expect(body?.password ?? null).toBeNull();
  });

  it('puts the missing password error on the password field', async () => {
    const { user, form } = await openPage(SETTINGS, {
      [`PUT ${PATH}`]: { status: 400, body: { code: 'validation.failed', errors: { password: ['email_settings.password_missing'] } } },
    });
    await user.clear(within(form).getByLabelText(/^Sunucu/));
    await user.type(within(form).getByLabelText(/^Sunucu/), 'smtp.yeni.com');
    await user.click(within(form).getByRole('button', { name: 'Kaydet' }));
    expect(await within(form).findByText(/^Şifreyi yazın\./)).toBeInTheDocument();
  });

  it('keeps the input and the warning after a conflict', async () => {
    const { user, api, form } = await openPage(SETTINGS, { [`PUT ${PATH}`]: { status: 409, body: { code: 'concurrency.conflict' } } });
    await user.clear(within(form).getByLabelText(/^Gönderen adı/));
    await user.type(within(form).getByLabelText(/^Gönderen adı/), 'Yardım Masası');
    await user.click(within(form).getByRole('button', { name: 'Kaydet' }));
    await waitFor(() => expect(api.calls.filter((c) => c.method === 'GET' && c.url.endsWith(PATH)).length).toBeGreaterThan(1));
    expect(await screen.findByText(/başka biri tarafından değiştirildi/)).toBeInTheDocument();
    expect(within(form).getByLabelText(/^Gönderen adı/)).toHaveValue('Yardım Masası');
  });

  it('keeps the typed values when a save of active settings fails its test send', async () => {
    const { user, form } = await openPage(ACTIVE, { [`PUT ${PATH}`]: { status: 409, body: { code: 'email_settings.auth_failed' } } });
    await user.clear(within(form).getByLabelText(/^Sunucu/));
    await user.type(within(form).getByLabelText(/^Sunucu/), 'smtp.yeni.com');
    await user.type(within(form).getByLabelText(/^Şifre/), 'yanlis-sifre');
    await user.click(within(form).getByRole('button', { name: 'Kaydet' }));
    const text = await within(form).findByText('Sunucu kullanıcı adını veya şifreyi kabul etmedi. Google için uygulama şifresi kullanın.');
    expect(text.closest('[role="alert"]')).not.toBeNull();
    expect(within(form).getByLabelText(/^Sunucu/)).toHaveValue('smtp.yeni.com');
    expect(within(form).getByLabelText(/^Şifre/)).toHaveValue('yanlis-sifre');
  });

  it('clears the test result when the form changes again', async () => {
    const { user, form } = await openPage(SETTINGS, {
      [`POST ${PATH}/test`]: { status: 200, body: { ok: false, errorCode: 'email_settings.timeout', detail: null } },
    });
    await user.click(within(form).getByRole('button', { name: 'Test maili gönder' }));
    const message = 'Sunucu zamanında yanıt vermedi. Daha sonra tekrar deneyin.';
    expect(await within(form).findByText(message)).toBeInTheDocument();
    await user.type(within(form).getByLabelText(/^Gönderen adı/), ' 2');
    expect(within(form).queryByText(message)).not.toBeInTheDocument();
  });

  it('clears the activate error on an edit and after a later activate', async () => {
    const tested = { ...SETTINGS, isTested: true, lastTestedAt: TESTED_AT, lastTestSucceeded: true };
    const { user, api, form } = await openPage(tested, { [`POST ${PATH}/activate`]: { status: 409, body: { code: 'email_settings.not_tested' } } });
    const message = /^Bu ayarlar henüz test edilmedi/;
    await user.click(within(form).getByRole('button', { name: 'Kullanıma al' }));
    expect(await within(form).findByText(message)).toBeInTheDocument();
    await user.type(within(form).getByLabelText(/^Gönderen adı/), ' 2');
    expect(within(form).queryByText(message)).not.toBeInTheDocument();
    await user.type(within(form).getByLabelText(/^Gönderen adı/), '{Backspace}{Backspace}');

    await user.click(within(form).getByRole('button', { name: 'Kullanıma al' }));
    expect(await within(form).findByText(message)).toBeInTheDocument();
    api.set(`POST ${PATH}/activate`, { status: 200, body: { ...tested, isActive: true, version: 5 } });
    await user.click(within(form).getByRole('button', { name: 'Kullanıma al' }));
    await waitFor(() => expect(within(form).queryByText(message)).not.toBeInTheDocument());
  });

  it('test_button_is_disabled_while_the_form_has_unsaved_changes', async () => {
    const { user, form } = await openPage();
    expect(within(form).getByRole('button', { name: 'Test maili gönder' })).not.toHaveAttribute('aria-disabled', 'true');
    await user.type(within(form).getByLabelText(/^Gönderen adı/), ' 2');
    expect(within(form).getByRole('button', { name: 'Test maili gönder' })).toHaveAttribute('aria-disabled', 'true');
  });

  it('failed_test_shows_code_text_and_server_detail', async () => {
    const { user, form } = await openPage(SETTINGS, {
      [`POST ${PATH}/test`]: { status: 200, body: { ok: false, errorCode: 'email_settings.auth_failed', detail: '535 5.7.8 Username and Password not accepted' } },
    });
    await user.click(within(form).getByRole('button', { name: 'Test maili gönder' }));
    expect(await within(form).findByText('Sunucu kullanıcı adını veya şifreyi kabul etmedi. Google için uygulama şifresi kullanın.')).toBeInTheDocument();
    expect(within(form).getByText(/535 5\.7\.8 Username and Password not accepted/)).toBeInTheDocument();
  });

  it('kullanima_al_is_disabled_until_tested', async () => {
    const { form } = await openPage();
    expect(within(form).getByRole('button', { name: 'Kullanıma al' })).toHaveAttribute('aria-disabled', 'true');
  });

  it('kullanima_al_posts_activate_with_version', async () => {
    const tested = { ...SETTINGS, isTested: true, lastTestedAt: TESTED_AT, lastTestSucceeded: true };
    const { user, api, form } = await openPage(tested, { [`POST ${PATH}/activate`]: { status: 200, body: { ...tested, isActive: true, version: 5 } } });
    await user.click(within(form).getByRole('button', { name: 'Kullanıma al' }));
    await waitFor(async () => expect(await bodiesOf(api.calls, 'POST', `${PATH}/activate`)).toEqual([{ version: 4 }]));
    expect(await screen.findByText(/^Gönderim şu an: Panel ayarları/)).toBeInTheDocument();
  });

  it('env_button_confirms_then_posts_use_environment', async () => {
    const { user, api } = await openPage(ACTIVE, { [`POST ${PATH}/use-environment`]: { status: 200, body: { ...ACTIVE, isActive: false, version: 5 } } });
    await user.click(screen.getByRole('button', { name: '.env ayarlarına dön' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText('Gönderim .env ayarlarına dönsün mü? Panel ayarları silinmez.')).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));
    await waitFor(async () => expect(await bodiesOf(api.calls, 'POST', `${PATH}/use-environment`)).toEqual([{ version: 4 }]));
    expect(await screen.findByText(/^Gönderim şu an: \.env ayarları/)).toBeInTheDocument();
  });

  it('admin_menu_lists_e_posta_gonderimi_under_sistem_ve_denetim', async () => {
    await openPage();
    const nav = screen.getByRole('navigation', { name: 'Yönetim menüsü' });
    const links = within(nav).getAllByRole('link').map((link) => link.textContent);
    const at = links.indexOf('E-posta gönderimi');
    expect(at).toBeGreaterThan(-1);
    expect(links[at - 1]).toBe('Giriş yöntemi');
    expect(links[at + 1]).toBe('İzleme kaynakları');
    expect(within(nav).getByRole('link', { name: 'E-posta gönderimi' })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('heading', { level: 1, name: 'E-posta gönderimi' })).toBeInTheDocument();
  });

  it('pickup_mode_shows_the_file_banner', async () => {
    await openPage({ ...SETTINGS, isPickup: true });
    expect(screen.getByText('Bu kurulum e-postaları dosyaya yazar (Pickup). Panel ve .env SMTP ayarları kullanılmaz.')).toBeInTheDocument();
  });
});
