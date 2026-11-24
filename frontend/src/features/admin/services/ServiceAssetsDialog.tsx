/**
 * @input A service id and name, and a close callback.
 * @output A dialog to find an asset by tag and link it, and to unlink the linked assets.
 * @position Opened from the admin service list.
 */
import { Button } from '@astryxdesign/core/Button';
import { Dialog, DialogHeader } from '@astryxdesign/core/Dialog';
import { HStack } from '@astryxdesign/core/HStack';
import { Layout, LayoutContent, LayoutFooter } from '@astryxdesign/core/Layout';
import { Link } from '@astryxdesign/core/Link';
import { List, ListItem } from '@astryxdesign/core/List';
import { Selector } from '@astryxdesign/core/Selector';
import { Text } from '@astryxdesign/core/Text';
import { TextInput } from '@astryxdesign/core/TextInput';
import { VStack } from '@astryxdesign/core/VStack';
import { useMemo, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../../../shared/api/errorMessage';
import { FormAlert } from '../../../shared/feedback/FormAlert';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/feedback/States';
import { useDebouncedValue } from '../../../shared/lib/useDebouncedValue';
import { ROUTES, serviceFilteredPath } from '../../../shared/routes';
import { recordActionLabel } from '../../../shared/ui/recordActionLabel';
import { useAssets, type AssetStatus } from '../../assets/api';
import { useLinkServiceAsset, useServiceImpact, useUnlinkServiceAsset } from '../../services/serviceApi';

const NO_SELECTION = '';
const DIALOG_WIDTH = 560;
const META_SEPARATOR = ' · ';
const SEARCH_DEBOUNCE_MS = 300;
const SEARCH_PAGE_SIZE = 20;
const FIRST_PAGE = 1;

export function ServiceAssetsDialog({ serviceId, serviceName, onClose }: { serviceId: string; serviceName: string; onClose: () => void }) {
  const { t } = useTranslation('admin');
  const { t: ta } = useTranslation('assets');
  const { t: tc } = useTranslation();
  const impact = useServiceImpact(serviceId);
  const link = useLinkServiceAsset();
  const unlink = useUnlinkServiceAsset();
  const [search, setSearch] = useState('');
  const [selected, setSelected] = useState(NO_SELECTION);
  const query = useDebouncedValue(search.trim(), SEARCH_DEBOUNCE_MS);
  const found = useAssets({ search: query, type: undefined, status: undefined, page: FIRST_PAGE, pageSize: SEARCH_PAGE_SIZE }, query !== '');
  const options = useMemo(
    () =>
      query === ''
        ? []
        : (found.data?.items ?? []).map((asset) => ({
            value: asset.id,
            label: [asset.assetTag, `${asset.brand} ${asset.model}`, ta(`statuses.${asset.status}`)].join(META_SEPARATOR),
          })),
    [found.data, query, ta],
  );
  const error = link.error ?? unlink.error;
  const assets = impact.data?.assets;

  const linkSelected = (event: FormEvent) => {
    event.preventDefault();
    if (selected === NO_SELECTION) {
      return;
    }
    unlink.reset();
    link.mutate({ serviceId, assetId: selected }, { onSuccess: () => setSelected(NO_SELECTION) });
  };
  const close = (open: boolean) => {
    if (!open) {
      onClose();
    }
  };

  return (
    <Dialog isOpen onOpenChange={close} width={DIALOG_WIDTH}>
      <Layout
        header={<DialogHeader title={t('services.assetsTitle', { name: serviceName })} onOpenChange={close} />}
        content={
          <LayoutContent>
            <VStack gap={4}>
              <FormAlert message={error ? describeError(error) : null} />
              <form onSubmit={linkSelected}>
                <VStack gap={2}>
                  <TextInput
                    type="search"
                    label={t('services.assetSearch')}
                    value={search}
                    onChange={(value) => {
                      // The chosen asset may vanish from the new results; a stale hidden value must not be sent.
                      setSelected(NO_SELECTION);
                      setSearch(value);
                    }}
                  />
                  <HStack gap={2} vAlign="end" wrap="wrap">
                    <Selector label={t('services.assetToLink')} value={selected} options={options} placeholder={tc('none')} onChange={setSelected} />
                    <Button type="submit" label={t('services.linkAsset')} variant="primary" isDisabled={selected === NO_SELECTION} isLoading={link.isPending} />
                  </HStack>
                </VStack>
              </form>
              {impact.isPending && <LoadingState />}
              {impact.isError && <ErrorState error={impact.error} onRetry={() => void impact.refetch()} />}
              {assets && assets.items.length === 0 && <EmptyState message={t('services.noAssets')} />}
              {assets && assets.items.length > 0 && (
                <VStack gap={2}>
                  <List hasDividers density="compact" aria-label={t('services.assetsTitle', { name: serviceName })}>
                    {assets.items.map((asset) => (
                      <ListItem
                        key={asset.id}
                        label={asset.tag}
                        description={[asset.name, ta(`statuses.${asset.status as AssetStatus}`)].join(META_SEPARATOR)}
                        endContent={
                          <Button
                            label={t('services.unlink')}
                            aria-label={recordActionLabel(t('services.unlink'), asset.tag)}
                            size="sm"
                            variant="ghost"
                            isLoading={unlink.isPending && unlink.variables?.assetId === asset.id}
                            isDisabled={unlink.isPending}
                            onClick={() => {
                              link.reset();
                              unlink.mutate({ serviceId, assetId: asset.id });
                            }}
                          />
                        }
                      />
                    ))}
                  </List>
                  {assets.total > assets.items.length && (
                    <Text type="supporting">
                      {t('services.assetsShown', { count: assets.items.length, total: assets.total })} <Link href={serviceFilteredPath(ROUTES.assets, serviceId)}>{t('services.assetsAll')}</Link>
                    </Text>
                  )}
                </VStack>
              )}
            </VStack>
          </LayoutContent>
        }
        footer={
          <LayoutFooter>
            <HStack hAlign="end">
              <Button label={tc('actions.close')} variant="primary" onClick={onClose} />
            </HStack>
          </LayoutFooter>
        }
      />
    </Dialog>
  );
}
