/**
 * @input The changed targets and pause reasons between the current policy version and the form.
 * @output A list of old to new values and a note on which tickets the new version reaches.
 * @position Above the save button of the SLA policy page.
 */
import { Banner } from '@astryxdesign/core/Banner';
import { List, ListItem } from '@astryxdesign/core/List';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';

import { SectionCard } from '../../../shared/page/SectionCard';
import { useFormatBusinessDuration } from '../../tickets/slaState';

import type { HoldReason } from './api';
import type { TargetChange } from './policyChanges';

const SECONDS_PER_MINUTE = 60;
const MINUTES_PER_HOUR = 60;

export function PolicyChangeSummary({ changes, pauseChanges }: { changes: readonly TargetChange[]; pauseChanges: readonly HoldReason[] }) {
  const { t } = useTranslation('admin');
  const { t: tt } = useTranslation('tickets');
  const formatDuration = useFormatBusinessDuration();
  // Under an hour the duration would only repeat the minutes.
  const minutesText = (minutes: number) =>
    minutes < MINUTES_PER_HOUR ? t('slaPolicy.minutesOnly', { minutes }) : t('slaPolicy.minutesWithDuration', { minutes, duration: formatDuration(minutes * SECONDS_PER_MINUTE) });
  const isEmpty = changes.length === 0 && pauseChanges.length === 0;

  return (
    <SectionCard title={t('slaPolicy.changesTitle')}>
      <VStack gap={3}>
        <Banner status="info" title={t('slaPolicy.appliesTo')} description={t('slaPolicy.appliesToDetail')} />
        {isEmpty ? (
          <Text type="supporting">{t('slaPolicy.noChanges')}</Text>
        ) : (
          <List hasDividers density="compact" aria-label={t('slaPolicy.changesTitle')}>
            {changes.map((change) => (
              <ListItem
                key={`${change.kind}-${change.priority}-${change.metric}-${change.field}`}
                label={`${tt(`kinds.${change.kind}`)} · ${tt(`priorities.${change.priority}`)} · ${tt(`sla.metrics.${change.metric}`)}`}
                description={
                  change.field === 'minutes'
                    ? t('slaPolicy.targetChange', { from: minutesText(change.from), to: minutesText(change.to) })
                    : t('slaPolicy.warningChange', { from: change.from, to: change.to })
                }
              />
            ))}
            {pauseChanges.map((reason) => (
              <ListItem key={reason} label={tt(`holdReasons.${reason}`)} description={t('slaPolicy.pauseChange')} />
            ))}
          </List>
        )}
      </VStack>
    </SectionCard>
  );
}
