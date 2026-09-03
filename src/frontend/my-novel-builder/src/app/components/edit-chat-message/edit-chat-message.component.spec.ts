import { TestBed } from '@angular/core/testing';
import {
  DynamicDialogConfig,
  DynamicDialogRef,
} from 'primeng/dynamicdialog';
import { EditChatMessageComponent } from './edit-chat-message.component';

describe('EditChatMessageComponent workflow', () => {
  let component: EditChatMessageComponent;
  let dialogRef: jasmine.SpyObj<DynamicDialogRef>;
  let config: DynamicDialogConfig;

  const createComponent = (): EditChatMessageComponent =>
    TestBed.runInInjectionContext(() => new EditChatMessageComponent());

  beforeEach(() => {
    dialogRef = jasmine.createSpyObj<DynamicDialogRef>('DynamicDialogRef', [
      'close',
    ]);
    config = { data: { text: 'Original text' } };

    TestBed.configureTestingModule({
      providers: [
        { provide: DynamicDialogRef, useValue: dialogRef },
        { provide: DynamicDialogConfig, useValue: config },
      ],
    });

    component = createComponent();
  });

  it('loads the configured message text on initialization', () => {
    component.ngOnInit();

    expect(component.messageText).toBe('Original text');
  });

  it('keeps an empty value when dialog data is absent', () => {
    config.data = undefined;
    component = createComponent();

    component.ngOnInit();

    expect(component.messageText).toBe('');
  });

  it('returns the edited text when saved', () => {
    component.messageText = 'Edited text';

    component.save();

    expect(dialogRef.close).toHaveBeenCalledOnceWith('Edited text');
  });

  it('closes without a result when cancelled', () => {
    component.cancel();

    expect(dialogRef.close).toHaveBeenCalledOnceWith();
  });
});
