import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { createBrowserRouter, type RouteObject } from 'react-router';

// Astryx layers: reset, components, theme tokens (ADR-0027).
import '@astryxdesign/core/reset.css';
import '@astryxdesign/core/astryx.css';
import '@astryxdesign/theme-neutral/theme.css';
// Self-hosted Figtree (theme font) instead of the theme's Google Fonts import: no outside request.
import '@fontsource/figtree/400.css';
import '@fontsource/figtree/500.css';
import '@fontsource/figtree/600.css';
import '@fontsource/figtree/700.css';
import '../shared/i18n';
import { initColorMode } from '../shared/ui/colorMode';

import { App } from './App';

/** Starts one entry page: the user application (`main.tsx`) or the admin pages (`admin.tsx`). */
export function mount(routes: RouteObject[]) {
  initColorMode();
  const rootElement = document.getElementById('root');
  if (rootElement === null) {
    throw new Error('Root element #root is missing.');
  }
  createRoot(rootElement).render(
    <StrictMode>
      <App router={createBrowserRouter(routes)} />
    </StrictMode>,
  );
}
