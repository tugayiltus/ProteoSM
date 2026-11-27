/**
 * @input Template text, the variable names an event allows.
 * @output The text with sample values in place of `{{name}}`, and the names the server would refuse.
 * @position Used by the template editor preview. It mirrors the server renderer's placeholder shape (ADR-0016).
 */

const PLACEHOLDER = /\{\{(.*?)\}\}/g;
const VALID_NAME = /^[A-Za-z][A-Za-z0-9]*$/;

/** Fixed sample values, one per variable of `TemplateVariables`; the preview never reads real records. */
export const SAMPLE_VALUES: Readonly<Record<string, string>> = {
  ticketNumber: 'INC-2026-000123',
  subject: 'Yazıcı çalışmıyor',
  status: 'Açık',
  resolution: 'Toner değiştirildi.',
  completedAt: '27.11.2026 14:30',
  ticketUrl: 'https://destek.example.com/tickets/123',
  ratingUrl: 'https://destek.example.com/rate/123',
  recipientName: 'Ayşe Kaya',
  actionUrl: 'https://destek.example.com/action',
  expiresAt: '28.11.2026 09:00',
  slaMetric: 'Çözüm',
  dueAt: '27.11.2026 17:00',
  taskNumber: 'TSK-2026-000045',
  taskUrl: 'https://destek.example.com/tasks/45',
  teamName: 'Servis Masası',
  dueDate: '30.11.2026',
  changeNumber: 'CHG-2026-000012',
  changeUrl: 'https://destek.example.com/changes/12',
  windowStart: '29.11.2026 22:00',
  windowEnd: '30.11.2026 02:00',
  risk: 'Orta',
  reason: 'Güvenlik yaması',
  licenceName: 'Ofis lisansı',
  licenceUrl: 'https://destek.example.com/licences/7',
  softwareName: 'Ofis Paketi',
  expiresOn: '31.12.2026',
  problemNumber: 'PRB-2026-000003',
  problemUrl: 'https://destek.example.com/problems/3',
  impact: 'Yüksek',
};

export interface RenderedText {
  text: string;
  /** Placeholder contents the server refuses: a bad shape, or a name this event does not allow. */
  refused: string[];
}

export function previewTemplate(text: string, allowed: readonly string[]): RenderedText {
  const refused = new Set<string>();
  const rendered = text.replace(PLACEHOLDER, (whole: string, inner: string) => {
    const name = inner.trim();
    if (!VALID_NAME.test(name) || !allowed.includes(name)) {
      refused.add(name);
      return whole;
    }
    return SAMPLE_VALUES[name] ?? '';
  });
  return { text: rendered, refused: [...refused] };
}
