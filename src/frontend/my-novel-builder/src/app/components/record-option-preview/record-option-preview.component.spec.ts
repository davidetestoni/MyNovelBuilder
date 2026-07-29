import { RecordOptionPreviewComponent } from './record-option-preview.component';

describe('RecordOptionPreviewComponent', () => {
  it('stores the supplied record name and image', () => {
    const component = new RecordOptionPreviewComponent();
    component.name = 'Aria';
    component.imageUrl = '/aria.png';

    expect(component.name).toBe('Aria');
    expect(component.imageUrl).toBe('/aria.png');
  });

  it('uses a null image by default', () => {
    const component = new RecordOptionPreviewComponent();
    component.name = 'Aria';

    expect(component.imageUrl).toBeNull();
  });
});
