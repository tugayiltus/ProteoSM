/**
 * @input A licence and a close callback.
 * @output A dialog that finds a user or an asset, matching the licence kind, and gives one seat.
 * @position Licence detail.
 */
import { Selector } from '@astryxdesign/core/Selector';
import { TextInput } from '@astryxdesign/core/TextInput';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { ActionDialog } from '../../shared/feedback/ActionDialog';
import { useDebouncedValue } from '../../shared/lib/useDebouncedValue';
import { useAssets, useUserSearch } from '../assets/api';

import { useAssignLicence, type LicenceDetail } from './api';

const NO_SELECTION = '';
const SEARCH_DEBOUNCE_MS = 300;
const FIRST_PAGE = 1;
const PAGE_SIZE = 20;
const META_SEPARATOR = ' · ';

export function AssignLicenceDialog({ licence, onClose }: { licence: LicenceDetail; onClose: () => void }) {
  const { t } = useTranslation('licences');
  const { t: tc } = useTranslation();
  const perUser = licence.kind === 'per_user';
  const assign = useAssignLicence();
  const [search, setSearch] = useState('');
  const [selected, setSelected] = useState(NO_SELECTION);
  const query = useDebouncedValue(search.trim(), SEARCH_DEBOUNCE_MS);
  const users = useUserSearch(perUser ? query : '');
  const assets = useAssets({ search: query, type: undefined, status: undefined, page: FIRST_PAGE, pageSize: PAGE_SIZE }, !perUser && query !== '');
  const options =
    query === ''
      ? []
      : perUser
        ? (users.data ?? []).map((x) => ({ value: x.id, label: x.fullName }))
        : (assets.data?.items ?? []).map((x) => ({ value: x.id, label: [x.assetTag, `${x.brand} ${x.model}`].join(META_SEPARATOR) }));
  return (
    <ActionDialog
      title={t('detail.assign')}
      confirmLabel={t('detail.assign')}
      isPending={assign.isPending}
      isConfirmDisabled={selected === NO_SELECTION}
      error={assign.error}
      onConfirm={() => assign.mutate({ id: licence.id, userId: perUser ? selected : null, assetId: perUser ? null : selected }, { onSuccess: onClose })}
      onClose={onClose}
    >
      <TextInput
        type="search"
        label={perUser ? t('assign.searchUser') : t('assign.searchAsset')}
        value={search}
        onChange={(value) => {
          // The chosen target may vanish from the new results; a stale hidden value must not be sent.
          setSelected(NO_SELECTION);
          setSearch(value);
        }}
      />
      <Selector label={perUser ? t('kindTargets.per_user') : t('kindTargets.per_device')} value={selected} options={options} placeholder={tc('none')} onChange={setSelected} />
    </ActionDialog>
  );
}
