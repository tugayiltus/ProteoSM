import { QueryClientProvider } from '@tanstack/react-query';
import { RouterProvider, type createBrowserRouter } from 'react-router';

import { AstryxProvider } from '../shared/ui/AstryxProvider';

import { createQueryClient } from './queryClient';

const queryClient = createQueryClient();

export function App({ router }: { router: ReturnType<typeof createBrowserRouter> }) {
  return (
    <QueryClientProvider client={queryClient}>
      <AstryxProvider>
        <RouterProvider router={router} />
      </AstryxProvider>
    </QueryClientProvider>
  );
}
