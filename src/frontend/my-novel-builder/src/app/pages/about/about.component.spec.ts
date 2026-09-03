import { AboutComponent } from './about.component';

describe('AboutComponent', () => {
  it('exposes the application version from the frontend package metadata', () => {
    const component = new AboutComponent();

    expect(component.version).toMatch(/^\d+\.\d+\.\d+/);
  });
});
