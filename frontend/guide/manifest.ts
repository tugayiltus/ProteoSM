import type { Locator, Page } from '@playwright/test';

/**
 * Screenshots of the user guide (docs/user-guides/images/<id>-<view>.png), driven by guide/screenshots.ts against the
 * seeded pilot database (tools/seed-pilot.sh). Ids are grouped by the guide page that shows them.
 */

export type GuideRole = 'employee' | 'it' | 'approver' | 'admin';
export type GuideView = 'mobile' | 'desktop';

/** Seed accounts (backend Features/Pilot): the first account of each role. */
export const ROLE_EMAILS: Record<GuideRole, string> = {
  employee: 'personel01@pilot.example.com',
  it: 'it01@pilot.example.com',
  approver: 'onay01@pilot.example.com',
  admin: 'admin@pilot.example.com',
};

export const VIEW_VIEWPORTS: Record<GuideView, { width: number; height: number }> = {
  mobile: { width: 390, height: 844 },
  desktop: { width: 1280, height: 800 },
};

export interface GuideShot {
  /** File stem; the images are `<id>-mobile.png` and `<id>-desktop.png`. */
  id: string;
  role: GuideRole;
  path: string;
  /** Optional step run after the page loaded and before the picture is taken (open a dialog, switch a tab). */
  setup?: (page: Page) => Promise<void>;
  views: readonly GuideView[];
}

const ALL_VIEWS: readonly GuideView[] = ['mobile', 'desktop'];
const MOBILE: readonly GuideView[] = ['mobile'];
const DESKTOP: readonly GuideView[] = ['desktop'];

/** Visible "Aç" (open) links of a list; tables and phone cards both render one, only one of them is visible. */
const OPEN_LINK = /^Aç /;

/** Accessible name of the knowledge base article list; its links open the articles. */
const ARTICLE_LIST = 'Makaleler';

/** Incident links in the "İlgili arızalar" panel of a problem; their name starts with the incident number. */
const INCIDENT_LINK = /^INC-/;

/** Clicks the first visible match and waits until the page it leads to is open. */
const openFirst =
  (find: (page: Page) => Locator) =>
  async (page: Page): Promise<void> => {
    const before = page.url();
    await find(page).filter({ visible: true }).first().click();
    await page.waitForURL((url) => url.href !== before);
  };

/** Opens the first record of the list on the page. */
const openFirstRecord = openFirst((page) => page.getByRole('link', { name: OPEN_LINK }));
const openFirstArticle = openFirst((page) => page.getByRole('list', { name: ARTICLE_LIST }).getByRole('link'));
/** Opens the first linked incident on a problem page ("İlgili arızalar"). */
const openFirstIncident = openFirst((page) => page.getByRole('link', { name: INCIDENT_LINK }));

const clickButton =
  (name: string) =>
  async (page: Page): Promise<void> => {
    await page.getByRole('button', { name, exact: true }).filter({ visible: true }).first().click();
    await page.getByRole('dialog').waitFor();
  };

const CONFLICT_BANNER = /değişiklikle çakışıyor/;

/** Opens the first change of the list whose detail shows the window conflict banner. */
const openConflictingChange = async (page: Page): Promise<void> => {
  const links = page.getByRole('link', { name: OPEN_LINK }).filter({ visible: true });
  await links.first().waitFor();
  const hrefs = await links.evaluateAll((elements) => elements.map((element) => element.getAttribute('href') ?? ''));
  for (const href of hrefs) {
    await page.goto(href);
    await page.getByRole('heading', { name: 'Plan', exact: true }).waitFor();
    if (await page.getByText(CONFLICT_BANNER).first().isVisible()) return;
  }
  throw new Error('No change on the list page has a window conflict.');
};

/** The change report card of closed changes tied to an incident they caused. */
const CAUSED_INCIDENTS_CARD = 'Sorun çıkaran değişiklikler';

/** Tickets assigned to the signed-in IT person that are being worked on. */
const MY_WORK_QUEUE = '/workspace/queue?queue=assigned_to_me&status=in_progress';

/** Opens the first ticket of the list and scrolls its "İşlemler" card into view. */
const openWorkActions = async (page: Page): Promise<void> => {
  await openFirstRecord(page);
  await page.getByRole('heading', { name: 'İşlemler' }).scrollIntoViewIfNeeded();
};

/** Opens the phone menu and waits until its slide-in animation is over. */
const openMobileMenu = async (page: Page): Promise<void> => {
  await page.getByRole('button', { name: 'Menüyü aç' }).click();
  await page.getByRole('dialog').getByRole('link', { name: 'Kayıtlarım' }).waitFor();
  await page.waitForFunction(() => document.getAnimations().every((animation) => animation.playState !== 'running'));
};

