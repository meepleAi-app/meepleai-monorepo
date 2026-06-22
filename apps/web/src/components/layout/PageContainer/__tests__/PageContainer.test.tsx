import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import {
  DetailPageContainer,
  FormPageContainer,
  HubPageContainer,
  SettingsPageContainer,
} from '../PageContainer';

describe('PageContainer primitives', () => {
  describe('HubPageContainer', () => {
    it('renders children with max-w-[1440px] default', () => {
      const { container } = render(
        <HubPageContainer>
          <span data-testid="child">hub</span>
        </HubPageContainer>
      );
      const wrapper = container.firstElementChild as HTMLElement;
      expect(wrapper).toBeInTheDocument();
      expect(wrapper.className).toContain('max-w-[1440px]');
      expect(wrapper.className).toContain('mx-auto');
    });

    it('merges custom className with defaults', () => {
      const { container } = render(
        <HubPageContainer className="gap-8 pb-24">
          <span>x</span>
        </HubPageContainer>
      );
      const wrapper = container.firstElementChild as HTMLElement;
      expect(wrapper.className).toContain('gap-8');
      expect(wrapper.className).toContain('pb-24');
      expect(wrapper.className).toContain('max-w-[1440px]');
    });

    it('passes through HTML attributes (data-slot, id)', () => {
      const { container } = render(
        <HubPageContainer data-slot="hub-page" id="my-hub">
          <span>x</span>
        </HubPageContainer>
      );
      const wrapper = container.firstElementChild as HTMLElement;
      expect(wrapper.getAttribute('data-slot')).toBe('hub-page');
      expect(wrapper.id).toBe('my-hub');
    });
  });

  describe('DetailPageContainer', () => {
    it('uses max-w-4xl (896px)', () => {
      const { container } = render(
        <DetailPageContainer>
          <span>detail</span>
        </DetailPageContainer>
      );
      const wrapper = container.firstElementChild as HTMLElement;
      expect(wrapper.className).toContain('max-w-4xl');
    });
  });

  describe('FormPageContainer', () => {
    it('uses max-w-2xl (672px)', () => {
      const { container } = render(
        <FormPageContainer>
          <span>form</span>
        </FormPageContainer>
      );
      const wrapper = container.firstElementChild as HTMLElement;
      expect(wrapper.className).toContain('max-w-2xl');
    });
  });

  describe('SettingsPageContainer', () => {
    it('uses max-w-3xl (768px)', () => {
      const { container } = render(
        <SettingsPageContainer>
          <span>settings</span>
        </SettingsPageContainer>
      );
      const wrapper = container.firstElementChild as HTMLElement;
      expect(wrapper.className).toContain('max-w-3xl');
    });
  });
});
