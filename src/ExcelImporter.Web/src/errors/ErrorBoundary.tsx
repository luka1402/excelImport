import { Component, type ErrorInfo, type ReactNode } from 'react';
import { t } from '../messages.sr';
import { ErrorState } from '../ui/states';

interface Props {
  readonly children: ReactNode;
}

interface State {
  readonly failed: boolean;
}

export class ErrorBoundary extends Component<Props, State> {
  override state: State = { failed: false };

  static getDerivedStateFromError(): State {
    return { failed: true };
  }

  override componentDidCatch(error: Error, info: ErrorInfo): void {
    console.error('Neobrađena greška u interfejsu', error, info.componentStack);
  }

  override render(): ReactNode {
    if (this.state.failed) {
      return (
        <ErrorState
          message={t('error.unexpected')}
          onRetry={() => {
            this.setState({ failed: false });
          }}
        />
      );
    }
    return this.props.children;
  }
}
