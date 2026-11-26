/** URL paths are English (ADR-0006). */
export const ROUTES = {
  home: '/',
  login: '/login',
  changePassword: '/change-password',
  setPassword: '/set-password',
  forgotPassword: '/forgot-password',
  profile: '/profile',
  assets: '/assets',
  assetImport: '/assets/import',
  assetLabels: '/assets/labels',
  scanAsset: '/scan/asset/:id',
  myAssets: '/assets/mine',
  tickets: '/tickets',
  tasks: '/tasks',
  problems: '/problems',
  services: '/services',
  releases: '/releases',
  licences: '/licences',
  software: '/software',
  mySoftware: '/software/mine',
  changes: '/changes',
  changeCalendar: '/changes/calendar',
  newIncident: '/tickets/new/incident',
  newRequest: '/tickets/new/request',
  kb: '/kb',
  kbNew: '/kb/new',
  workspace: '/workspace',
  workspaceQueue: '/workspace/queue',
  approvals: '/approvals',
  reports: '/reports',
  reportExports: '/reports/exports',
  admin: '/admin',
  adminUsers: '/admin/users',
  adminDepartments: '/admin/departments',
  adminLocations: '/admin/locations',
  adminTeams: '/admin/teams',
  adminServices: '/admin/services',
  adminAudit: '/admin/audit',
  adminEmailTemplates: '/admin/email-templates',
  adminEmailDeliveries: '/admin/email-deliveries',
  adminSystem: '/admin/system',
  adminSettings: '/admin/settings',
  adminAnnouncements: '/admin/announcements',
  adminMonitoring: '/admin/monitoring',
  adminLoginMethod: '/admin/login-method',
  adminSlaCalendar: '/admin/sla/calendar',
  adminSlaPolicy: '/admin/sla/policy',
  adminCategories: '/admin/categories',
  adminRequestTypes: '/admin/request-types',
  adminBrands: '/admin/brands',
  adminModels: '/admin/models',
} as const;

export const assetPath = (id: string) => `${ROUTES.assets}/${id}`;
export const SCANNED_ASSET_PARAM = 'asset';
/** New incident with a scanned device chosen (ADR-0028). */
export const newIncidentPath = (assetId: string) => `${ROUTES.newIncident}?${new URLSearchParams({ [SCANNED_ASSET_PARAM]: assetId }).toString()}`;
/** Print view of one asset's label; a bulk print passes its ids in navigation state (a long list does not fit a URL). */
export const assetLabelsPath = (id: string) => `${ROUTES.assetLabels}?${new URLSearchParams({ ids: id }).toString()}`;

export const ticketPath = (number: string) => `${ROUTES.tickets}/${number}`;
/** The completion email and the home card link a ticket with `?rate=1`; the rating form then takes the focus. */
export const RATE_PARAM = 'rate';
export const RATE_PARAM_ON = '1';
export const ticketRatePath = (number: string) => `${ticketPath(number)}?${new URLSearchParams({ [RATE_PARAM]: RATE_PARAM_ON }).toString()}`;
export const taskPath = (number: string) => `${ROUTES.tasks}/${number}`;
export const servicePath = (id: string) => `${ROUTES.services}/${id}`;
/** Query parameter of the staff ticket, change and problem lists that keeps only one service's records. */
export const SERVICE_FILTER_PARAM = 'serviceId';
/** Query parameter of those lists that keeps only the records the service page counts as open. */
export const OPEN_FILTER_PARAM = 'open';
export const OPEN_FILTER_ON = 'true';
/** A record list filtered to one service ("Tümünü göster" on the service page). */
export const serviceFilteredPath = (listPath: string, serviceId: string, openOnly = false) => `${listPath}?${new URLSearchParams({ [SERVICE_FILTER_PARAM]: serviceId, ...(openOnly ? { [OPEN_FILTER_PARAM]: OPEN_FILTER_ON } : {}) }).toString()}`;
export const releasePath = (number: string) => `${ROUTES.releases}/${number}`;
export const licencePath = (id: string) => `${ROUTES.licences}/${id}`;
/** Query parameter of the licence list that keeps one software's licences. */
export const SOFTWARE_FILTER_PARAM = 'software';
export const softwareLicencesPath = (softwareId: string) => `${ROUTES.licences}?${new URLSearchParams({ [SOFTWARE_FILTER_PARAM]: softwareId }).toString()}`;
export const problemPath = (number: string) => `${ROUTES.problems}/${number}`;
export const changePath = (number: string) => `${ROUTES.changes}/${number}`;

export const articlePath = (id: string) => `${ROUTES.kb}/${id}`;

export const articleEditPath = (id: string) => `${ROUTES.kb}/${id}/edit`;

export const reportPath = (report: string) => `${ROUTES.reports}/${report}`;

/** Drill-down rows behind a report figure; metric, bucket and filters go in the query string. */
export const reportListPath = (report: string) => `${ROUTES.reports}/${report}/list`;
