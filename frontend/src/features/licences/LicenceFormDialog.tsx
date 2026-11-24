/**
 * @input A licence to edit, or null for a new one, and a close callback.
 * @output The licence form; software and kind are fixed after creation.
 * @position Opened from the licence list and detail.
 */
import { TextInput } from '@astryxdesign/core/TextInput';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { applyServerErrors, describeError, isHandledByFields } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { FormDialog } from '../../shared/feedback/FormDialog';
import { FormDateInput, FormNumberInput, FormSelector, FormTextArea, FormTextInput, type SelectOption } from '../../shared/form/formFields';
import { useDebouncedValue } from '../../shared/lib/useDebouncedValue';
import { withCurrentOption } from '../../shared/reference/referenceOptions';
import { useCloseOnConflict } from '../changes/useCloseOnConflict';
import { useReferenceTeams } from '../tickets/api';

import { useCreateLicence, useSoftwareSearch, useUpdateLicence, type LicenceDetail } from './api';

/** Mirrors backend LicenceLimits. */
const NAME_MAX_LENGTH = 150;
const NOTES_MAX_LENGTH = 2000;
const MIN_QUANTITY = 1;
const MAX_QUANTITY = 100_000;
const SEARCH_DEBOUNCE_MS = 300;

const schema = z.object({
  softwareId: z.string().min(1, ERROR_CODES.required),
  name: z.string().trim().min(1, ERROR_CODES.required).max(NAME_MAX_LENGTH, ERROR_CODES.tooLong),
  kind: z.enum(['per_user', 'per_device']),
  quantity: z.number({ error: ERROR_CODES.required }).int().min(MIN_QUANTITY).max(MAX_QUANTITY),
  ownerTeamId: z.string().min(1, ERROR_CODES.required),
  expiresOn: z.string(),
  notes: z.string().trim().max(NOTES_MAX_LENGTH, ERROR_CODES.tooLong),
});
type FormValues = z.infer<typeof schema>;
const FORM_FIELDS = ['softwareId', 'name', 'kind', 'quantity', 'ownerTeamId', 'expiresOn', 'notes'] as const;
const CODE_FIELDS = {
  [ERROR_CODES.licenceQuantityBelowUse]: 'quantity',
  [ERROR_CODES.licenceOwnerTeamInactive]: 'ownerTeamId',
  [ERROR_CODES.softwareInactive]: 'softwareId',
} as const;

export function LicenceFormDialog({ licence, onClose }: { licence: LicenceDetail | null; onClose: () => void }) {
  const { t } = useTranslation('licences');
  const { t: tc } = useTranslation();
  const create = useCreateLicence();
  const update = useUpdateLicence();
  const mutation = licence === null ? create : update;
  const teams = useReferenceTeams(true);
  // The version the user saw when opening the dialog; a background refetch must not skip the conflict check.
  const [openedVersion] = useState(licence?.version ?? 0);
  const closeOnConflict = useCloseOnConflict(onClose);
  const [softwareSearch, setSoftwareSearch] = useState('');
  const [chosenSoftware, setChosenSoftware] = useState<SelectOption | null>(null);
  const software = useSoftwareSearch(useDebouncedValue(softwareSearch.trim(), SEARCH_DEBOUNCE_MS));
  const teamOptions = useMemo(
    () => withCurrentOption((teams.data ?? []).map((x) => ({ value: x.id, label: x.name })), licence?.ownerTeamId, licence?.ownerTeamName),
    [teams.data, licence],
  );
  const softwareOptions = useMemo(
    () => withCurrentOption((software.data?.items ?? []).map((x) => ({ value: x.id, label: `${x.vendor} ${x.name}` })), chosenSoftware?.value, chosenSoftware?.label),
    [software.data, chosenSoftware],
  );
  const {
    control,
    handleSubmit,
    setError,
    formState: { isDirty },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      softwareId: licence?.softwareId ?? '',
      name: licence?.name ?? '',
      kind: licence?.kind ?? 'per_user',
      quantity: licence?.quantity ?? MIN_QUANTITY,
      ownerTeamId: licence?.ownerTeamId ?? '',
      expiresOn: licence?.expiresOn ?? '',
      notes: licence?.notes ?? '',
    },
  });
  const onSubmit = handleSubmit(({ softwareId, kind, expiresOn, notes, ...rest }) => {
    const body = { ...rest, expiresOn: expiresOn === '' ? null : expiresOn, notes: notes === '' ? null : notes };
    const onError = (error: unknown) => {
      closeOnConflict(error);
      applyServerErrors(error, setError, FORM_FIELDS, CODE_FIELDS);
    };
    const callbacks = { onSuccess: onClose, onError };
    if (licence === null) {
      create.mutate({ softwareId, kind, ...body }, callbacks);
    } else {
      update.mutate({ id: licence.id, version: openedVersion, ...body }, callbacks);
    }
  });
  const alert = mutation.error && !isHandledByFields(mutation.error, FORM_FIELDS, CODE_FIELDS) ? describeError(mutation.error) : null;
  return (
    <FormDialog
      isDirty={isDirty}
      title={licence === null ? t('form.new') : t('form.edit')}
      submitLabel={licence === null ? tc('actions.create') : tc('actions.save')}
      isPending={mutation.isPending}
      alert={alert}
      onSubmit={(event) => void onSubmit(event)}
      onClose={onClose}
    >
      {licence === null && (
        <>
          <TextInput type="search" label={t('software.search')} value={softwareSearch} onChange={setSoftwareSearch} />
          <FormSelector
            control={control}
            name="softwareId"
            label={t('columns.software')}
            options={softwareOptions}
            placeholder={tc('none')}
            onValueChange={(value) => setChosenSoftware(softwareOptions.find((option) => option.value === value) ?? null)}
            isRequired
          />
          <FormSelector
            control={control}
            name="kind"
            label={t('columns.kind')}
            options={[
              { value: 'per_user', label: t('kinds.per_user') },
              { value: 'per_device', label: t('kinds.per_device') },
            ]}
            isRequired
          />
        </>
      )}
      <FormTextInput control={control} name="name" label={t('columns.name')} autoComplete="off" isRequired />
      <FormNumberInput control={control} name="quantity" label={t('form.quantity')} min={MIN_QUANTITY} max={MAX_QUANTITY} isRequired />
      <FormSelector control={control} name="ownerTeamId" label={t('columns.team')} options={teamOptions} placeholder={tc('none')} isRequired />
      <FormDateInput control={control} name="expiresOn" label={t('columns.end')} />
      <FormTextArea control={control} name="notes" label={t('form.notes')} />
    </FormDialog>
  );
}
