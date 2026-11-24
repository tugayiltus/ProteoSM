import { render, screen } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { describe, expect, it } from 'vitest';

import { entryUrl, isAdminPath, isLocalPath } from './area';
import { AreaLink } from './AreaLink';

const at = (pathname: string, state: unknown = null, search = '') => ({ pathname, search, hash: '', state });

describe('areas', () => {
  it.each([['/admin', true], ['/admin/users', true], ['/administration', false], ['/', false]])('%s is admin: %s', (path, expected) => {
    expect(isAdminPath(path)).toBe(expected);
  });

  it.each(['//evil.example', '/\\evil.example', 'https://evil.example', null])('%s is not a local path', (path) => {
    expect(isLocalPath(path)).toBe(false);
  });

  it('carries the page to return to into the sign-in address, and only there', () => {
    expect(entryUrl(at('/login', { from: '/admin/users?page=2' }))).toBe('/login?from=%2Fadmin%2Fusers%3Fpage%3D2');
    expect(entryUrl(at('/login', { from: '//evil.example' }))).toBe('/login');
    expect(entryUrl(at('/tickets', { from: '/admin' }, '?page=2'))).toBe('/tickets?page=2');
  });

  it('loads the page for a link into the other area and routes inside its own', async () => {
    const element = (
      <>
        <AreaLink to="/admin/users">admin</AreaLink>
        <AreaLink to="/tickets">app</AreaLink>
      </>
    );
    const router = createMemoryRouter([{ path: '*', element }], { initialEntries: ['/'] });
    render(<RouterProvider router={router} />);
    const stopped = new Promise<boolean>((resolve) => {
      window.addEventListener('click', (event) => { resolve(event.defaultPrevented); event.preventDefault(); }, { once: true });
    });
    screen.getByText('admin').click();
    expect(await stopped).toBe(false);
    screen.getByText('app').click();
    expect(router.state.location.pathname).toBe('/tickets');
  });
});
