# BuildWorks README standard

The README is the stable front door for players. Current evidence belongs in CURRENT_STATUS, technical details in WORK_STATUS, and dated attempts in DEVELOPMENT_JOURNAL.

## Required order

1. One H1 product name and language switch.
2. One-sentence purpose and one clearly labelled cover; concept art must be called concept art.
3. Four to six concrete capabilities supported by the current module.
4. A real tool example with source/version and honest capture status; diagrams are labelled illustrations.
5. A short first-use sequence leading to the full wiki.
6. Installation/update requirements and data backup.
7. Current limitations next to the relevant features; no completed multiplayer/project claims without evidence.
8. Guide links, the forward roadmap, support, and licence.

## Maintenance rules

- Keep the opening reader-facing: no checkpoint banners, old QA version announcements, hashes, RPC counts, test transcripts, or competing “current” snapshots.
- Keep one concise status paragraph. Link to the evidence matrix rather than duplicating history.
- EN and RU use the same heading order and availability boundaries. Update both in the same editorial step.
- Keep the main README roughly 400–650 words. Detailed tutorials live in the wiki; source-build commands live in CONTRIBUTING.
- Use relative links for repository assets and absolute links for the wiki. Before publication verify every image/target, alt text, provenance and visible private information.
- Use one coherent cover, one meaningful native example, and at most one overview diagram on the main page. A gallery belongs in the wiki.
- A generated cover/icon is branding, not proof. A lab render, controlled native camera/Canvas capture, OS screenshot, state assertion and owner acceptance are different evidence.
- Never prepend work history. Change the existing capability/limitation paragraph and record history in its own document.
- New gameplay captures need explicit source/version/capture method. Retain historical failures; do not relabel old evidence as a new test.
- Source visibility does not mean an open-source licence. Preserve the actual licence and do not invent compatibility or redistribution promises.

## Review before merging

Can a new player tell what the mod does, see a truthful example, find installation and the wiki, and understand the remaining limits in one minute? Check EN/RU parity, meaningful links, no private paths/secrets, and no unapproved release claims. Only the release coordinator integrates this standard and README overlay into the central tree.
