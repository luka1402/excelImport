import { useState, type SubmitEvent } from 'react';
import { t } from '../../../messages.sr';
import styles from './analysis.module.css';

interface Props {
  /** 0-based indeks prepoznatog/izabranog header reda; null kada header nije pronađen. */
  readonly headerRowIndex: number | null;
  readonly manual: boolean;
  readonly max: number;
  /** null vraća automatsko prepoznavanje. */
  readonly onChange: (headerRowIndex: number | null) => void;
}

export function HeaderRowControl({ headerRowIndex, manual, max, onChange }: Props) {
  const [draft, setDraft] = useState(headerRowIndex === null ? '' : (headerRowIndex + 1).toString());
  const [invalid, setInvalid] = useState(false);

  const submit = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    const row = Number(draft);
    if (!Number.isInteger(row) || row < 1 || row > max) {
      setInvalid(true);
      return;
    }
    setInvalid(false);
    onChange(row - 1);
  };

  const status = manual
    ? t('analysis.headerManual')
    : headerRowIndex === null
      ? t('analysis.noHeader')
      : t('analysis.headerDetected', { row: headerRowIndex + 1 });

  return (
    <form className={styles.headerRow} onSubmit={submit} noValidate>
      <div className={styles.field}>
        <label htmlFor="analysis-header-row">
          {t('analysis.headerRow')} {!manual && t('analysis.headerRowAuto')}
        </label>
        <input
          id="analysis-header-row"
          type="number"
          inputMode="numeric"
          min={1}
          max={max}
          value={draft}
          aria-invalid={invalid}
          aria-describedby="analysis-header-status"
          onChange={(event) => {
            setDraft(event.target.value);
          }}
        />
      </div>
      <button type="submit">{t('analysis.changeRow')}</button>
      {manual && (
        <button
          type="button"
          onClick={() => {
            onChange(null);
          }}
        >
          {t('analysis.useAuto')}
        </button>
      )}
      <p id="analysis-header-status" role={invalid ? 'alert' : 'status'}>
        {invalid ? t('analysis.headerRowRange', { max }) : status}
      </p>
    </form>
  );
}