/** Picks an option of an Astryx select: a combobox, or a button named "label value" when it has a search box. */
const chooseOption = async (page: Page, label: string, option: string): Promise<void> => {
  const button = new RegExp(`^${label}(?:\\s|$)`);
  await page.getByRole('combobox', { name: label }).or(page.getByRole('button', { name: button })).first().click();
  await page.getByRole('option', { name: option, exact: true }).click();
};

/** Search text of the central search shot; it finds records of several kinds in the pilot seed. */
const SEARCH_TEXT = 'VPN';
const SEARCH_LABEL = 'Ara';

/** Opens the central search dialog and waits until its result groups are shown. */
const openSearch = async (page: Page): Promise<void> => {
  await page.getByRole('button', { name: SEARCH_LABEL, exact: true }).filter({ visible: true }).first().click();
  const dialog = page.getByRole('dialog', { name: SEARCH_LABEL });
  await dialog.waitFor();
  await page.keyboard.type(SEARCH_TEXT);
  await dialog.getByText('Değişiklikler', { exact: true }).waitFor();
};

/** The "Hizmet" field of a record detail; its button opens the service picker ("Hizmet seç" or "Hizmeti değiştir"). */
const SERVICE_PICKER_BUTTON = /^(Hizmet seç|Hizmeti değiştir)$/;

/** Opens the first ticket of the list and the service picker on it. */
const openServicePicker = async (page: Page): Promise<void> => {
  await openFirstRecord(page);
  await page.getByRole('button', { name: SERVICE_PICKER_BUTTON }).filter({ visible: true }).first().click();
  await page.getByRole('dialog').waitFor();
};

/** The report card of records by service. */
const SERVICE_REPORT_CARD = 'Hizmete göre';

/** Row button of the admin service list that opens the asset links of a service ("Varlıklar <service>"). */
const SERVICE_ASSETS_BUTTON = /^Varlıklar /;

/** Seed names the guide shows in its examples (backend Features/Pilot). */
const PILOT_CATEGORY = 'Proteone Pilot';
const APPROVAL_REQUEST_TYPE = 'Yeni bilgisayar';

