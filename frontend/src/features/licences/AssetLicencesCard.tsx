/**
 * @input An asset tag.
 * @output The licences that use a seat for this asset.
 * @position Asset detail page (IT staff and admins).
 */
import { Link } from '@astryxdesign/core/Link';
import { List, ListItem } from '@astryxdesign/core/List';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';

import { ErrorState, LoadingState } from '../../shared/feedback/States';
import { formatDate } from '../../shared/lib/format';
import { SectionCard } from '../../shared/page/SectionCard';
import { licencePath } from '../../shared/routes';

import { useAssetLicences } from './api';

export function AssetLicencesCard({ assetTag }: { assetTag: string }) {
  const { t } = useTranslation('licences');
  const licences = useAssetLicences(assetTag);
  return (
    <SectionCard title={t('asset.title')}>
      <VStack gap={3}>
        {licences.isPending && <LoadingState />}
        {licences.isError && <ErrorState error={licences.error} onRetry={() => void licences.refetch()} />}
        {licences.data && licences.data.length === 0 && <Text type="supporting">{t('asset.none')}</Text>}
        {licences.data && licences.data.length > 0 && (
          <List hasDividers density="compact" aria-label={t('asset.title')}>
            {licences.data.map((x) => (
              <ListItem
                key={x.licenceId}
                label={<Link href={licencePath(x.licenceId)}>{`${x.vendor} ${x.softwareName}`}</Link>}
                description={t('asset.meta', { name: x.licenceName, end: x.expiresOn === null ? t('noEnd') : formatDate(x.expiresOn) })}
              />
            ))}
          </List>
        )}
      </VStack>
    </SectionCard>
  );
}
