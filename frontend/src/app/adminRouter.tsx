import { Outlet, type RouteObject } from 'react-router';

import { RequireRole, RequireSession } from '../features/auth/guards';
import { ADMIN_ONLY } from '../features/auth/session';
import { ROUTES } from '../shared/routes';
import { NotFoundPage } from '../shared/ui/NotFoundPage';

import { LeaveArea } from './LeaveArea';
import { withRoot } from './withRoot';

/** The pages of the admin entry (ADR-0038). Sign-in and every other path belong to the user application. */
export const adminPages: RouteObject[] = [
  {
    element: (
      <RequireSession>
        <Outlet />
      </RequireSession>
    ),
    children: [
      {
        path: ROUTES.admin,
        element: (
          <RequireRole roles={ADMIN_ONLY}>
            <Outlet />
          </RequireRole>
        ),
        children: [
          {
            // Lazy like the admin pages: a static import would put the whole feature-admin chunk on every page's preload list.
            lazy: async () => ({ Component: (await import('../shared/layout/AdminLayout')).AdminLayout }),
            children: [
              { id: 'adminOverview', index: true, lazy: async () => ({ Component: (await import('../features/admin/overview/AdminOverviewPage')).AdminOverviewPage }) },
              { id: 'adminUsers', path: ROUTES.adminUsers, lazy: async () => ({ Component: (await import('../features/admin/users/UsersPage')).UsersPage }) },
              { id: 'adminDepartments', path: ROUTES.adminDepartments, lazy: async () => ({ Component: (await import('../features/admin/departments/DepartmentsPage')).DepartmentsPage }) },
              { id: 'adminLocations', path: ROUTES.adminLocations, lazy: async () => ({ Component: (await import('../features/admin/locations/LocationsPage')).LocationsPage }) },
              { id: 'adminTeams', path: ROUTES.adminTeams, lazy: async () => ({ Component: (await import('../features/admin/teams/TeamsPage')).TeamsPage }) },
              { id: 'adminServices', path: ROUTES.adminServices, lazy: async () => ({ Component: (await import('../features/admin/services/ServicesAdminPage')).ServicesAdminPage }) },
              { id: 'adminCategories', path: ROUTES.adminCategories, lazy: async () => ({ Component: (await import('../features/admin/catalog/CategoriesPage')).CategoriesPage }) },
              { id: 'adminRequestTypes', path: ROUTES.adminRequestTypes, lazy: async () => ({ Component: (await import('../features/admin/catalog/RequestTypesPage')).RequestTypesPage }) },
              { id: 'adminBrands', path: ROUTES.adminBrands, lazy: async () => ({ Component: (await import('../features/admin/deviceCatalog/BrandsPage')).BrandsPage }) },
              { id: 'adminModels', path: ROUTES.adminModels, lazy: async () => ({ Component: (await import('../features/admin/deviceCatalog/ModelsPage')).ModelsPage }) },
              { id: 'adminEmailTemplates', path: ROUTES.adminEmailTemplates, lazy: async () => ({ Component: (await import('../features/admin/email/EmailTemplatesPage')).EmailTemplatesPage }) },
              { id: 'adminEmailDeliveries', path: ROUTES.adminEmailDeliveries, lazy: async () => ({ Component: (await import('../features/admin/email/EmailDeliveriesPage')).EmailDeliveriesPage }) },
              { id: 'adminSlaCalendar', path: ROUTES.adminSlaCalendar, lazy: async () => ({ Component: (await import('../features/admin/sla/SlaCalendarPage')).SlaCalendarPage }) },
              { id: 'adminSlaPolicy', path: ROUTES.adminSlaPolicy, lazy: async () => ({ Component: (await import('../features/admin/sla/SlaPolicyPage')).SlaPolicyPage }) },
              { id: 'adminLoginMethod', path: ROUTES.adminLoginMethod, lazy: async () => ({ Component: (await import('../features/admin/auth/LoginMethodPage')).LoginMethodPage }) },
              { id: 'adminEmailSending', path: ROUTES.adminEmailSending, lazy: async () => ({ Component: (await import('../features/admin/email/EmailSendingPage')).EmailSendingPage }) },
              { id: 'adminMonitoring', path: ROUTES.adminMonitoring, lazy: async () => ({ Component: (await import('../features/admin/monitoring/MonitoringPage')).MonitoringPage }) },
              { id: 'adminSystem', path: ROUTES.adminSystem, lazy: async () => ({ Component: (await import('../features/admin/system/SystemPage')).SystemPage }) },
              { id: 'adminSettings', path: ROUTES.adminSettings, lazy: async () => ({ Component: (await import('../features/admin/settings/SettingsPage')).SettingsPage }) },
              { id: 'adminAnnouncements', path: ROUTES.adminAnnouncements, lazy: async () => ({ Component: (await import('../features/admin/announcements/AnnouncementsPage')).AnnouncementsPage }) },
              { id: 'adminAudit', path: ROUTES.adminAudit, lazy: async () => ({ Component: (await import('../features/admin/audit/AuditPage')).AuditPage }) },
            ],
          },
        ],
      },
    ],
  },
];

export const routes: RouteObject[] = withRoot([...adminPages, { path: `${ROUTES.admin}/*`, element: <NotFoundPage /> }, { path: '*', element: <LeaveArea /> }]);
