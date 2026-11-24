import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import indexHtml from '../../../index.html?raw';
import { stubSystemColorScheme } from '../../test/colorMode';

import { applyColorMode, COLOR_MODE_STORAGE_KEY, LEGACY_COLOR_MODE_STORAGE_KEY, readStoredMode, resetColorModeForTests, resolveMode, setColorMode, THEME_COLORS, useColorMode } from './colorMode';

describe('color mode', () => {
  beforeEach(() => {
    window.localStorage.clear();
    document.documentElement.style.colorScheme = '';
    resetColorModeForTests();
  });
  afterEach(() => vi.restoreAllMocks());

  it('defaults to auto and follows the system', () => {
    stubSystemColorScheme(true);
    expect(readStoredMode()).toBe('auto');
    expect(resolveMode('auto')).toBe('dark');
  });

  it('writes the choice and the resolved attribute', () => {
    stubSystemColorScheme(false);
    setColorMode('dark');
    expect(window.localStorage.getItem(COLOR_MODE_STORAGE_KEY)).toBe('dark');
    expect(document.documentElement.style.colorScheme).toBe('dark');
  });

  it('keeps the mode stored under the former product name until a new choice is made', () => {
    window.localStorage.setItem(LEGACY_COLOR_MODE_STORAGE_KEY, 'dark');
    expect(readStoredMode()).toBe('dark');
    setColorMode('light');
    expect(readStoredMode()).toBe('light');
    expect(indexHtml).toContain(`localStorage.getItem('${LEGACY_COLOR_MODE_STORAGE_KEY}')`);
  });

  it('ignores an unknown stored value', () => {
    stubSystemColorScheme(false);
    window.localStorage.setItem(COLOR_MODE_STORAGE_KEY, 'purple');
    expect(readStoredMode()).toBe('auto');
  });

  it('survives blocked storage', () => {
    stubSystemColorScheme(false);
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    expect(readStoredMode()).toBe('auto');
    expect(() => setColorMode('dark')).not.toThrow();
    expect(document.documentElement.style.colorScheme).toBe('dark');
  });

  it('re-resolves auto when the system switches', () => {
    const system = stubSystemColorScheme(false);
    const { result } = renderHook(() => useColorMode());
    act(() => result.current.setMode('auto'));
    expect(result.current.resolved).toBe('light');
    act(() => system.flip(true));
    expect(result.current.resolved).toBe('dark');
    expect(document.documentElement.style.colorScheme).toBe('dark');
  });

  it('keeps a fixed mode when the system switches', () => {
    const system = stubSystemColorScheme(false);
    const { result } = renderHook(() => useColorMode());
    act(() => result.current.setMode('light'));
    act(() => system.flip(true));
    expect(result.current.resolved).toBe('light');
  });

  it('applies a fixed mode without consulting the system', () => {
    stubSystemColorScheme(true);
    applyColorMode('light');
    expect(document.documentElement.style.colorScheme).toBe('light');
  });

  it('gives the browser bar the chosen mode color, also against the system', () => {
    stubSystemColorScheme(false);
    const metas = ['(prefers-color-scheme: light)', '(prefers-color-scheme: dark)'].map((media) => {
      const meta = document.createElement('meta');
      meta.name = 'theme-color';
      meta.media = media;
      document.head.append(meta);
      return meta;
    });
    try {
      setColorMode('dark');
      expect(metas.map((meta) => meta.content)).toEqual([THEME_COLORS.dark, THEME_COLORS.dark]);
      setColorMode('light');
      expect(metas.map((meta) => meta.content)).toEqual([THEME_COLORS.light, THEME_COLORS.light]);
    } finally {
      metas.forEach((meta) => meta.remove());
    }
  });

  it('starts the browser bar with the same colors as the app', () => {
    expect(indexHtml).toContain(`content="${THEME_COLORS.light}" media="(prefers-color-scheme: light)"`);
    expect(indexHtml).toContain(`content="${THEME_COLORS.dark}" media="(prefers-color-scheme: dark)"`);
  });

  it('applies the stored color mode before the first paint with the shared storage key', () => {
    expect(indexHtml).toContain(`localStorage.getItem('${COLOR_MODE_STORAGE_KEY}')`);
    expect(indexHtml).toContain('colorScheme');
  });
});
