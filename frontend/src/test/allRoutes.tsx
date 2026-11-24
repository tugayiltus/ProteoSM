import { adminPages } from '../app/adminRouter';
import { userPages } from '../app/router';
import { withRoot } from '../app/withRoot';
import { NotFoundPage } from '../shared/ui/NotFoundPage';

/** Both entries in one router, so a test can follow a path across them without a page load. */
export const allRoutes = withRoot([...userPages, ...adminPages, { path: '*', element: <NotFoundPage /> }]);
