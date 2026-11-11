import { RecordContextSectionsPipe } from './record-context-sections.pipe';

describe('RecordContextSectionsPipe', () => {
  const pipe = new RecordContextSectionsPipe();

  it('splits keyword regions and keeps surrounding prose in order', () => {
    expect(pipe.transform('Intro\n[appearance]Blue cloak\nSilver eyes[/appearance]\nBetween\n[goal]Win[/goal]After')).toEqual([
      { heading: null, text: 'Intro' },
      { heading: 'appearance', text: 'Blue cloak\nSilver eyes' },
      { heading: null, text: 'Between' },
      { heading: 'goal', text: 'Win' },
      { heading: null, text: 'After' },
    ]);
  });

  it('handles literal kw tags, repeated keywords, spaces, and mixed case', () => {
    expect(pipe.transform('[kw]One[/kw][kw]Two[/kw][Current goal] Three [/current goal]')).toEqual([
      { heading: 'kw', text: 'One' },
      { heading: 'kw', text: 'Two' },
      { heading: 'Current goal', text: 'Three' },
    ]);
  });

  it('preserves plain text and incomplete or mismatched tags', () => {
    for (const text of ['Plain\ntext', '[goal]Unfinished', '[goal]Text[/other]']) {
      expect(pipe.transform(text)).toEqual([{ heading: null, text }]);
    }
  });

  it('keeps HTML as text and allows empty tagged sections', () => {
    expect(pipe.transform('[goal]<b>Win</b>[/goal][notes][/notes]')).toEqual([
      { heading: 'goal', text: '<b>Win</b>' },
      { heading: 'notes', text: '' },
    ]);
  });

  it('handles missing or blank descriptions', () => {
    for (const text of [null, undefined, '', ' \n ']) {
      expect(pipe.transform(text)).toEqual([]);
    }
  });
});
