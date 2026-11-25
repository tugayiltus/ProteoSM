/**
 * @input Nothing; loads the announcements of this moment.
 * @output One banner per announcement, or nothing. A failed load stays silent: the page works without it.
 * @position Top of the home page.
 */
import { Banner } from '@astryxdesign/core/Banner';
import { VStack } from '@astryxdesign/core/VStack';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { api, unwrap } from '../../shared/api/client';

export function Announcements() {
  const { t } = useTranslation('tickets');
  const announcements = useQuery({ queryKey: ['announcements'], queryFn: () => unwrap(api.GET('/api/v1/announcements')) });
  if (announcements.data === undefined || announcements.data.length === 0) {
    return null;
  }
  return (
    <VStack gap={2} role="region" aria-label={t('home.announcements')}>
      {announcements.data.map((item) => (
        <Banner key={item.id} status={item.level} title={item.title} description={item.body} />
      ))}
    </VStack>
  );
}
