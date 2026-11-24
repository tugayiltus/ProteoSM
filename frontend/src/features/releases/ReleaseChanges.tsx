/**
 * @input A release.
 * @output Its changes with their state; people who work on the release add a change by number or remove one.
 * @position Release detail, main column. A change the viewer cannot open is plain text (ADR-0033).
 */
import { Button } from '@astryxdesign/core/Button';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { List, ListItem } from '@astryxdesign/core/List';
import { Text } from '@astryxdesign/core/Text';
import { TextInput } from '@astryxdesign/core/TextInput';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router';

import { ActionDialog } from '../../shared/feedback/ActionDialog';
import { ConfirmDialog } from '../../shared/feedback/ConfirmDialog';
import { linkState } from '../../shared/navigation/returnTo';
import { StateLink } from '../../shared/navigation/StateLink';
import { SectionCard } from '../../shared/page/SectionCard';
import { changePath } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';
import { ChangeStatusBadge } from '../changes/ChangeBadges';

import { isOpenRelease, useAddReleaseChange, useRemoveReleaseChange, type ReleaseChangeItem, type ReleaseDetail } from './api';

const META_SEPARATOR = ' · ';

function AddDialog({ release, onClose }: { release: ReleaseDetail; onClose: () => void }) {
  const { t } = useTranslation('releases');
  const add = useAddReleaseChange();
  const [number, setNumber] = useState('');
  return (
    <ActionDialog
      title={t('changes.add')}
      confirmLabel={t('changes.add')}
      isPending={add.isPending}
      isConfirmDisabled={number.trim() === ''}
      error={add.error}
      onConfirm={() => add.mutate({ number: release.number, changeNumber: number.trim() }, { onSuccess: onClose })}
      onClose={onClose}
    >
      <TextInput label={t('changes.number')} value={number} onChange={setNumber} />
    </ActionDialog>
  );
}

function RemoveDialog({ release, changeNumber, onClose }: { release: ReleaseDetail; changeNumber: string; onClose: () => void }) {
  const { t } = useTranslation('releases');
  const remove = useRemoveReleaseChange();
  return (
    <ConfirmDialog
      title={t('changes.remove')}
      message={t('changes.removeMessage', { number: changeNumber })}
      confirmLabel={t('changes.remove')}
      variant="danger"
      isPending={remove.isPending}
      error={remove.error}
      onConfirm={() => remove.mutate({ number: release.number, changeNumber }, { onSuccess: onClose })}
      onCancel={onClose}
    />
  );
}

export function ReleaseChanges({ release }: { release: ReleaseDetail }) {
  const { t } = useTranslation('releases');
  const location = useLocation();
  const [isAdding, setIsAdding] = useState(false);
  const [removing, setRemoving] = useState<string | null>(null);
  const canEdit = release.canWork && isOpenRelease(release.status);
  const label = (x: ReleaseChangeItem) => `${x.number}${META_SEPARATOR}${x.title}`;
  return (
    <SectionCard
      title={t('changes.title')}
      actions={canEdit && <Button label={t('changes.add')} variant="secondary" size="sm" icon={<Icon icon={Icons.plus} />} onClick={() => setIsAdding(true)} />}
    >
      {release.changes.length === 0 && <Text type="supporting">{t('changes.empty')}</Text>}
      {release.changes.length > 0 && (
        <List hasDividers density="compact" aria-label={t('changes.title')}>
          {release.changes.map((x) => (
            <ListItem
              key={x.number}
              label={
                x.canOpen ? (
                  <StateLink href={changePath(x.number)} state={linkState(location)}>
                    {label(x)}
                  </StateLink>
                ) : (
                  label(x)
                )
              }
              description={x.canOpen ? x.ownerTeamName : [x.ownerTeamName, t('changes.notOpenable')].join(META_SEPARATOR)}
              endContent={
                <HStack gap={2} vAlign="center" wrap="wrap">
                  <ChangeStatusBadge status={x.status} />
                  {canEdit && <Button label={t('changes.remove')} aria-label={recordActionLabel(t('changes.remove'), x.number)} size="sm" variant="ghost" onClick={() => setRemoving(x.number)} />}
                </HStack>
              }
            />
          ))}
        </List>
      )}
      {isAdding && <AddDialog release={release} onClose={() => setIsAdding(false)} />}
      {removing !== null && <RemoveDialog release={release} changeNumber={removing} onClose={() => setRemoving(null)} />}
    </SectionCard>
  );
}
