import { TestBed } from '@angular/core/testing';
import { VoiceSelectorComponent } from './voice-selector.component';

describe('VoiceSelectorComponent', () => {
  async function create(value = '', listed = true) {
    const fixture = TestBed.createComponent(VoiceSelectorComponent);
    fixture.componentRef.setInput('allowCustom', true);
    fixture.componentRef.setInput('inputId', 'test-voice');
    fixture.componentRef.setInput('options', listed ? [{ label: 'Listed voice', value: 'listed' }] : []);
    fixture.detectChanges();
    fixture.componentInstance.writeValue(value);
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture;
  }

  it('keeps listed voices in the normal select and puts Custom last', async () => {
    const fixture = await create('listed');
    expect(fixture.componentInstance.customMode).toBeFalse();
    expect(fixture.nativeElement.querySelector('input[type="text"]')).toBeNull();
    expect(fixture.componentInstance.selectOptions.map(option => option.label)).toEqual(['Listed voice', 'Custom']);
    fixture.destroy();
  });

  for (const listed of [false, true]) {
    it(`preselects Custom for an unlisted saved ID (list populated: ${listed})`, async () => {
      const fixture = await create('saved-reference', listed);
      expect(fixture.componentInstance.selection).toBe(fixture.componentInstance.customOption);
      const input = fixture.nativeElement.querySelector('#test-voice-custom') as HTMLInputElement;
      expect(input.value).toBe('saved-reference');
      expect(fixture.nativeElement.querySelector('.voice-inputs.custom')).not.toBeNull();
      fixture.destroy();
    });
  }

  it('shows a text box when Custom is clicked and emits only the entered ID', async () => {
    const fixture = await create('listed');
    const changed = jasmine.createSpy('changed');
    fixture.componentInstance.registerOnChange(changed);
    fixture.nativeElement.querySelector('.p-select-dropdown').click();
    fixture.detectChanges();
    await fixture.whenStable();
    const custom = Array.from(document.querySelectorAll<HTMLElement>('[role="option"]'))
      .find(option => option.textContent?.trim() === 'Custom')!;
    expect(custom).toBeDefined();
    custom.click();
    fixture.detectChanges();
    await fixture.whenStable();
    const input = fixture.nativeElement.querySelector('#test-voice-custom') as HTMLInputElement;
    expect(input.value).toBe('');
    input.value = 'new-reference';
    input.dispatchEvent(new Event('input', { bubbles: true }));
    fixture.detectChanges();
    await fixture.whenStable();
    expect(changed).toHaveBeenCalledWith('new-reference');
    expect(changed).not.toHaveBeenCalledWith(fixture.componentInstance.customOption);
    fixture.componentInstance.selectVoice('listed');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#test-voice-custom')).toBeNull();
    fixture.componentInstance.selectVoice(fixture.componentInstance.customOption);
    fixture.detectChanges();
    await fixture.whenStable();
    expect((fixture.nativeElement.querySelector('#test-voice-custom') as HTMLInputElement).value).toBe('new-reference');
    fixture.destroy();
  });

  it('reconciles a saved voice when the list arrives later, without changing its ID', async () => {
    const fixture = await create('listed', false);
    const changed = jasmine.createSpy('changed');
    fixture.componentInstance.registerOnChange(changed);
    fixture.componentRef.setInput('options', [{ label: 'Listed voice', value: 'listed' }]);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(fixture.componentInstance.customMode).toBeFalse();
    expect(fixture.componentInstance.value).toBe('listed');
    expect(changed).not.toHaveBeenCalled();
    fixture.destroy();
  });

  it('disables both controls when the parent form is disabled', async () => {
    const fixture = await create('custom-reference');
    fixture.componentInstance.setDisabledState(true);
    fixture.detectChanges();
    await fixture.whenStable();
    expect((fixture.nativeElement.querySelector('#test-voice-custom') as HTMLInputElement).disabled).toBeTrue();
    expect(fixture.nativeElement.querySelector('.p-select.p-disabled')).not.toBeNull();
    fixture.destroy();
  });
});
