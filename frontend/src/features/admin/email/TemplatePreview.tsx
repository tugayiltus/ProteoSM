/**
 * @input The subject and body typed in the template editor, and the variables the event allows.
 * @output The subject and body with sample values, and a warning for placeholders the server would refuse. It sends nothing.
 * @position Under the fields of the email template editor.
 */
import { Banner } from '@astryxdesign/core/Banner';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';

import { previewTemplate } from './sampleRender';

const LINE_BREAK = /\r?\n/;
const NON_BREAKING_SPACE = '\u00a0';

export function TemplatePreview({ subject, body, allowedVariables }: { subject: string; body: string; allowedVariables: readonly string[] }) {
  const { t } = useTranslation('admin');
  const renderedSubject = previewTemplate(subject, allowedVariables);
  const renderedBody = previewTemplate(body, allowedVariables);
  const refused = [...new Set([...renderedSubject.refused, ...renderedBody.refused])];
  return (
    <VStack gap={2} role="group" aria-label={t('emailTemplates.preview.title')}>
      <Text type="label">{t('emailTemplates.preview.title')}</Text>
      <Text type="supporting">{t('emailTemplates.preview.hint')}</Text>
      {refused.length > 0 && <Banner status="warning" title={t('emailTemplates.preview.refused', { names: refused.join(', ') })} />}
      <Text type="supporting">{t('emailTemplates.fields.subject')}</Text>
      <Text type="label" data-testid="template-preview-subject">
        {renderedSubject.text}
      </Text>
      <Text type="supporting">{t('emailTemplates.fields.body')}</Text>
      <VStack gap={0} data-testid="template-preview-body">
        {renderedBody.text.split(LINE_BREAK).map((line, index) => (
          <Text key={index} display="block">
            {line === '' ? NON_BREAKING_SPACE : line}
          </Text>
        ))}
      </VStack>
    </VStack>
  );
}
