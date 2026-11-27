/**
 * @input Nothing; reads the outgoing mail settings.
 * @output "E-posta gönderimi": which sender is in use, the panel settings form with the Google preset, a test send,
 *   "Kullanıma al" and the way back to the `.env` values.
 * @position `/admin/email-sending`, admin only.
 */
import { Banner } from '@astryxdesign/core/Banner';
import { Button } from '@astryxdesign/core/Button';
import { HStack } from '@astryxdesign/core/HStack';
import { Text } from '@astryxdesign/core/Text';
import { useToast } from '@astryxdesign/core/Toast';
import { VStack } from '@astryxdesign/core/VStack';
import { zodResolver } from '@hookform/resolvers/zod';
import { useState } from 'react';
import { useForm, useWatch, type Path } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { applyServerErrors, describeError, isHandledByFields, translateErrorCode } from '../../../shared/api/errorMessage';
import { ERROR_CODES } from '../../../shared/api/errors';
import { ConfirmDialog } from '../../../shared/feedback/ConfirmDialog';
import { FormAlert } from '../../../shared/feedback/FormAlert';
import { ErrorState, LoadingState } from '../../../shared/feedback/States';
import { UnsavedChangesGuard } from '../../../shared/feedback/UnsavedChangesGuard';
import { FormNumberInput, FormRadioList, FormTextInput } from '../../../shared/form/formFields';
import { formatDateTime } from '../../../shared/lib/format';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../../shared/navigation/routeMeta';
import { SectionCard } from '../../../shared/page/SectionCard';
import { USER_EMAIL_MAX_LENGTH } from '../users/userSchema';

import {
  useActivateEmailSettings,
  useEmailSettings,
  useSaveEmailSettings,
  useTestEmailSettings,
  useUseEnvironmentEmail,
  type EmailProvider,
  type EmailSecurity,
  type EmailSettings,
} from './sendingApi';

/** Mirror backend `EmailSettings` limits. */
const SENDER_NAME_MAX_LENGTH = 100;
const HOST_MAX_LENGTH = 253;
const PASSWORD_MAX_LENGTH = 512;
const MIN_PORT = 1;
const MAX_PORT = 65535;

const PROVIDERS = ['google', 'smtp'] as const satisfies readonly EmailProvider[];
const SECURITIES = ['start_tls', 'ssl_tls', 'none'] as const satisfies readonly EmailSecurity[];
const GOOGLE: EmailProvider = 'google';
const NO_SECURITY: EmailSecurity = 'none';

/** The server replaces these values for Google, so the form shows them locked. */
const GOOGLE_PRESET = { host: 'smtp.gmail.com', port: 587, security: 'start_tls' } as const satisfies { host: string; port: number; security: EmailSecurity };

/** The usual port of each security choice; a change of security suggests it. */
const SECURITY_PORTS = { start_tls: 587, ssl_tls: 465, none: 25 } as const satisfies Record<EmailSecurity, number>;

/** The fields that can carry a server validation error. */
const SERVER_FIELDS = ['senderName', 'senderAddress', 'host', 'port', 'userName', 'password'] as const satisfies readonly Path<FormValues>[];

const isEmail = (value: string) => z.email().safeParse(value).success;

const schema = z
  .object({
    provider: z.enum(PROVIDERS),
    senderName: z.string().trim().max(SENDER_NAME_MAX_LENGTH, ERROR_CODES.tooLong),
    senderAddress: z.string().trim().min(1, ERROR_CODES.required).max(USER_EMAIL_MAX_LENGTH, ERROR_CODES.tooLong).pipe(z.email(ERROR_CODES.invalidEmail)),
    host: z.string().trim().min(1, ERROR_CODES.required).max(HOST_MAX_LENGTH, ERROR_CODES.tooLong),
    port: z.number({ error: ERROR_CODES.required }).int(ERROR_CODES.valueInvalid).min(MIN_PORT, ERROR_CODES.valueInvalid).max(MAX_PORT, ERROR_CODES.valueInvalid),
    security: z.enum(SECURITIES),
    userName: z.string().trim().max(USER_EMAIL_MAX_LENGTH, ERROR_CODES.tooLong),
    password: z.string().max(PASSWORD_MAX_LENGTH, ERROR_CODES.tooLong),
  })
  .superRefine((values, context) => {
    // A login over an unencrypted connection would send the password in clear text.
    if (values.security === NO_SECURITY && values.userName !== '') {
      context.addIssue({ code: 'custom', path: ['userName'], message: ERROR_CODES.emailSettingsPlainTextLogin });
    }
    // Google signs in with the account address.
    if (values.provider === GOOGLE && !isEmail(values.userName)) {
      context.addIssue({ code: 'custom', path: ['userName'], message: values.userName === '' ? ERROR_CODES.required : ERROR_CODES.invalidEmail });
    }
  });
