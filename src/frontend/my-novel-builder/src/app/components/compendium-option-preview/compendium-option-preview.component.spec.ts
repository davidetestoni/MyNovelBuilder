import { CompendiumDto } from '../../types/dtos/compendium/compendium.dto';
import { CompendiumRecordType } from '../../types/enums/compendium-record-type';
import { CompendiumOptionPreviewComponent } from './compendium-option-preview.component';

describe('CompendiumOptionPreviewComponent', () => {
  const compendium = (): CompendiumDto => ({
    id: 'compendium',
    createdAt: '',
    updatedAt: '',
    name: 'World',
    description: '',
    records: [
      {
        id: 'without-image',
        name: 'No image',
        type: CompendiumRecordType.Other,
        imageUrl: null,
      },
      {
        id: 'first-image',
        name: 'First',
        type: CompendiumRecordType.Character,
        imageUrl: '/first.png',
      },
      {
        id: 'second-image',
        name: 'Second',
        type: CompendiumRecordType.Place,
        imageUrl: '/second.png',
      },
    ],
  });

  it('prioritizes available images and pads the three preview slots', () => {
    const component = new CompendiumOptionPreviewComponent();
    component.compendium = compendium();

    expect(component.previewImages).toEqual([
      '/first.png',
      '/second.png',
      null,
    ]);
  });

  it('does not reorder the source record collection', () => {
    const component = new CompendiumOptionPreviewComponent();
    component.compendium = compendium();
    const originalOrder = component.compendium.records.map(({ id }) => id);

    void component.previewImages;

    expect(component.compendium.records.map(({ id }) => id)).toEqual(
      originalOrder,
    );
  });
});
