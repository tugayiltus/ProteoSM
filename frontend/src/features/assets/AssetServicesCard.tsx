/**
 * @input The asset id and tag.
 * @output A "Hizmetler" card with the services that use the asset; admins also link and unlink here.
 * @position Side column of the asset detail page.
 */
import { Button } from '@astryxdesign/core/Button';
import { HStack } from '@astryxdesign/core/HStack';
import { Link } from '@astryxdesign/core/Link';
import { List, ListItem } from '@astryxdesign/core/List';
import { Selector } from '@astryxdesign/core/Selector';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../../shared/api/errorMessage';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { ErrorState, LoadingState } from '../../shared/feedback/States';
import { ActiveToken } from '../../shared/page/ActiveToken';
import { SectionCard } from '../../shared/page/SectionCard';
import { servicePath } from '../../shared/routes';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';
import { useActiveServiceOptions, useLinkServiceAsset, useUnlinkServiceAsset } from '../admin/services/api';
import { ADMIN_ONLY, useSession } from '../auth/session';

import { useAssetServices } from './api';

const NO_SELECTION = '';

export function AssetServicesCard({ assetId, assetTag }: { assetId: string; assetTag: string }) {
  const { t } = useTranslation('assets');
  const { t: tc } = useTranslation();
  const canEdit = ADMIN_ONLY.includes(useSession().role);
  const services = useAssetServices(assetTag);
  const link = useLinkServiceAsset();
  const unlink = useUnlinkServiceAsset();
  const [selected, setSelected] = useState(NO_SELECTION);
  const linkedIds = new Set(services.data?.map((service) => service.id) ?? []);
  const options = useActiveServiceOptions(canEdit).filter((option) => !linkedIds.has(option.value));
  const error = link.error ?? unlink.error;

  const linkSelected = (event: FormEvent) => {
    event.preventDefault();
    if (selected === NO_SELECTION) {
      return;
    }
    unlink.reset();
    link.mutate({ serviceId: selected, assetId }, { onSuccess: () => setSelected(NO_SELECTION) });
  };

  return (
    <SectionCard title={t('services.title')}>
      <VStack gap={3}>
        {canEdit && <FormAlert message={error ? describeError(error) : null} />}
        {services.isPending && <LoadingState />}
        {services.isError && <ErrorState error={services.error} onRetry={() => void services.refetch()} />}
        {services.data && services.data.length === 0 && <Text type="supporting">{t('services.none')}</Text>}
        {services.data && services.data.length > 0 && (
          <List hasDividers density="compact" aria-label={t('services.title')}>
            {services.data.map((service) => (
              <ListItem
                key={service.id}
                label={<Link href={servicePath(service.id)}>{service.name}</Link>}
                endContent={
                  <HStack gap={2} vAlign="center">
                    <ActiveToken isActive={service.isActive} />
                    {canEdit && (
                      <Button
                        label={t('services.unlink')}
                        aria-label={recordActionLabel(t('services.unlink'), service.name)}
                        size="sm"
                        variant="ghost"
                        isLoading={unlink.isPending && unlink.variables?.serviceId === service.id}
                        isDisabled={unlink.isPending}
                        onClick={() => {
                          link.reset();
                          unlink.mutate({ serviceId: service.id, assetId });
                        }}
                      />
                    )}
                  </HStack>
                }
              />
            ))}
          </List>
        )}
        {canEdit && (
          <form onSubmit={linkSelected}>
            <HStack gap={2} vAlign="end" wrap="wrap">
              <Selector label={t('services.toLink')} value={selected} options={options} placeholder={tc('none')} onChange={setSelected} />
              <Button type="submit" label={t('services.link')} variant="secondary" isDisabled={selected === NO_SELECTION} isLoading={link.isPending} />
            </HStack>
          </form>
        )}
      </VStack>
    </SectionCard>
  );
}
