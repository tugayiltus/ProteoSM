/**
 * @input `package.json`, `vite.config.ts`, `index.html` and every non-test source file, read as text at build time.
 * @output Fails when a UI library or styling method that Astryx replaced comes back (ADR-0027).
 * @position The one guard for the styling stack.
 */
import { describe, expect, it } from 'vitest';

import indexHtml from '../../index.html?raw';
import packageJson from '../../package.json';
import viteConfig from '../../vite.config.ts?raw';

const allDependencies: Record<string, string> = { ...packageJson.dependencies, ...packageJson.devDependencies };

/** Packages of the replaced UI stacks: CoreUI, Bootstrap, Sass, Tailwind and Radix. */
const LEGACY_PACKAGE = /^(@coreui\/.+|bootstrap|sass|sass-embedded|tailwindcss|@tailwindcss\/.+|radix-ui|@radix-ui\/.+)$/;
/**
 * An import of a legacy package: `from '…'`, a side-effect `import '…'`, or a dynamic `import('…')`.
 * A bare name ends at the quote or a `/`, so `sassy-x` or `bootstrapper` do not match.
 */
const LEGACY_IMPORT = /(?:from|import)\s*\(?\s*['"](?:@coreui\/|@tailwindcss\/|@radix-ui\/|(?:bootstrap|sass|sass-embedded|tailwindcss|radix-ui)(?=['"/]))/;
/** Astryx takes styling through props and tokens, so no component sets a class attribute. */
const CLASS_ATTRIBUTE = /\bclass(?:Name)?\s*=/;
/** Hooks the old stacks left in the build config and the page shell. */
const LEGACY_MARKER = /coreui|tailwind|bootstrap/i;

/** Every non-test source file as raw text, keyed by path; the glob runs at build time, so the test sees the real tree. */
const sources = import.meta.glob<string>(['../**/*.{ts,tsx}', '!../**/*.test.{ts,tsx}'], { query: '?raw', import: 'default', eager: true });
/** Styles come from Astryx and the theme; the app ships no style sheet of its own. Lazy, so only the paths load. */
const styleSheets = import.meta.glob('../**/*.{css,scss,sass}');

const offenders = (pattern: RegExp) =>
  Object.entries(sources)
    .filter(([, text]) => pattern.test(text))
    .map(([path]) => path);

describe('no legacy UI library or utility classes', () => {
  it('reads the application source', () => {
    expect(Object.keys(sources).length).toBeGreaterThan(0);
  });

  it('uses Astryx and depends on no replaced UI package', () => {
    expect(allDependencies).toHaveProperty('@astryxdesign/core');
    expect(Object.keys(allDependencies).filter((name) => LEGACY_PACKAGE.test(name))).toEqual([]);
  });

  it('matches whole legacy package names only', () => {
    const legacy = ["import '@coreui/coreui/dist/css/coreui.min.css';", "import 'bootstrap';", "from 'sass'", "import('tailwindcss/plugin')", "from \"radix-ui\""];
    const other = ["import 'sassy-x';", "from 'bootstrapper'", "from 'tailwindcss-like'", "from '@astryxdesign/core'"];
    expect(legacy.filter((line) => !LEGACY_IMPORT.test(line))).toEqual([]);
    expect(other.filter((line) => LEGACY_IMPORT.test(line))).toEqual([]);
  });

  it('has no source file that imports a replaced UI package', () => {
    expect(offenders(LEGACY_IMPORT)).toEqual([]);
  });

  it('has no class attribute in any component', () => {
    expect(offenders(CLASS_ATTRIBUTE)).toEqual([]);
  });

  it('ships no style sheet of its own', () => {
    expect(Object.keys(styleSheets)).toEqual([]);
  });

  it('keeps no legacy hook in the build config or the page shell', () => {
    expect(viteConfig).not.toMatch(LEGACY_MARKER);
    expect(indexHtml).not.toMatch(LEGACY_MARKER);
  });
});
