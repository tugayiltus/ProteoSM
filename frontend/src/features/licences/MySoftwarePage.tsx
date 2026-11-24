/**
 * @input Nothing; loads the software of the signed-in person.
 * @output The names of that software.
 * @position `/software/mine`, inside the app shell.
 */
import { Card } from '@astryxdesign/core/Card';
import { List, ListItem } from '@astryxdesign/core/List';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';

import { EmptyState, ErrorState, LoadingState } from '../../shared/feedback/States';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../shared/navigation/routeMeta';

import { useMySoftware } from './api';

const SKELETON_ROWS = 5;

export function MySoftwarePage() {
  const { t } = useTranslation('licences');
  const software = useMySoftware();
  return (
    <VStack gap={4}>
      <PageHeader icon={ROUTE_META.mySoftware.icon} />
      <Card padding={0}>
        {software.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {software.isError && <ErrorState error={software.error} onRetry={() => void software.refetch()} />}
        {software.data && software.data.length === 0 && <EmptyState message={t('mine.empty')} />}
        {software.data && software.data.length > 0 && (
          <List hasDividers aria-label={t('mine.caption')}>
            {software.data.map((x) => (
              <ListItem key={`${x.vendor} ${x.name}`} label={x.name} description={x.vendor} />
            ))}
          </List>
        )}
      </Card>
    </VStack>
  );
}
