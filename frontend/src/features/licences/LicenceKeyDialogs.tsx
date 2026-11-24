/**
 * @input A licence and a close callback.
 * @output Dialogs that store, clear or show the licence key.
 * @position Licence detail, admins only. The key lives only in the open dialog.
 */
import { Button } from '@astryxdesign/core/Button';
import { Text } from '@astryxdesign/core/Text';
import { TextArea } from '@astryxdesign/core/TextArea';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { ActionDialog } from '../../shared/feedback/ActionDialog';
import { ConfirmDialog } from '../../shared/feedback/ConfirmDialog';
import { InfoDialog } from '../../shared/feedback/InfoDialog';

import { useRevealLicenceKey, useSetLicenceKey, type LicenceDetail } from './api';

/** Mirrors backend LicenceLimits.KeyMaxLength. */
const KEY_MAX_LENGTH = 2000;

export function LicenceKeyDialog({ licence, onClose }: { licence: LicenceDetail; onClose: () => void }) {
  const { t } = useTranslation('licences');
  const { t: tc } = useTranslation();
  const save = useSetLicenceKey();
  const [key, setKey] = useState('');
  const [isClearing, setIsClearing] = useState(false);
  if (isClearing) {
    return (
      <ConfirmDialog
        title={t('key.clear')}
        message={t('key.clearMessage')}
        confirmLabel={t('key.clear')}
        variant="danger"
        isPending={save.isPending}
        error={save.error}
        onConfirm={() => save.mutate({ id: licence.id, version: licence.version, key: null }, { onSuccess: onClose })}
        onCancel={onClose}
      />
    );
  }
  return (
    <ActionDialog
      title={licence.hasKey ? t('key.change') : t('key.add')}
      confirmLabel={tc('actions.save')}
      isPending={save.isPending}
      isConfirmDisabled={key.trim() === '' || key.trim().length > KEY_MAX_LENGTH}
      error={save.error}
      onConfirm={() => save.mutate({ id: licence.id, version: licence.version, key: key.trim() }, { onSuccess: onClose })}
      onClose={onClose}
    >
      <TextArea label={t('key.title')} value={key} onChange={setKey} />
      {licence.hasKey && <Button label={t('key.clear')} variant="ghost" onClick={() => setIsClearing(true)} />}
    </ActionDialog>
  );
}

/** Asks first: every showing is written to the audit log. */
export function RevealKeyDialog({ licenceId, onClose }: { licenceId: string; onClose: () => void }) {
  const { t } = useTranslation('licences');
  const reveal = useRevealLicenceKey();
  if (reveal.data) {
    return (
      <InfoDialog title={t('key.title')} message={t('key.shown')} onClose={onClose}>
        <Text type="code" wordBreak="break-all">
          {reveal.data.key}
        </Text>
      </InfoDialog>
    );
  }
  return (
    <ConfirmDialog
      title={t('key.show')}
      message={t('key.showMessage')}
      confirmLabel={t('key.show')}
      isPending={reveal.isPending}
      error={reveal.error}
      onConfirm={() => reveal.mutate(licenceId)}
      onCancel={onClose}
    />
  );
}
