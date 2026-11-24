/**
 * @input The record's current service, an optional suggested service, and the save mutation state.
 * @output A dialog with a "Hizmet" select over the active services and, when given, the hint "Önerilen: …".
 * @position Opened from `ServiceField` on ticket, change and problem detail pages (IT staff and admins).
 */
import { Selector } from '@astryxdesign/core/Selector';
import { Text } from '@astryxdesign/core/Text';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { ActionDialog } from '../../shared/feedback/ActionDialog';

import type { ServiceRef } from './api';
import { useActiveServiceOptions } from './serviceApi';


const NO_SELECTION = '';

export interface ServicePickerProps {
  current: ServiceRef | null;
  /** Pre-selected while the user has not chosen; it is never saved without the confirm click. */
  suggestion: ServiceRef | null;
  isPending: boolean;
  error: unknown;
  onSave: (serviceId: string) => void;
  onClose: () => void;
}

/** Picks one active service. Clearing the service is a separate action on the field. */
export function ServicePicker({ current, suggestion, isPending, error, onSave, onClose }: ServicePickerProps) {
  const { t } = useTranslation('services');
  const { t: tc } = useTranslation();
  const options = useActiveServiceOptions(true);
  // `undefined` means "not chosen yet": the suggestion can still arrive after the dialog opens.
  const [chosen, setChosen] = useState<string | undefined>(undefined);
  const showSuggestion = current === null && suggestion !== null;
  const fallback = current?.isActive === true ? current.id : showSuggestion ? suggestion.id : NO_SELECTION;
  const selected = chosen ?? fallback;
  const unchanged = selected === NO_SELECTION || selected === current?.id;

  return (
    <ActionDialog
      title={current === null ? t('field.choose') : t('field.change')}
      confirmLabel={tc('actions.save')}
      isPending={isPending}
      isConfirmDisabled={unchanged}
      error={error}
      onConfirm={() => onSave(selected)}
      onClose={onClose}
    >
      <Selector label={t('field.label')} value={selected} options={options} placeholder={tc('none')} onChange={setChosen} />
      {showSuggestion && <Text type="supporting">{t('field.suggested', { name: suggestion.name })}</Text>}
    </ActionDialog>
  );
}
