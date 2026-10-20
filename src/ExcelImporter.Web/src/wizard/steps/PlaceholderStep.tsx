import { t } from '../../messages.sr';
import { EmptyState } from '../../ui/states';

// Zamenjuje se pravim korakom u zadacima T12, T15, T18, T23/T24 i T29.
export function PlaceholderStep() {
  return <EmptyState message={t('common.notImplemented')} />;
}