type FormValues = z.infer<typeof schema>;

/** The password field always starts empty: the API never returns the stored password. */
const valuesOf = (settings: EmailSettings): FormValues => ({
  provider: settings.provider,
  senderName: settings.senderName ?? '',
  senderAddress: settings.senderAddress,
  host: settings.host,
  port: settings.port,
  security: settings.security,
  userName: settings.userName ?? '',
  password: '',
});

const emptyToNull = (value: string) => (value === '' ? null : value);

function currentSender(settings: EmailSettings, t: ReturnType<typeof useTranslation<'admin'>>['t']): string {
  if (settings.isPickup) {
    return t('emailSending.current.pickup');
  }
  if (!settings.isActive) {
    return t('emailSending.current.environment', { from: settings.environmentFrom, host: settings.environmentHost });
  }
  if (settings.lastTestedAt === null || settings.lastTestSucceeded === null) {
    return t('emailSending.current.panel');
  }
  const date = formatDateTime(settings.lastTestedAt);
  return settings.lastTestSucceeded ? t('emailSending.current.panelTestOk', { date }) : t('emailSending.current.panelTestFailed', { date });
}

function SendingStatus({ settings }: { settings: EmailSettings }) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const useEnvironment = useUseEnvironmentEmail();
  const [confirming, setConfirming] = useState(false);
  return (
    <SectionCard title={t('emailSending.statusTitle')}>
      <VStack gap={3}>
        {settings.isPickup && <Banner status="info" title={t('emailSending.pickupBanner')} />}
        <Text type="label">{currentSender(settings, t)}</Text>
        {settings.isActive && (
          <HStack>
            <Button label={t('emailSending.useEnvironment')} variant="secondary" onClick={() => setConfirming(true)} />
          </HStack>
        )}
      </VStack>
      {confirming && (
        <ConfirmDialog
          title={t('emailSending.useEnvironment')}
          message={t('emailSending.useEnvironmentConfirm')}
          confirmLabel={tc('actions.confirm')}
          variant="primary"
          isPending={useEnvironment.isPending}
          error={useEnvironment.error}
          onConfirm={() => useEnvironment.mutate({ version: settings.version }, { onSuccess: () => setConfirming(false) })}
          onCancel={() => setConfirming(false)}
        />
      )}
    </SectionCard>
  );
}

function TestResult({ ok, errorCode, detail }: { ok: boolean; errorCode: string | null; detail: string | null }) {
  const { t } = useTranslation('admin');
  if (ok) {
    return <Banner status="success" title={t('emailSending.testOk')} />;
  }
  return (
    <Banner
      status="error"
      title={translateErrorCode(errorCode ?? ERROR_CODES.unexpected)}
      description={detail === null ? undefined : t('emailSending.testDetail', { detail })}
    />
  );
}

