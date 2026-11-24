import { Outlet, type RouteObject } from 'react-router';

import { ChangePasswordPage } from '../features/auth/ChangePasswordPage';
import { ForgotPasswordPage } from '../features/auth/ForgotPasswordPage';
import { RequireRole, RequireSession } from '../features/auth/guards';
import { LoginPage } from '../features/auth/LoginPage';
import { ADMIN_ONLY, IT_STAFF_OR_ADMIN } from '../features/auth/session';
import { SetPasswordPage } from '../features/auth/SetPasswordPage';
import { ProfilePage } from '../features/profile/ProfilePage';
import { AppLayout } from '../shared/layout/AppLayout';
import { ROUTES } from '../shared/routes';
import { NotFoundPage } from '../shared/ui/NotFoundPage';

import { LeaveArea } from './LeaveArea';
import { withRoot } from './withRoot';

/** Every page of the user application; the admin pages are the other entry (ADR-0038). */
export const userPages: RouteObject[] = [
  { path: ROUTES.login, element: <LoginPage /> },
  { path: ROUTES.setPassword, element: <SetPasswordPage /> },
  { path: ROUTES.forgotPassword, element: <ForgotPasswordPage /> },
  {
    element: (
      <RequireSession>
        <Outlet />
      </RequireSession>
    ),
    children: [
      {
        // Outside the app shell: the page holds only the labels, so the print shows nothing else.
        element: (
          <RequireRole roles={IT_STAFF_OR_ADMIN}>
            <Outlet />
          </RequireRole>
        ),
        children: [
          {
            id: 'assetLabels',
            path: ROUTES.assetLabels,
            lazy: async () => ({ Component: (await import('../features/assets/labels/AssetLabelsPrintPage')).AssetLabelsPrintPage }),
          },
        ],
      },
      {
        element: <AppLayout />,
        children: [
          { id: 'home', index: true, lazy: async () => ({ Component: (await import('../features/home/HomePage')).HomePage }) },
          { id: 'profile', path: ROUTES.profile, element: <ProfilePage /> },
          { id: 'changePassword', path: ROUTES.changePassword, element: <ChangePasswordPage /> },
          { id: 'myTickets', path: ROUTES.tickets, lazy: async () => ({ Component: (await import('../features/tickets/MyTicketsPage')).MyTicketsPage }) },
          { id: 'newIncident', path: ROUTES.newIncident, lazy: async () => ({ Component: (await import('../features/tickets/NewTicketPage')).NewIncidentPage }) },
          { id: 'newRequest', path: ROUTES.newRequest, lazy: async () => ({ Component: (await import('../features/tickets/NewTicketPage')).NewRequestPage }) },
          { id: 'ticketDetail', path: `${ROUTES.tickets}/:number`, lazy: async () => ({ Component: (await import('../features/tickets/TicketDetailPage')).TicketDetailPage }) },
          {
            element: (
              <RequireRole roles={ADMIN_ONLY}>
                <Outlet />
              </RequireRole>
            ),
            children: [{ id: 'approvals', path: ROUTES.approvals, lazy: async () => ({ Component: (await import('../features/approvals/ApprovalsPage')).ApprovalsPage }) }],
          },
          { id: 'kbList', path: ROUTES.kb, lazy: async () => ({ Component: (await import('../features/kb/KnowledgeListPage')).KnowledgeListPage }) },
          {
            element: (
              <RequireRole roles={IT_STAFF_OR_ADMIN}>
                <Outlet />
              </RequireRole>
            ),
            children: [
              { id: 'kbNew', path: ROUTES.kbNew, lazy: async () => ({ Component: (await import('../features/kb/ArticleEditorPage')).ArticleCreatePage }) },
              { id: 'kbEdit', path: `${ROUTES.kb}/:id/edit`, lazy: async () => ({ Component: (await import('../features/kb/ArticleEditorPage')).ArticleEditPage }) },
            ],
          },
          { id: 'kbArticle', path: `${ROUTES.kb}/:id`, lazy: async () => ({ Component: (await import('../features/kb/ArticlePage')).ArticlePage }) },
          { id: 'scanAsset', path: ROUTES.scanAsset, lazy: async () => ({ Component: (await import('../features/assets/labels/ScanAssetPage')).ScanAssetPage }) },
          { id: 'myAssets', path: ROUTES.myAssets, lazy: async () => ({ Component: (await import('../features/assets/MyAssetsPage')).MyAssetsPage }) },
          {
            element: (
              <RequireRole roles={IT_STAFF_OR_ADMIN}>
                <Outlet />
              </RequireRole>
            ),
            children: [
              { id: 'workspace', path: ROUTES.workspace, lazy: async () => ({ Component: (await import('../features/workspace/WorkspacePage')).WorkspacePage }) },
              { id: 'workspaceQueue', path: ROUTES.workspaceQueue, lazy: async () => ({ Component: (await import('../features/workspace/WorkspacePage')).QueuePage }) },
              { id: 'tasks', path: ROUTES.tasks, lazy: async () => ({ Component: (await import('../features/tasks/TaskListPage')).TaskListPage }) },
              { id: 'taskDetail', path: `${ROUTES.tasks}/:number`, lazy: async () => ({ Component: (await import('../features/tasks/TaskDetailPage')).TaskDetailPage }) },
              { id: 'problems', path: ROUTES.problems, lazy: async () => ({ Component: (await import('../features/problems/ProblemListPage')).ProblemListPage }) },
              { id: 'problemDetail', path: `${ROUTES.problems}/:number`, lazy: async () => ({ Component: (await import('../features/problems/ProblemDetailPage')).ProblemDetailPage }) },
              { id: 'changes', path: ROUTES.changes, lazy: async () => ({ Component: (await import('../features/changes/ChangeListPage')).ChangeListPage }) },
              { id: 'changeCalendar', path: ROUTES.changeCalendar, lazy: async () => ({ Component: (await import('../features/changes/ChangeCalendarPage')).ChangeCalendarPage }) },
              { id: 'changeDetail', path: `${ROUTES.changes}/:number`, lazy: async () => ({ Component: (await import('../features/changes/ChangeDetailPage')).ChangeDetailPage }) },
              { id: 'assets', path: ROUTES.assets, lazy: async () => ({ Component: (await import('../features/assets/AssetsPage')).AssetsPage }) },
              { id: 'assetImport', path: ROUTES.assetImport, lazy: async () => ({ Component: (await import('../features/assets/AssetImportPage')).AssetImportPage }) },
              { id: 'assetDetail', path: `${ROUTES.assets}/:id`, lazy: async () => ({ Component: (await import('../features/assets/AssetDetailPage')).AssetDetailPage }) },
              { id: 'services', path: ROUTES.services, lazy: async () => ({ Component: (await import('../features/services/ServicesPage')).ServicesPage }) },
              { id: 'serviceDetail', path: `${ROUTES.services}/:id`, lazy: async () => ({ Component: (await import('../features/services/ServiceDetailPage')).ServiceDetailPage }) },
            ],
          },
          {
            // Employees never see the entry; a direct URL shows the 403 page instead of silently going home.
            element: (
              <RequireRole roles={IT_STAFF_OR_ADMIN} forbidden>
                <Outlet />
              </RequireRole>
            ),
            children: [
              { id: 'reports', path: ROUTES.reports, lazy: async () => ({ Component: (await import('../features/reports/ReportsPage')).ReportsIndexRedirect }) },
              { id: 'reportExports', path: ROUTES.reportExports, lazy: async () => ({ Component: (await import('../features/reports/MyExportsPage')).MyExportsPage }) },
              { id: 'report', path: `${ROUTES.reports}/:report`, lazy: async () => ({ Component: (await import('../features/reports/ReportsPage')).ReportsPage }) },
              { id: 'reportList', path: `${ROUTES.reports}/:report/list`, lazy: async () => ({ Component: (await import('../features/reports/DrilldownPage')).DrilldownPage }) },
            ],
          },
        ],
      },
    ],
  },
];

export const routes: RouteObject[] = withRoot([...userPages, { path: `${ROUTES.admin}/*`, element: <LeaveArea /> }, { path: '*', element: <NotFoundPage /> }]);
