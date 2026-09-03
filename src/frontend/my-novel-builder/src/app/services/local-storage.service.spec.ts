import { TestBed } from '@angular/core/testing';
import { LocalStorageService } from './local-storage.service';

describe('LocalStorageService', () => {
  let service: LocalStorageService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(LocalStorageService);
    localStorage.clear();
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('returns null for missing or malformed objects', () => {
    expect(service.getObjectForKey('missing')).toBeNull();

    localStorage.setItem('broken', '{not json');

    expect(service.getObjectForKey('broken')).toBeNull();
  });

  it('round-trips stored objects', () => {
    const value = { enabled: true, count: 3 };

    service.setObjectForKey('settings', value);

    expect(service.getObjectForKey('settings')).toEqual(value);
  });

  it('returns defaults for malformed nested values', () => {
    localStorage.setItem(
      'preferences',
      JSON.stringify({ draft: '{broken', recent: '{also broken' }),
    );

    expect(
      service.getNestedObjectForKey('preferences', 'draft'),
    ).toBeNull();
    expect(
      service.getNestedStringArrayForKey('preferences', 'recent'),
    ).toEqual([]);
  });

  it('filters non-string values from nested string arrays', () => {
    service.setNestedObjectForKey(
      'preferences',
      'recent',
      ['one', 2, null, 'two'],
    );

    expect(
      service.getNestedStringArrayForKey('preferences', 'recent'),
    ).toEqual(['one', 'two']);
  });

  it('replaces a malformed outer map when setting a nested value', () => {
    localStorage.setItem('preferences', '{broken');

    expect(() =>
      service.setNestedStringForKey('preferences', 'prompt', 'prompt-id'),
    ).not.toThrow();
    expect(
      service.getNestedStringForKey('preferences', 'prompt'),
    ).toBe('prompt-id');
  });

  it('deduplicates and limits recent nested strings', () => {
    ['one', 'two', 'three', 'two'].forEach((value) =>
      service.pushNestedRecentStringForKey(
        'preferences',
        'recent',
        value,
        3,
      ),
    );

    expect(
      service.getNestedStringArrayForKey('preferences', 'recent'),
    ).toEqual(['two', 'three', 'one']);
  });
});
