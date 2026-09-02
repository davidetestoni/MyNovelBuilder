# First run

MyNovelBuilder can be explored without an API key. Writing, editing,
organizing prose, managing compendia, and browsing the sample content are all
local features. AI-driven text, image, and speech generation require a
compatible provider or local server to be configured separately.

## Initial content

By default, a fresh application-data directory is initialized with:

- an example novel (includes story events, compendia, record images, record overrides, etc.)
- one editable prompt for each supported AI action.

This content is seeded once. It is ordinary user data after that: edit or
delete any part of it as you like. Restarting or upgrading the application
does not restore deleted prompts, replace edits, or recreate the sample novel.

## A short tour

1. Open **Novels** and select the sample novel.
2. Edit prose directly, click inside the editor or select some text to reveal
   AI and record actions, and open **Storyline** at the bottom of the editor.
3. Select **Plan** to rearrange chapters and sections on the planning board.
4. Open **Compendia** to inspect the characters, places, objects, events, concepts,
   and other reference records used by the sample.
5. Open **Prompts** to see the messages and available keywords behind each AI
   action.
6. Create a chat or world-building session after configuring a text model, or
   continue using the non-AI parts of the application without one.

## How the main pieces fit together

- A **novel** contains its settings, chapters, prose, section summaries, and
  story events.
- A **compendium** is a reusable collection of records. Attach one or more to
  a novel to make that reference material available while writing.
- A **prompt** defines the messages and placeholders used for one kind of AI
  action. Prompts are shared across the application.
- A **chat** belongs to a novel and can include selected chapters, compendia,
  and records as context.
- A **world-building session** can propose additions or changes to compendia;
  nothing is applied until the proposal is accepted.
