import type { Role } from '../../features/auth/api';
import { ADMIN_ONLY, IT_STAFF_OR_ADMIN } from '../../features/auth/session';
import { ROUTES } from '../routes';
import type { AppIcon } from '../ui/icons';

/**
 * Single source for page titles, the breadcrumb tree and the sidebar menus (spec §4.3).
 * Every signed-in route in `app/router.tsx` carries one of these ids.
 */
export const ROUTE_IDS = [
  'home',
  'profile',
  'changePassword',
  'myTickets',
  'newIncident',
  'newRequest',
  'ticketDetail',
  'workspace',
  'workspaceQueue',
  'tasks',
  'taskDetail',
  'problems',
  'problemDetail',
  'changes',
  'changeCalendar',
  'changeDetail',
  'releases',
  'releaseDetail',
  'approvals',
  'kbList',
  'kbNew',
  'kbArticle',
  'kbEdit',
  'myAssets',
  'assets',
  'assetImport',
  'assetLabels',
  'scanAsset',
  'assetDetail',
  'services',
  'serviceDetail',
  'licences',
  'licenceDetail',
  'software',
  'mySoftware',
  'reports',
  'reportExports',
  'report',
  'reportList',
  'adminOverview',
  'adminUsers',
  'adminDepartments',
  'adminLocations',
  'adminTeams',
  'adminCategories',
  'adminRequestTypes',
  'adminServices',
  'adminBrands',
  'adminModels',
  'adminEmailTemplates',
  'adminEmailDeliveries',
  'adminSlaCalendar',
  'adminSlaPolicy',
  'adminSystem',
  'adminLoginMethod',
  'adminMonitoring',
  'adminSettings',
  'adminAnnouncements',
  'adminAudit',
] as const;

export type RouteId = (typeof ROUTE_IDS)[number];

export type MenuArea = 'app' | 'admin';

export const APP_MENU_GROUPS = ['personal', 'itOps', 'changeOps', 'inventory', 'analysis'] as const;
export const ADMIN_MENU_GROUPS = ['overview', 'organization', 'deviceCatalog', 'serviceCatalog', 'sla', 'notifications', 'system'] as const;
export type MenuGroupId = (typeof APP_MENU_GROUPS)[number] | (typeof ADMIN_MENU_GROUPS)[number];

export interface RouteMeta {
  /** Path pattern exactly as in the router. */
  path: string;
  /** i18n key in the common namespace. */
  titleKey: string;
  parent?: RouteId;
  area: MenuArea;
  /** Menu group; admin pages also show it as plain text in the breadcrumb. */
  group?: MenuGroupId;
  /** Sidebar icon; required for menu routes (test-enforced). */
  icon?: AppIcon;
  /** Shown in the sidebar when set; roles that see it (none = everyone signed in). */
  menu?: { order: number; roles?: readonly Role[] };
  /** Title comes from loaded data (ticket number, article title …) set with `useBreadcrumbLabel`. */
  dynamic?: boolean;
  /** Title from a route param through an i18n prefix (e.g. report name). */
  titleParam?: { param: string; keyPrefix: string };
}

const app = (meta: Omit<RouteMeta, 'area'>): RouteMeta => ({ ...meta, area: 'app' });
const admin = (meta: Omit<RouteMeta, 'area'>): RouteMeta => ({ ...meta, area: 'admin', menu: { order: meta.menu?.order ?? 0, roles: ADMIN_ONLY } });

