import { useEffect, useRef, useState, type DragEvent } from 'react';
import { uploadFile } from '../../../api/files';
import { t } from '../../../messages.sr';
import { EmptyState, ErrorState } from '../../../ui/states';
import { useWizard } from '../../WizardContext';
import { formatFileSize, uploadErrorMessage, validateFile } from './fileValidation';
import styles from './upload.module.css';

interface UploadingFile {
  readonly name: string;
  readonly size: number;
  readonly percent: number;
}

export function UploadStep() {
  const { data, updateData } = useWizard();
  const [uploading, setUploading] = useState<UploadingFile | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [dragging, setDragging] = useState(false);
  const controllerRef = useRef<AbortController | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(
    () => () => {
      controllerRef.current?.abort();
    },
    [],
  );

  const start = async (file: File) => {
    const invalid = validateFile(file);
    if (invalid) {
      setErrorMessage(t(invalid));
      return;
    }
    setErrorMessage(null);
    const controller = new AbortController();
    controllerRef.current = controller;
    setUploading({ name: file.name, size: file.size, percent: 0 });
    try {
      const { fileId } = await uploadFile(file, {
        signal: controller.signal,
        onProgress: (percent) => {
          setUploading({ name: file.name, size: file.size, percent });
        },
      });
      // Novi fajl poništava sve što je izvedeno iz prethodnog.
      updateData({
        fileId,
        fileName: file.name,
        fileSize: file.size,
        sheet: '',
        sheetIndex: null,
        headerRowIndex: null,
        analysisUsable: false,
      });
    } catch (error) {
      if (!(error instanceof DOMException && error.name === 'AbortError')) {
        setErrorMessage(uploadErrorMessage(error));
      }
    } finally {
      controllerRef.current = null;
      setUploading(null);
    }
  };

  const remove = () => {
    updateData({ fileId: '', fileName: '', fileSize: 0, sheet: '', sheetIndex: null, headerRowIndex: null, analysisUsable: false });
    setErrorMessage(null);
  };

  const onDrop = (event: DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    setDragging(false);
    const file = event.dataTransfer.files[0];
    if (file && !uploading) {
      void start(file);
    }
  };

  const busy = uploading !== null;
  const hasFile = data.fileId !== '';

  return (
    <div className={styles.upload}>
      <div
        className={[styles.dropzone, dragging ? styles.dragging : '', busy ? styles.disabled : ''].join(' ')}
        onDragOver={(event) => {
          event.preventDefault();
          if (!busy) {
            setDragging(true);
          }
        }}
        onDragLeave={() => {
          setDragging(false);
        }}
        onDrop={onDrop}
      >
        <p>{t('upload.dropzone')}</p>
        <input
          ref={inputRef}
          id="upload-file"
          className={styles.fileInput}
          type="file"
          accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
          disabled={busy}
          onChange={(event) => {
            const file = event.target.files?.[0];
            // Isti fajl mora moći ponovo da se izabere posle greške.
            event.target.value = '';
            if (file) {
              void start(file);
            }
          }}
        />
        <label htmlFor="upload-file" className={styles.selectButton} aria-disabled={busy}>
          {t('upload.select')}
        </label>
        <p className={styles.hint}>{t('upload.hint')}</p>
      </div>

      {uploading && (
        <div className={styles.progress}>
          <label htmlFor="upload-progress">{t('upload.progress', { percent: uploading.percent })}</label>
          <progress id="upload-progress" max={100} value={uploading.percent} />
          <button
            type="button"
            onClick={() => {
              controllerRef.current?.abort();
            }}
          >
            {t('upload.cancel')}
          </button>
        </div>
      )}

      {errorMessage && <ErrorState message={errorMessage} />}

      {hasFile && !busy && (
        <p className={styles.selected}>
          {t('upload.selected', { name: data.fileName, size: formatFileSize(data.fileSize) })}{' '}
          <button type="button" onClick={remove}>
            {t('upload.remove')}
          </button>
        </p>
      )}
      {!hasFile && !busy && !errorMessage && <EmptyState message={t('upload.empty')} />}
    </div>
  );
}
