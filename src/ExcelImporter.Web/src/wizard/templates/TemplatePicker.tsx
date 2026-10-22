import { useState } from 'react';
import { t } from '../../messages.sr';
import { EmptyState, ErrorState, LoadingState } from '../../ui/states';
import { useWizard } from '../WizardContext';
import { TemplateWarning } from './TemplateWarning';
import { fromApplyResponse } from './templateMapping';
import { useTemplateActions, useTemplates, type TemplateSummary } from './templatesApi';
import styles from './templates.module.css';

type Outcome =
  | { readonly kind: 'applied' }
  | { readonly kind: 'unusable'; readonly issues: readonly string[] }
  | { readonly kind: 'applyFailed' }
  | { readonly kind: 'deleteFailed' };

/** Izbor, primena i brisanje sačuvanih šablona; primena popunjava odredište, mapiranje, konstante i pravila. */
export function TemplatePicker() {
  const { data, updateData } = useWizard();
  const templates = useTemplates();
  const actions = useTemplateActions();
  const [busyId, setBusyId] = useState<number | null>(null);
  const [deletingId, setDeletingId] = useState<number | null>(null);
  const [outcome, setOutcome] = useState<Outcome | null>(null);

  const apply = async (template: TemplateSummary) => {
    setBusyId(template.id);
    setOutcome(null);
    try {
      const response = await actions.apply(template.id, data.fileId, data.sheetIndex, data.headerRowIndex);
      if (response.isUsable) {
        updateData(fromApplyResponse(template, response, data));
        setOutcome({ kind: 'applied' });
      } else {
        setOutcome({ kind: 'unusable', issues: response.schemaIssues });
      }
    } catch {
      setOutcome({ kind: 'applyFailed' });
    } finally {
      setBusyId(null);
    }
  };

  const remove = async (template: TemplateSummary) => {
    setBusyId(template.id);
    setOutcome(null);
    try {
      await actions.remove(template.id);
      if (data.appliedTemplate?.id === template.id) {
        updateData({ appliedTemplate: null });
      }
    } catch {
      setOutcome({ kind: 'deleteFailed' });
    } finally {
      setBusyId(null);
      setDeletingId(null);
    }
  };

  const renderList = (items: readonly TemplateSummary[]) => (
    <ul className={styles.list}>
      {items.map((template) => (
        <li key={template.id}>
          <span>{t('tpl.item', { name: template.name, table: `${template.schemaName}.${template.tableName}` })}</span>
          {deletingId === template.id ? (
            <span role="alertdialog" aria-label={t('tpl.confirmDelete', { name: template.name })} className={styles.actions}>
              {t('tpl.confirmDelete', { name: template.name })}
              <button
                type="button"
                disabled={busyId !== null}
                onClick={() => {
                  setDeletingId(null);
                }}
              >
                {t('tpl.cancel')}
              </button>
              <button
                type="button"
                disabled={busyId !== null}
                onClick={() => {
                  void remove(template);
                }}
              >
                {t('tpl.confirmDeleteYes')}
              </button>
            </span>
          ) : (
            <span className={styles.actions}>
              <button
                type="button"
                aria-label={t('tpl.applyFor', { name: template.name })}
                disabled={busyId !== null}
                onClick={() => {
                  void apply(template);
                }}
              >
                {t('tpl.apply')}
              </button>
              <button
                type="button"
                aria-label={t('tpl.deleteFor', { name: template.name })}
                disabled={busyId !== null}
                onClick={() => {
                  setDeletingId(template.id);
                  setOutcome(null);
                }}
              >
                {t('tpl.delete')}
              </button>
            </span>
          )}
        </li>
      ))}
    </ul>
  );

  return (
    <section aria-labelledby="templates-heading" className={styles.section}>
      <h2 id="templates-heading">{t('tpl.heading')}</h2>
      <p>{t('tpl.hint')}</p>
      {templates.isPending && <LoadingState message={t('tpl.loading')} />}
      {templates.isError && (
        <ErrorState
          message={t('tpl.err.load')}
          onRetry={() => {
            void templates.refetch();
          }}
        />
      )}
      {templates.data && (templates.data.length === 0 ? <EmptyState message={t('tpl.empty')} /> : renderList(templates.data))}

      {outcome?.kind === 'applied' && data.appliedTemplate && (
        <>
          <p role="status">{t('tpl.applied', { name: data.appliedTemplate.name })}</p>
          <TemplateWarning template={data.appliedTemplate} />
        </>
      )}
      {outcome?.kind === 'unusable' && (
        <div role="alert" className={styles.invalid}>
          <p>{t('tpl.err.unusable')}</p>
          <ul>
            {outcome.issues.map((issue) => (
              <li key={issue}>{issue}</li>
            ))}
          </ul>
        </div>
      )}
      {outcome?.kind === 'applyFailed' && <p role="alert" className={styles.invalid}>{t('tpl.err.apply')}</p>}
      {outcome?.kind === 'deleteFailed' && <p role="alert" className={styles.invalid}>{t('tpl.err.delete')}</p>}
    </section>
  );
}
