# Compendia and AI tools

## Build reusable compendia

A compendium groups reference material that can be attached to more than one
novel. Records are organized as characters, places, objects, events, concepts,
or other material. Each record has a name, comma-separated aliases, context,
an **Always included** option, and optional image or video media. Character
records can also store a voice for each provider and model pair, for immersive
narration TTS mode.

Attach compendia from a novel's settings. They then appear beside its prose and
become available to novel-aware AI actions.

## Control context inclusion

For normal novel generation, a record is included when its name or one of its
aliases appears as a whole term in the relevant prose or context. A referenced
record can bring in other records mentioned in its own context. Enable
**Always included** for information that should be sent even when it is not
mentioned. Assets like images or videos are never sent to the LLM.

Prompt preview buttons in chat, world building, and RPG mode show the assembled
request before it is sent, and it's a very useful troubleshooting feature.

## Customize prompts

The **Prompts** page contains a filterable library. Every prompt belongs to a
specific action type and consists of ordered system, user, and assistant
messages. The editor lists the placeholders supported by the selected type.

Prompt types cannot be changed after creation, but names and messages are
editable. Prompts—including the defaults created on first run—can be deleted.
AI controls remember prompt choices by feature where supported.

## Chat about a novel

Create a chat for a selected novel, then choose a text model and a `Send Chat
Message` prompt. The context bar can include all or selected compendia,
individual records, and one chapter. Messages support Markdown and can be
copied, edited, deleted, or resent.

Chats remain linked to their original novel. If that novel is deleted, the
conversation remains visible but cannot build novel context.

## Review world-building proposals

A world-building session combines an optional novel, chapter, premise,
compendia, and record selection with a structured-output model and a `World
Building Agent` prompt. Ask it to propose new world material, refinements, or
ways to resolve contradictions.

Suggested operations appear as editable proposal cards. Review their target,
record type, aliases, context, and rationale, then explicitly **Accept** or
**Reject** each proposal. Accepting applies the operation to the selected
compendium; ordinary chat messages do not modify records on their own.
