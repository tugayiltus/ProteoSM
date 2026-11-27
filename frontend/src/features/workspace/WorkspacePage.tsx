/**
 * @input `queue` query parameter on the queue page.
 * @output İş listem: the title, the counted views, then the combined list of my open work; and the queue page (a ticket list) under the same title and views.
 * @position `/workspace` and `/workspace/queue`, inside the app shell.
 */
import { Heading } from '@astryxdesign/core/Heading';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';

import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { WorkItemList } from '../tasks/WorkItemList';
import { WORKSPACE_QUEUES, type WorkQueue } from '../tickets/api';
import { TicketList } from '../tickets/TicketList';

import { ALL_QUEUE, QUEUE_PARAM } from './queuePath';
import { WorkViewBar, type WorkView } from './WorkViewBar';

const DEFAULT_QUEUE: WorkQueue = 'team';

export function WorkspacePage() {
  const { t } = useTranslation('tickets');
  return (
    <VStack gap={4}>
      <PageHeader icon={ROUTE_META.workspace.icon} titleOverride={t('workspace.title')} />
      <WorkViewBar view="assigned" />
      <WorkItemList />
    </VStack>
  );
}

/** Only these two queues have their own counted view; every other queue falls under "Diğer kuyruklar". */
const queueView = (queue: WorkQueue | typeof ALL_QUEUE): WorkView => (queue === 'unassigned' || queue === 'sla_risk' ? queue : 'other');

export function QueuePage() {
  const { t } = useTranslation('tickets');
  const [params] = useSearchParams();
  const requested = params.get(QUEUE_PARAM);
  const isAll = requested === ALL_QUEUE;
  const queue = WORKSPACE_QUEUES.find((q) => q === requested) ?? DEFAULT_QUEUE;
  const scope = isAll ? ALL_QUEUE : queue;
  return (
    <VStack gap={4}>
      <PageHeader icon={ROUTE_META.workspace.icon} titleOverride={t('workspace.title')} />
      <WorkViewBar key={queueView(scope)} view={queueView(scope)} />
      <Heading level={2}>{t(`workspace.listTitles.${scope}`)}</Heading>
      <TicketList key={scope} queue={isAll ? undefined : queue} showWorkColumns title={t(`workspace.listTitles.${scope}`)} />
    </VStack>
  );
}
