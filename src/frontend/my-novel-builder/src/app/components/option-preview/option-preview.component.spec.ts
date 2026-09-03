import { OptionPreviewComponent } from './option-preview.component';

describe('OptionPreviewComponent', () => {
  it('stores the supplied label and image', () => {
    const component = new OptionPreviewComponent();
    component.label = 'Aria';
    component.imageUrl = '/aria.png';

    expect(component.label).toBe('Aria');
    expect(component.imageUrl).toBe('/aria.png');
  });

  it('uses generic record defaults when optional inputs are omitted', () => {
    const component = new OptionPreviewComponent();
    component.label = 'Aria';

    expect(component.imageUrl).toBeNull();
    expect(component.fallbackIcon).toBe('icon-compendium');
  });
});