export const MANIFEST: readonly GuideShot[] = [
  // getting-started
  { id: 'home', role: 'employee', path: '/', views: ALL_VIEWS },
  { id: 'home-menu', role: 'employee', path: '/', setup: openMobileMenu, views: MOBILE },
  { id: 'search', role: 'admin', path: '/workspace', setup: openSearch, views: DESKTOP },
  // incidents-and-requests, feedback
  { id: 'tickets-new-incident', role: 'employee', path: '/tickets/new/incident', views: DESKTOP },
  {
    id: 'tickets-new-request',
    role: 'employee',
    path: '/tickets/new/request',
    setup: async (page) => {
      await chooseOption(page, 'Talep tipi', APPROVAL_REQUEST_TYPE);
    },
    views: MOBILE,
  },
  {
    id: 'feedback-incident',
    role: 'employee',
    path: '/tickets/new/incident',
    setup: async (page) => {
      await page.getByLabel(/^Konu/).fill('Rapor sayfası açılmıyor');
      await chooseOption(page, 'Kategori', PILOT_CATEGORY);
    },
    views: MOBILE,
  },
  // my-tickets
  { id: 'tickets-list', role: 'employee', path: '/tickets', views: DESKTOP },
  { id: 'ticket-detail', role: 'employee', path: '/tickets', setup: openFirstRecord, views: DESKTOP },
  // my-assets
  { id: 'my-assets', role: 'employee', path: '/assets/mine', views: MOBILE },
  // knowledge-base
  { id: 'knowledge-base', role: 'employee', path: '/kb', views: DESKTOP },
  { id: 'kb-article', role: 'employee', path: '/kb', setup: openFirstArticle, views: MOBILE },
  { id: 'kb-new', role: 'it', path: '/kb/new', views: DESKTOP },
  // it-workspace
  { id: 'workspace', role: 'it', path: '/workspace', views: DESKTOP },
  { id: 'workspace-queue', role: 'it', path: '/workspace/queue?queue=team', views: DESKTOP },
  { id: 'ticket-work', role: 'it', path: MY_WORK_QUEUE, setup: openWorkActions, views: DESKTOP },
  {
    id: 'ticket-complete',
    role: 'it',
    path: MY_WORK_QUEUE,
    setup: async (page) => {
      await openFirstRecord(page);
      await clickButton('Tamamla')(page);
    },
    views: DESKTOP,
  },
  // problems
  { id: 'problems-list', role: 'it', path: '/problems?queue=open', views: DESKTOP },
  { id: 'problem-detail', role: 'it', path: '/problems?status=known_error', setup: openFirstRecord, views: ALL_VIEWS },
  {
    id: 'ticket-problem',
    role: 'it',
    path: '/problems?status=known_error',
    setup: async (page) => {
      await openFirstRecord(page);
      await openFirstIncident(page);
      await page.getByRole('heading', { name: 'Problem', exact: true }).scrollIntoViewIfNeeded();
    },
    views: DESKTOP,
  },
  // tasks-and-changes
  { id: 'tasks-list', role: 'it', path: '/tasks?queue=team', views: DESKTOP },
  { id: 'task-detail', role: 'admin', path: '/tasks?status=in_progress', setup: openFirstRecord, views: DESKTOP },
  { id: 'changes-list', role: 'admin', path: '/changes?queue=open', views: DESKTOP },
  { id: 'change-detail', role: 'admin', path: '/changes?status=scheduled', setup: openConflictingChange, views: ALL_VIEWS },
  { id: 'change-calendar', role: 'admin', path: '/changes/calendar', views: ALL_VIEWS },
  // services
  { id: 'services-list', role: 'it', path: '/services', views: DESKTOP },
  { id: 'service-detail', role: 'it', path: '/services', setup: openFirstRecord, views: ALL_VIEWS },
  { id: 'ticket-service', role: 'it', path: MY_WORK_QUEUE, setup: openServicePicker, views: DESKTOP },
  {
    id: 'reports-service',
    role: 'it',
    path: '/reports/incidents',
    setup: async (page) => {
      await page.getByText(SERVICE_REPORT_CARD, { exact: true }).first().scrollIntoViewIfNeeded();
    },
    views: DESKTOP,
  },
  { id: 'admin-services', role: 'admin', path: '/admin/services', views: DESKTOP },
  {
    id: 'admin-service-assets',
    role: 'admin',
    path: '/admin/services',
    setup: async (page) => {
      await page.getByRole('button', { name: SERVICE_ASSETS_BUTTON }).filter({ visible: true }).first().click();
      await page.getByRole('dialog').waitFor();
    },
    views: DESKTOP,
  },
  // inventory
  { id: 'assets-list', role: 'it', path: '/assets', views: DESKTOP },
  { id: 'asset-detail', role: 'it', path: '/assets?status=assigned', setup: openFirstRecord, views: DESKTOP },
  { id: 'asset-new', role: 'it', path: '/assets', setup: clickButton('Yeni cihaz'), views: DESKTOP },
  { id: 'assets-import', role: 'it', path: '/assets/import', views: DESKTOP },
  // reports
  { id: 'reports-incidents', role: 'it', path: '/reports/incidents', views: ALL_VIEWS },
  { id: 'reports-changes', role: 'admin', path: '/reports/changes', views: DESKTOP },
  {
    id: 'reports-changes-caused',
    role: 'admin',
    path: '/reports/changes',
    setup: async (page) => {
      await page.getByText(CAUSED_INCIDENTS_CARD, { exact: true }).scrollIntoViewIfNeeded();
    },
    views: DESKTOP,
  },
  { id: 'reports-tasks', role: 'admin', path: '/reports/tasks', views: DESKTOP },
  { id: 'reports-problems', role: 'admin', path: '/reports/problems', views: DESKTOP },
  { id: 'reports-exports', role: 'it', path: '/reports/exports', views: DESKTOP },
  // approvals
  { id: 'approvals', role: 'approver', path: '/approvals', views: DESKTOP },
  // admin-users-and-organization, first-setup
  { id: 'admin-overview', role: 'admin', path: '/admin', views: DESKTOP },
  { id: 'admin-users', role: 'admin', path: '/admin/users', views: DESKTOP },
  { id: 'admin-user-new', role: 'admin', path: '/admin/users', setup: clickButton('Yeni kullanıcı'), views: DESKTOP },
  { id: 'admin-departments', role: 'admin', path: '/admin/departments', views: DESKTOP },
  { id: 'admin-locations', role: 'admin', path: '/admin/locations', views: DESKTOP },
  { id: 'admin-teams', role: 'admin', path: '/admin/teams', views: DESKTOP },
  { id: 'admin-audit', role: 'admin', path: '/admin/audit', views: DESKTOP },
  // admin-catalogs
  { id: 'admin-categories', role: 'admin', path: '/admin/categories', views: DESKTOP },
  { id: 'admin-request-types', role: 'admin', path: '/admin/request-types', views: DESKTOP },
  { id: 'admin-brands', role: 'admin', path: '/admin/brands', views: DESKTOP },
  { id: 'admin-models', role: 'admin', path: '/admin/models', views: DESKTOP },
  // admin-sla
  { id: 'admin-sla-calendar', role: 'admin', path: '/admin/sla/calendar', views: DESKTOP },
  { id: 'admin-sla-policy', role: 'admin', path: '/admin/sla/policy', views: DESKTOP },
  // admin-email-and-system
  { id: 'admin-email-templates', role: 'admin', path: '/admin/email-templates', views: DESKTOP },
  { id: 'admin-email-deliveries', role: 'admin', path: '/admin/email-deliveries', views: DESKTOP },
  { id: 'admin-system', role: 'admin', path: '/admin/system', views: DESKTOP },
];