function SettingsForm({ settings }: { settings: EmailSettings }) {
  const { t } = useTranslation('admin');
  const showToast = useToast();
  const save = useSaveEmailSettings();
  const test = useTestEmailSettings();
  const activate = useActivateEmailSettings();
  // After a conflict the fields the user did not touch follow the server, so a retry reverts nothing unseen.
  const { control, handleSubmit, reset, setError, setValue, formState } = useForm<FormValues>({
    resolver: zodResolver(schema),
    values: valuesOf(settings),
    resetOptions: { keepDirtyValues: true },
  });
  const provider = useWatch({ control, name: 'provider' });
  const userName = useWatch({ control, name: 'userName' });
  const isGoogle = provider === GOOGLE;
  const hasUserName = userName.trim() !== '';
  const isDirty = formState.isDirty;

  const onProviderChange = (next: string) => {
    if (next !== GOOGLE) {
      return;
    }
    setValue('host', GOOGLE_PRESET.host, { shouldDirty: true });
    setValue('port', GOOGLE_PRESET.port, { shouldDirty: true });
    setValue('security', GOOGLE_PRESET.security, { shouldDirty: true });
  };
  const onSecurityChange = (next: string) => {
    if (next in SECURITY_PORTS) {
      setValue('port', SECURITY_PORTS[next as EmailSecurity], { shouldDirty: true, shouldValidate: true });
    }
  };

  const onSubmit = handleSubmit((values) => {
    const server = values.provider === GOOGLE ? GOOGLE_PRESET : values;
    save.mutate(
      {
        provider: values.provider,
        senderName: emptyToNull(values.senderName),
        senderAddress: values.senderAddress,
        host: server.host,
        port: server.port,
        security: server.security,
        userName: emptyToNull(values.userName),
        password: emptyToNull(values.password),
        version: settings.version,
      },
      {
        onSuccess: (saved) => {
          test.reset();
          reset(valuesOf(saved));
          showToast({ body: t('emailSending.saved') });
        },
        onError: (error) => applyServerErrors(error, setError, SERVER_FIELDS),
      },
    );
  });

  const saveMessage = save.error && !isHandledByFields(save.error, SERVER_FIELDS) ? describeError(save.error) : null;
  const securityOptions = SECURITIES.map((value) => {
    const isBlocked = value === NO_SECURITY && hasUserName;
    return { value, label: t(`emailSending.securities.${value}`), isDisabled: isBlocked, description: isBlocked ? t('emailSending.noneNeedsNoUser') : undefined };
  });

  return (
    <SectionCard title={t('emailSending.settingsTitle')}>
      <UnsavedChangesGuard when={isDirty} />
      <form noValidate onSubmit={(event) => void onSubmit(event)}>
        <VStack gap={3}>
          <FormAlert message={saveMessage} />
          <FormRadioList
            control={control}
            name="provider"
            label={t('emailSending.fields.provider')}
            orientation="horizontal"
            options={PROVIDERS.map((value) => ({ value, label: t(`emailSending.providers.${value}`) }))}
            onValueChange={onProviderChange}
          />
          <FormTextInput control={control} name="senderName" label={t('emailSending.fields.senderName')} autoComplete="off" />
          <FormTextInput control={control} name="senderAddress" label={t('emailSending.fields.senderAddress')} type="email" autoComplete="off" isRequired />
          <FormTextInput
            control={control}
            name="host"
            label={t('emailSending.fields.host')}
            autoComplete="off"
            isRequired
            isDisabled={isGoogle}
            disabledMessage={isGoogle ? t('emailSending.googlePreset') : undefined}
          />
          <FormNumberInput
            control={control}
            name="port"
            label={t('emailSending.fields.port')}
            min={MIN_PORT}
            max={MAX_PORT}
            isIntegerOnly
            isDisabled={isGoogle}
            disabledMessage={isGoogle ? t('emailSending.googlePreset') : undefined}
          />
          <FormRadioList
            control={control}
            name="security"
            label={t('emailSending.fields.security')}
            options={securityOptions}
            isDisabled={isGoogle}
            disabledMessage={isGoogle ? t('emailSending.googlePreset') : undefined}
            onValueChange={onSecurityChange}
          />
          <FormTextInput control={control} name="userName" label={t('emailSending.fields.userName')} autoComplete="off" />
          <FormTextInput
            control={control}
            name="password"
            label={t('emailSending.fields.password')}
            type="password"
            autoComplete="new-password"
            placeholder={settings.hasPassword ? t('emailSending.passwordStored') : undefined}
            description={isGoogle ? t('emailSending.googleHint') : undefined}
          />
          {test.data && <TestResult {...test.data} />}
          {test.error && <Banner status="error" title={describeError(test.error)} />}
          {activate.error && <Banner status="error" title={describeError(activate.error)} />}
          <HStack gap={2} wrap="wrap">
            <Button type="submit" label={t('emailSending.save')} variant="primary" isLoading={save.isPending} />
            <Button label={t('emailSending.test')} variant="secondary" isDisabled={isDirty} isLoading={test.isPending} onClick={() => test.mutate()} />
            {!settings.isActive && (
              <Button
                label={t('emailSending.activate')}
                variant="secondary"
                isDisabled={!settings.isTested || isDirty}
                isLoading={activate.isPending}
                onClick={() => activate.mutate({ version: settings.version })}
              />
            )}
          </HStack>
          {isDirty && <Text type="supporting">{t('emailSending.testNeedsSave')}</Text>}
          {!isDirty && !settings.isActive && !settings.isTested && <Text type="supporting">{t('emailSending.activateNeedsTest')}</Text>}
        </VStack>
      </form>
    </SectionCard>
  );
}

/** The admin page that sets, tests and switches the outgoing mail sender. */
export function EmailSendingPage() {
  const { t } = useTranslation('admin');
  const settings = useEmailSettings();
  return (
    <VStack gap={4}>
      <PageHeader icon={ROUTE_META.adminEmailSending.icon} description={t('emailSending.intro')} />
      {settings.isPending && <LoadingState />}
      {settings.isError && <ErrorState error={settings.error} onRetry={() => void settings.refetch()} />}
      {settings.data && (
        <>
          <SendingStatus settings={settings.data} />
          <SettingsForm settings={settings.data} />
        </>
      )}
    </VStack>
  );
}
