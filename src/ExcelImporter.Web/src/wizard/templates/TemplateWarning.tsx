import { t } from '../../messages.sr';
import type { AppliedTemplate } from '../types';
import styles from './templates.module.css';

/** Upozorenje o razlikama između kolona fajla i fajla iz kog je šablon nastao; ništa ne prikazuje kada nema razlika. */
export function TemplateWarning({ template }: { readonly template: AppliedTemplate }) {
  if (template.newColumns.length === 0 && template.missingColumns.length === 0) {
    return null;
  }
  return (
    <div role="status" className={styles.warning}>
      <p>
        <strong>⚠ {t('tpl.warn.title', { name: template.name })}</strong>
      </p>
      {template.newColumns.length > 0 && <p>{t('tpl.warn.new', { columns: template.newColumns.join(', ') })}</p>}
      {template.missingColumns.length > 0 && (
        <p>{t('tpl.warn.missing', { columns: template.missingColumns.join(', ') })}</p>
      )}
      <p>{t('tpl.warn.check')}</p>
    </div>
  );
}
