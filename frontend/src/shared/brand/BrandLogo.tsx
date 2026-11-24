/**
 * @output The horizontal logo: mark and product name.
 * @position Screens without the app shell (sign-in, password links, 404).
 */
import { Heading } from '@astryxdesign/core/Heading';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { useTranslation } from 'react-i18next';

import { Icons } from '../ui/icons';

export function BrandLogo() {
  const { t } = useTranslation();
  return (
    <HStack gap={2} vAlign="center">
      <Icon icon={Icons.brand} size="lg" color="accent" />
      <Heading level={2}>{t('appName')}</Heading>
    </HStack>
  );
}
