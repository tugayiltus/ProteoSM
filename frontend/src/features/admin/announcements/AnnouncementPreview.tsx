/**
 * @input The title, text, level and publish period typed in the announcement form.
 * @output The banner an employee would see, and the period it would show for. It writes nothing.
 * @position Under the fields of the announcement form dialog.
 */
import { Banner } from '@astryxdesign/core/Banner';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';

import { formatDateTime } from '../../../shared/lib/format';
import { fromLocalInput } from '../../changes/dateTime';

export interface AnnouncementDraft {
  title: string;
  body: string;
  level: 'info' | 'warning';
  startsAt: string;
  endsAt: string;
}

export function AnnouncementPreview({ draft }: { draft: AnnouncementDraft }) {
  const { t } = useTranslation('admin');
  const startsAt = fromLocalInput(draft.startsAt);
  const endsAt = fromLocalInput(draft.endsAt);
  const period = startsAt === null ? t('announcements.preview.noStart') : endsAt === null ? t('announcements.preview.open', { from: formatDateTime(startsAt) }) : t('announcements.preview.range', { from: formatDateTime(startsAt), to: formatDateTime(endsAt) });
  return (
    <VStack gap={2} role="group" aria-label={t('announcements.preview.title')}>
      <Text type="label">{t('announcements.preview.title')}</Text>
      <Banner status={draft.level} title={draft.title.trim() === '' ? t('announcements.preview.emptyTitle') : draft.title} description={draft.body.trim() === '' ? undefined : draft.body} />
      <Text type="supporting">{period}</Text>
      <Text type="supporting">{t('announcements.preview.note')}</Text>
    </VStack>
  );
}
