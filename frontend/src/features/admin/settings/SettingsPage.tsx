/**
 * @input Nothing; loads the company-wide settings.
 * @output A form for the licence notice and the warranty window.
 * @position `/admin/settings`, inside the admin shell.
 */
import { Button } from '@astryxdesign/core/Button';
import { HStack } from '@astryxdesign/core/HStack';
import { useToast } from '@astryxdesign/core/Toast';
import { VStack } from '@astryxdesign/core/VStack';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { describeError } from '../../../shared/api/errorMessage';
import { ERROR_CODES } from '../../../shared/api/errors';
import { FormAlert } from '../../../shared/feedback/FormAlert';
import { ErrorState, LoadingState } from '../../../shared/feedback/States';
import { FormNumberInput } from '../../../shared/form/formFields';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../../shared/navigation/routeMeta';
import { SectionCard } from '../../../shared/page/SectionCard';

import { useSystemSettings, useUpdateSystemSettings, type SystemSettings } from './api';

/** Mirror backend SystemSettings.MinDays / MaxDays. */
const MIN_DAYS = 1;
const MAX_DAYS = 365;
const SKELETON_ROWS = 2;

const days = z.number({ error: ERROR_CODES.required }).int(ERROR_CODES.valueInvalid).min(MIN_DAYS, ERROR_CODES.valueInvalid).max(MAX_DAYS, ERROR_CODES.valueInvalid);
const schema = z.object({ licenceNoticeDays: days, warrantyWindowDays: days });
type FormValues = z.infer<typeof schema>;

const valuesOf = ({ licenceNoticeDays, warrantyWindowDays }: SystemSettings): FormValues => ({ licenceNoticeDays, warrantyWindowDays });

function SettingsForm({ settings }: { settings: SystemSettings }) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const showToast = useToast();
  const update = useUpdateSystemSettings();
  // After a conflict the fields the user did not touch follow the server, so a retry reverts nothing unseen.
  const { control, handleSubmit, reset } = useForm<FormValues>({
    resolver: zodResolver(schema),
    values: valuesOf(settings),
    resetOptions: { keepDirtyValues: true },
  });
  const onSubmit = handleSubmit((values) =>
    update.mutate(
      { ...values, version: settings.version },
      {
        onSuccess: (saved) => {
          reset(valuesOf(saved));
          showToast({ body: t('settings.saved') });
        },
      },
    ),
  );
  return (
    <form noValidate onSubmit={(event) => void onSubmit(event)}>
      <VStack gap={4}>
        <FormAlert message={update.error ? describeError(update.error) : null} />
        <FormNumberInput control={control} name="licenceNoticeDays" label={t('settings.licenceNoticeDays')} description={t('settings.licenceNoticeHint')} units={t('settings.unit')} isIntegerOnly isRequired />
        <FormNumberInput control={control} name="warrantyWindowDays" label={t('settings.warrantyWindowDays')} description={t('settings.warrantyWindowHint')} units={t('settings.unit')} isIntegerOnly isRequired />
        <HStack hAlign="end">
          <Button type="submit" label={tc('actions.save')} variant="primary" isLoading={update.isPending} />
        </HStack>
      </VStack>
    </form>
  );
}

export function SettingsPage() {
  const { t } = useTranslation('admin');
  const settings = useSystemSettings();
  return (
    <VStack gap={4}>
      <PageHeader icon={ROUTE_META.adminSettings.icon} titleOverride={t('settings.title')} />
      <SectionCard title={t('settings.periodsTitle')}>
        {settings.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {settings.isError && <ErrorState error={settings.error} onRetry={() => void settings.refetch()} />}
        {settings.data && <SettingsForm settings={settings.data} />}
      </SectionCard>
    </VStack>
  );
}
