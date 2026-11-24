/**
 * @input A licence row.
 * @output Its seat use as text and a token when it is full or ended.
 * @position Licence list and detail.
 */
import { HStack } from '@astryxdesign/core/HStack';
import { Text } from '@astryxdesign/core/Text';
import { Token } from '@astryxdesign/core/Token';
import { useTranslation } from 'react-i18next';

import type { LicenceListItem } from './api';

export function LicenceSeats({ licence }: { licence: Pick<LicenceListItem, 'used' | 'quantity' | 'isExpired'> }) {
  const { t } = useTranslation('licences');
  return (
    <HStack gap={2} vAlign="center" wrap="wrap">
      <Text>{t('seats', { used: licence.used, quantity: licence.quantity })}</Text>
      {licence.used >= licence.quantity && <Token label={t('states.full')} color="orange" />}
      {licence.isExpired && <Token label={t('states.expired')} color="red" />}
    </HStack>
  );
}
