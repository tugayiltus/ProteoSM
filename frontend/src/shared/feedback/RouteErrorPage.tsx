/**
 * @input The error a route threw while loading its chunk or rendering.
 * @output A short Turkish message with "Yeniden dene" (reload) and "Uygulamaya dön"; the error goes to the console only.
 * @position `errorElement` of the root route (full page) and of the pages inside each layout (inside the shell).
 */
import { Button } from '@astryxdesign/core/Button';
import { EmptyState } from '@astryxdesign/core/EmptyState';
import { Icon } from '@astryxdesign/core/Icon';
import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useRouteError } from 'react-router';

import { AuthCard } from '../layout/AuthCard';
import { ROUTES } from '../routes';
import { Icons } from '../ui/icons';

import { isStaleChunkError } from './staleChunk';

function RouteErrorContent() {
  const { t } = useTranslation();
  const error = useRouteError();
  // Users see no technical detail; the console keeps it for support.
  useEffect(() => {
    console.error('Route error', error);
  }, [error]);
  const stale = isStaleChunkError(error);
  return (
    <EmptyState
      headingLevel={1}
      icon={<Icon icon={Icons.warning} />}
      title={t(stale ? 'routeError.staleTitle' : 'routeError.title')}
      description={t(stale ? 'routeError.staleDescription' : 'routeError.description')}
      actions={
        <>
          <Button label={t('routeError.retry')} variant="primary" onClick={() => window.location.reload()} />
          <Button label={t('routeError.back')} variant="secondary" href={ROUTES.home} />
        </>
      }
    />
  );
}

/** The layout itself failed (or a route outside it): no shell to show it in, so a centered card. */
export function RouteErrorPage() {
  return (
    <AuthCard>
      <RouteErrorContent />
    </AuthCard>
  );
}

/** A page failed inside a loaded layout: the menu stays, the content area shows the message. */
export function RouteErrorInShell() {
  return <RouteErrorContent />;
}
