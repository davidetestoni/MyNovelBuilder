import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { AliasSuggestionsComponent } from './alias-suggestions.component';

describe('AliasSuggestionsComponent', () => {
  let fixture: ComponentFixture<AliasSuggestionsComponent>;
  let component: AliasSuggestionsComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AliasSuggestionsComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(AliasSuggestionsComponent);
    component = fixture.componentInstance;
  });

  it('suggests the individual parts of a multi-word name', () => {
    component.name = 'Ada Lovelace';

    expect(component.suggestedAliases).toEqual(['Ada', 'Lovelace']);
  });

  it('normalizes surrounding and repeated whitespace', () => {
    component.name = '  Mary   Jane   Watson  ';

    expect(component.suggestedAliases).toEqual(['Mary', 'Jane', 'Watson']);
  });

  it('does not suggest aliases for a single-word or blank name', () => {
    component.name = 'Madonna';
    expect(component.suggestedAliases).toEqual([]);

    component.name = '   ';
    expect(component.suggestedAliases).toEqual([]);
  });

  it('omits one-character name parts', () => {
    component.name = 'J R Tolkien';

    expect(component.suggestedAliases).toEqual(['Tolkien']);
  });

  it('removes duplicate name parts while preserving their order', () => {
    component.name = 'John John Smith';

    expect(component.suggestedAliases).toEqual(['John', 'Smith']);
  });

  it('filters existing aliases case-insensitively and ignores empty entries', () => {
    component.name = 'Ada Byron Lovelace';
    component.currentAliases = ' lovelace, , ADA ';

    expect(component.suggestedAliases).toEqual(['Byron']);
  });

  it('renders nothing when there are no suggestions', () => {
    component.name = 'Madonna';
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.suggestions-container')).toBeNull();
  });

  it('renders suggestions and emits the clicked alias', () => {
    const emitted = spyOn(component.aliasAdded, 'emit');
    component.name = 'Ada Lovelace';
    fixture.detectChanges();

    const chips = fixture.debugElement.queryAll(By.css('p-chip'));
    expect(chips.map((chip) => chip.componentInstance.label)).toEqual([
      'Ada',
      'Lovelace',
    ]);

    chips[1].triggerEventHandler('click');

    expect(emitted).toHaveBeenCalledOnceWith('Lovelace');
  });

  it('emits aliases passed directly to addAlias', () => {
    const emitted = spyOn(component.aliasAdded, 'emit');

    component.addAlias('Pen name');

    expect(emitted).toHaveBeenCalledOnceWith('Pen name');
  });
});
