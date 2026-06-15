import { forwardRef, type ComponentPropsWithoutRef, type ReactNode } from 'react';

import { cn } from '@/lib/utils';

const PADDING_DEFAULT = 'px-4 py-6 sm:px-8 sm:py-8';

interface PageContainerProps extends ComponentPropsWithoutRef<'div'> {
  children: ReactNode;
}

const HubPageContainer = forwardRef<HTMLDivElement, PageContainerProps>(
  ({ className, children, ...rest }, ref) => (
    <div
      ref={ref}
      className={cn('mx-auto flex w-full flex-col max-w-[1440px]', PADDING_DEFAULT, className)}
      {...rest}
    >
      {children}
    </div>
  )
);
HubPageContainer.displayName = 'HubPageContainer';

const DetailPageContainer = forwardRef<HTMLDivElement, PageContainerProps>(
  ({ className, children, ...rest }, ref) => (
    <div
      ref={ref}
      className={cn('mx-auto flex w-full flex-col max-w-4xl', PADDING_DEFAULT, className)}
      {...rest}
    >
      {children}
    </div>
  )
);
DetailPageContainer.displayName = 'DetailPageContainer';

const FormPageContainer = forwardRef<HTMLDivElement, PageContainerProps>(
  ({ className, children, ...rest }, ref) => (
    <div
      ref={ref}
      className={cn('mx-auto flex w-full flex-col max-w-2xl', PADDING_DEFAULT, className)}
      {...rest}
    >
      {children}
    </div>
  )
);
FormPageContainer.displayName = 'FormPageContainer';

const SettingsPageContainer = forwardRef<HTMLDivElement, PageContainerProps>(
  ({ className, children, ...rest }, ref) => (
    <div
      ref={ref}
      className={cn('mx-auto flex w-full flex-col max-w-3xl', PADDING_DEFAULT, className)}
      {...rest}
    >
      {children}
    </div>
  )
);
SettingsPageContainer.displayName = 'SettingsPageContainer';

export { HubPageContainer, DetailPageContainer, FormPageContainer, SettingsPageContainer };
export type { PageContainerProps };
