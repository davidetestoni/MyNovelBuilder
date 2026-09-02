# Writing workflow

## Create and configure a novel

Open **Novels** and choose **Add Novel**. A novel has a title, author, brief,
tense, point of view, and language. You can also add a cover, attach compendia,
and choose a main character from the attached character records.

These values can be changed later from the novel's **Settings** page.

## Write prose

The prose editor organizes the manuscript into chapters and sections. Chapter
titles, section text, and section summaries can be edited in place. Each
chapter shows its section count, word count, and estimated reading time.

The actions beside a section can:

- generate or edit its summary;
- read the section with text to speech;
- attach an image;
- define record overrides for that point in the story;
- remove the section.

Clicking in the prose reveals **Generate** and **Suggestions**. Selecting text
also reveals actions to **Replace** the selection or turn it into a **new compendium record**.
AI actions require a matching prompt and ask you to choose a model.

## Rearrange the manuscript

Select **Plan** from a novel to open the planning board. Drag chapters to
reorder them, or drag sections within and between chapters. Section checkboxes
allow several sections to be moved together. Titles can be edited and chapters
or sections can be added or deleted from the board.

## Track the storyline

Open **Storyline** at the bottom of the prose editor to see chapter-level story
events. Events can have a title, description, and date. Add or edit them
manually, drag them within or between chapters, or use **Generate Story Events**
with a configured model and a `Create Story Events` prompt.

Selecting a chapter or one of its events changes the chapter shown in the
editor. Storyline is available whether or not RPG mode is enabled.

## Use RPG mode

RPG mode is an optional way to progress the story as if you were playing a text-based RPG.
Enable **Show RPG controls in the prose editor** from the novel's settings. The prose
editor then displays a model and prompt selector, **Do** and **Say** actions,
and a field for guiding the next beat.

RPG generation is available only while viewing the final chapter. **Do** tells
the prompt to treat the input as an action, while **Say** treats it as dialogue.
Use the eye button to inspect the assembled prompt before sending it.

## Change record details over time

A section can override part of a compendium record from that point in the
manuscript onward. The record context must first contain a named block such as:

```text
[age]27[/age]
```

In **Record Overrides**, select that record, choose `age`, and enter the new
content. The application replaces the text between them when building later prompts.
A later override for the same record and keyword supersedes the earlier value.

## Import, export, and translate

Novel settings provide exports in Markdown, HTML, and PDF. **Replace prose from
Markdown** accepts an uploaded `.md` or `.markdown` file or pasted Markdown and
replaces the novel's current chapters and prose after confirmation; it is not a
merge or backup operation.

**Translate** creates a translated copy through a configured text model and
translation prompt. Review generated text before relying on it.
