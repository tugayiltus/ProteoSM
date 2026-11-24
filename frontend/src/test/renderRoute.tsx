import { QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider, type InitialEntry } from 'react-router';

import { createQueryClient } from '../app/queryClient';
import { resetCsrfToken } from '../shared/api/client';
import { AstryxProvider } from '../shared/ui/AstryxProvider';

import { allRoutes } from './allRoutes';

export function renderRoute(path: InitialEntry) {
  resetCsrfToken();
  const queryClient = createQueryClient({ retry: false });
  const router = createMemoryRouter(allRoutes, { initialEntries: [path] });
  const user = userEvent.setup();
  render(
    <QueryClientProvider client={queryClient}>
      <AstryxProvider>
        <RouterProvider router={router} />
      </AstryxProvider>
    </QueryClientProvider>,
  );
  return { router, user, queryClient };
}
