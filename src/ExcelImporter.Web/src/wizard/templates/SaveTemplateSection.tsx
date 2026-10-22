import { useId, useState, type SubmitEvent } from 'react';
import { ApiError } from '../../api/apiError';
import { t } from '../../messages.sr';
import { useWizard } from '../WizardContext';
import { useFileAnalysis } from '../steps/analysis/useFileAnalysis';
import { toSaveRequest } from './templateMapping';
import { useTemplateActions } from './templatesApi';
import styles from './templates.module.css';

const maxNameLength = 200;

type Result =
  | { readonly kind: 'saved'; readonly name: string }
  | { readonly kind: 'failed'; readonly message: string };

function failureMessage(error: unknown): string {
  if (error instanceof ApiError) {
    // Ime koje već postoji server opisuje porukom namenjenom korisniku.
    if (error.status === 409 && error.problem?.title) {
      return error.problem.title;
    }
    if (error.status === 400) {
      return t('tpl.save.errInvalid');
    }
  }
  return t('tpl.save.err');
}

/** Čuva trenutno mapiranje, konstante i pravila kao šablon. */
export function SaveTemplateSection() {
  const { data } = useWizard();
  const actions = useTemplateActions();
  const { state } = useFileAnalysis({
    fileId: data.fileId,
    sheetIndex: data.sheetIndex,
    headerRowIndex: data.headerRowIndex,
  });
  const nameId = useId();
  const errorId = useId();
  const [name, setName] = useState('');
  const [nameError, setNameError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [result, setResult] = useState<Result | null>(null);

  const analysis = state.status === 'ready' ? state.analysis : null;

  const submit = async (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    const trimmed = name.trim();
    if (trimmed === '') {
      setNameError(t('tpl.save.nameRequired'));
      return;
    }
    if (trimmed.length > maxNameLength) {
      setNameError(t('tpl.save.nameTooLong'));
      return;
    }
    if (!analysis) {
      setResult({ kind: 'failed', message: t('tpl.save.errFile') });
      return;
    }
    setNameError(null);
    setSaving(true);
    setResult(null);
    try {
      await actions.save(
        toSaveRequest(data, {
          name: trimmed,
          sourceColumns: analysis.columns.map((column) => column.name),
          headerRow: data.headerRowIndex ?? analysis.headerRowIndex ?? 0,
        }),
      );
      setResult({ kind: 'saved', name: trimmed });
      setName('');
    } catch (error) {
      setResult({ kind: 'failed', message: failureMessage(error) });
    } finally {
      setSaving(false);
    }
  };

  return (
    <section aria-labelledby="save-template-heading" className={styles.section}>
      <h2 id="save-template-heading">{t('tpl.save.heading')}</h2>
      <p>{t('tpl.save.hint')}</p>
      <form
        className={styles.saveRow}
        noValidate
        onSubmit={(event) => {
          void submit(event);
        }}
      >
        <div className={styles.field}>
          <label htmlFor={nameId}>{t('tpl.save.name')}</label>
          <input
            id={nameId}
            type="text"
            value={name}
            aria-invalid={nameError !== null}
            aria-describedby={nameError ? errorId : undefined}
            onChange={(event) => {
              setName(event.target.value);
              setNameError(null);
            }}
          />
        </div>
        <button type="submit" disabled={saving}>
          {saving ? t('tpl.save.saving') : t('tpl.save.button')}
        </button>
      </form>
      {nameError && (
        <p id={errorId} role="alert" className={styles.invalid}>
          {nameError}
        </p>
      )}
      {result?.kind === 'saved' && <p role="status">{t('tpl.save.done', { name: result.name })}</p>}
      {result?.kind === 'failed' && (
        <p role="alert" className={styles.invalid}>
          {result.message}
        </p>
      )}
    </section>
  );
}