export const ROUTE_META: Record<RouteId, RouteMeta> = {
  home: app({ path: ROUTES.home, icon: 'home', titleKey: 'routes.home', group: 'personal', menu: { order: 10 } }),
  profile: app({ path: ROUTES.profile, titleKey: 'routes.profile', parent: 'home' }),
  changePassword: app({ path: ROUTES.changePassword, titleKey: 'routes.changePassword', parent: 'profile' }),
  myTickets: app({ path: ROUTES.tickets, icon: 'notes', titleKey: 'routes.myTickets', parent: 'home', group: 'personal', menu: { order: 20 } }),
  newIncident: app({ path: ROUTES.newIncident, titleKey: 'routes.newIncident', parent: 'myTickets' }),
  newRequest: app({ path: ROUTES.newRequest, titleKey: 'routes.newRequest', parent: 'myTickets' }),
  ticketDetail: app({ path: `${ROUTES.tickets}/:number`, titleKey: 'routes.ticketDetail', parent: 'myTickets', dynamic: true }),
  workspace: app({ path: ROUTES.workspace, icon: 'task', titleKey: 'routes.workspace', parent: 'home', group: 'itOps', menu: { order: 10, roles: IT_STAFF_OR_ADMIN } }),
  workspaceQueue: app({ path: ROUTES.workspaceQueue, icon: 'inbox', titleKey: 'routes.workspaceQueue', parent: 'workspace', group: 'itOps', menu: { order: 20, roles: IT_STAFF_OR_ADMIN } }),
  tasks: app({ path: ROUTES.tasks, icon: 'listChecks', titleKey: 'routes.tasks', parent: 'workspace', group: 'itOps', menu: { order: 25, roles: IT_STAFF_OR_ADMIN } }),
  taskDetail: app({ path: `${ROUTES.tasks}/:number`, titleKey: 'routes.taskDetail', parent: 'tasks', dynamic: true }),
  problems: app({ path: ROUTES.problems, icon: 'problems', titleKey: 'routes.problems', parent: 'workspace', group: 'changeOps', menu: { order: 26, roles: IT_STAFF_OR_ADMIN } }),
  problemDetail: app({ path: `${ROUTES.problems}/:number`, titleKey: 'routes.problemDetail', parent: 'problems', dynamic: true }),
  changes: app({ path: ROUTES.changes, icon: 'changes', titleKey: 'routes.changes', parent: 'workspace', group: 'changeOps', menu: { order: 27, roles: IT_STAFF_OR_ADMIN } }),
  changeCalendar: app({ path: ROUTES.changeCalendar, icon: 'calendar', titleKey: 'routes.changeCalendar', parent: 'changes' }),
  changeDetail: app({ path: `${ROUTES.changes}/:number`, titleKey: 'routes.changeDetail', parent: 'changes', dynamic: true }),
  releases: app({ path: ROUTES.releases, icon: 'releases', titleKey: 'routes.releases', parent: 'workspace', group: 'changeOps', menu: { order: 28, roles: IT_STAFF_OR_ADMIN } }),
  releaseDetail: app({ path: `${ROUTES.releases}/:number`, titleKey: 'routes.releaseDetail', parent: 'releases', dynamic: true }),
  approvals: app({ path: ROUTES.approvals, icon: 'checkCircle', titleKey: 'routes.approvals', parent: 'home', group: 'itOps', menu: { order: 30, roles: ADMIN_ONLY } }),
  kbList: app({ path: ROUTES.kb, icon: 'book', titleKey: 'routes.kbList', parent: 'home', group: 'personal', menu: { order: 40 } }),
  kbNew: app({ path: ROUTES.kbNew, titleKey: 'routes.kbNew', parent: 'kbList' }),
  kbArticle: app({ path: `${ROUTES.kb}/:id`, titleKey: 'routes.kbArticle', parent: 'kbList', dynamic: true }),
  kbEdit: app({ path: `${ROUTES.kb}/:id/edit`, titleKey: 'routes.kbEdit', parent: 'kbArticle' }),
  myAssets: app({ path: ROUTES.myAssets, icon: 'laptop', titleKey: 'routes.myAssets', parent: 'home', group: 'personal', menu: { order: 30 } }),
  assets: app({ path: ROUTES.assets, icon: 'devices', titleKey: 'routes.assets', parent: 'home', group: 'inventory', menu: { order: 40, roles: IT_STAFF_OR_ADMIN } }),
  services: app({ path: ROUTES.services, icon: 'services', titleKey: 'routes.services', parent: 'home', group: 'inventory', menu: { order: 45, roles: IT_STAFF_OR_ADMIN } }),
  serviceDetail: app({ path: `${ROUTES.services}/:id`, titleKey: 'routes.serviceDetail', parent: 'services', dynamic: true }),
  licences: app({ path: ROUTES.licences, icon: 'licences', titleKey: 'routes.licences', parent: 'home', group: 'inventory', menu: { order: 46, roles: IT_STAFF_OR_ADMIN } }),
  licenceDetail: app({ path: `${ROUTES.licences}/:id`, titleKey: 'routes.licenceDetail', parent: 'licences', dynamic: true }),
  software: app({ path: ROUTES.software, titleKey: 'routes.software', parent: 'licences' }),
  mySoftware: app({ path: ROUTES.mySoftware, icon: 'licences', titleKey: 'routes.mySoftware', parent: 'home', group: 'personal', menu: { order: 35 } }),
  assetImport: app({ path: ROUTES.assetImport, titleKey: 'routes.assetImport', parent: 'assets' }),
  scanAsset: app({ path: ROUTES.scanAsset, titleKey: 'routes.scanAsset' }),
  assetLabels: app({ path: ROUTES.assetLabels, titleKey: 'routes.assetLabels', parent: 'assets' }),
  assetDetail: app({ path: `${ROUTES.assets}/:id`, titleKey: 'routes.assetDetail', parent: 'assets', dynamic: true }),
  reports: app({ path: ROUTES.reports, icon: 'chartPie', titleKey: 'routes.reports', parent: 'home', group: 'analysis', menu: { order: 10, roles: IT_STAFF_OR_ADMIN } }),
  reportExports: app({ path: ROUTES.reportExports, icon: 'download', titleKey: 'routes.reportExports', parent: 'reports' }),
  report: app({ path: `${ROUTES.reports}/:report`, titleKey: 'routes.report', parent: 'reports', titleParam: { param: 'report', keyPrefix: 'reports:tabs.' } }),
  reportList: app({ path: `${ROUTES.reports}/:report/list`, titleKey: 'routes.reportList', parent: 'report', dynamic: true }),
  adminOverview: admin({ path: ROUTES.admin, icon: 'speedometer', titleKey: 'routes.adminOverview', group: 'overview', menu: { order: 10 } }),
  adminUsers: admin({ path: ROUTES.adminUsers, icon: 'people', titleKey: 'routes.adminUsers', parent: 'adminOverview', group: 'organization', menu: { order: 10 } }),
  adminDepartments: admin({ path: ROUTES.adminDepartments, icon: 'building', titleKey: 'routes.adminDepartments', parent: 'adminOverview', group: 'organization', menu: { order: 20 } }),
  adminLocations: admin({ path: ROUTES.adminLocations, icon: 'locationPin', titleKey: 'routes.adminLocations', parent: 'adminOverview', group: 'organization', menu: { order: 30 } }),
  adminTeams: admin({ path: ROUTES.adminTeams, icon: 'group', titleKey: 'routes.adminTeams', parent: 'adminOverview', group: 'organization', menu: { order: 40 } }),
  adminCategories: admin({ path: ROUTES.adminCategories, icon: 'tags', titleKey: 'routes.adminCategories', parent: 'adminOverview', group: 'serviceCatalog', menu: { order: 10 } }),
  adminRequestTypes: admin({ path: ROUTES.adminRequestTypes, icon: 'list', titleKey: 'routes.adminRequestTypes', parent: 'adminOverview', group: 'serviceCatalog', menu: { order: 20 } }),
  adminServices: admin({ path: ROUTES.adminServices, icon: 'services', titleKey: 'routes.adminServices', parent: 'adminOverview', group: 'serviceCatalog', menu: { order: 30 } }),
  adminBrands: admin({ path: ROUTES.adminBrands, icon: 'tag', titleKey: 'routes.adminBrands', parent: 'adminOverview', group: 'deviceCatalog', menu: { order: 10 } }),
  adminModels: admin({ path: ROUTES.adminModels, icon: 'screenDesktop', titleKey: 'routes.adminModels', parent: 'adminOverview', group: 'deviceCatalog', menu: { order: 20 } }),
  adminEmailTemplates: admin({ path: ROUTES.adminEmailTemplates, icon: 'envelopeOpen', titleKey: 'routes.adminEmailTemplates', parent: 'adminOverview', group: 'notifications', menu: { order: 10 } }),
  adminEmailDeliveries: admin({ path: ROUTES.adminEmailDeliveries, icon: 'envelopeClosed', titleKey: 'routes.adminEmailDeliveries', parent: 'adminOverview', group: 'notifications', menu: { order: 20 } }),
  adminSlaCalendar: admin({ path: ROUTES.adminSlaCalendar, icon: 'calendar', titleKey: 'routes.adminSlaCalendar', parent: 'adminOverview', group: 'sla', menu: { order: 10 } }),
  adminSlaPolicy: admin({ path: ROUTES.adminSlaPolicy, icon: 'timer', titleKey: 'routes.adminSlaPolicy', parent: 'adminOverview', group: 'sla', menu: { order: 20 } }),
  adminSystem: admin({ path: ROUTES.adminSystem, icon: 'settings', titleKey: 'routes.adminSystem', parent: 'adminOverview', group: 'system', menu: { order: 10 } }),
  adminLoginMethod: admin({ path: ROUTES.adminLoginMethod, icon: 'lock', titleKey: 'routes.adminLoginMethod', parent: 'adminOverview', group: 'system', menu: { order: 15 } }),
  adminMonitoring: admin({ path: ROUTES.adminMonitoring, icon: 'bell', titleKey: 'routes.adminMonitoring', parent: 'adminOverview', group: 'system', menu: { order: 17 } }),
  adminSettings: admin({ path: ROUTES.adminSettings, icon: 'pencil', titleKey: 'routes.adminSettings', parent: 'adminOverview', group: 'system', menu: { order: 5 } }),
  adminAnnouncements: admin({ path: ROUTES.adminAnnouncements, icon: 'megaphone', titleKey: 'routes.adminAnnouncements', parent: 'adminOverview', group: 'notifications', menu: { order: 5 } }),
  adminAudit: admin({ path: ROUTES.adminAudit, icon: 'history', titleKey: 'routes.adminAudit', parent: 'adminOverview', group: 'system', menu: { order: 20 } }),
};

export const isRouteId = (value: unknown): value is RouteId => typeof value === 'string' && (ROUTE_IDS as readonly string[]).includes(value);

/** The route and its ancestors, root first. */
export function routeChain(routeId: RouteId): RouteId[] {
  const chain: RouteId[] = [];
  for (let id: RouteId | undefined = routeId; id !== undefined; id = ROUTE_META[id].parent) {
    chain.unshift(id);
  }
  return chain;
}
