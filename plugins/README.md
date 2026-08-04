# Claude Code plugins

This repository doubles as a Claude Code **plugin marketplace**, so the session-authoring knowledge can be
installed into a repository that does not contain this one.

That indirection exists for a security reason. Skill Suite is open source; a session module is not — it holds
the hidden graded suite and, for a black-box session, the reference implementation itself, and stays secret
until the competition ends. So sessions are authored in their own private repositories, which means the
guidance has to travel to them rather than the other way round.

## Installing into a session repository

From inside the private session repo:

```
/plugin marketplace add BenceLakos/Skill.Suite
```

```
/plugin install session-authoring@skill-suite
```

Claude Code reads `.claude-plugin/marketplace.json` from the default branch, so pushing a change here is all it
takes to update every repository that installed it — `/plugin marketplace update skill-suite` pulls the latest.

To pin a session repo to a reviewed revision instead of tracking the default branch, add the marketplace from a
tag or commit rather than the bare `owner/repo` shorthand.

## Layout

```
.claude-plugin/marketplace.json          the catalogue — lists the plugins in this repo
plugins/session-authoring/
├── .claude-plugin/plugin.json           the plugin's own manifest
└── skills/authoring-judging-sessions/
    ├── SKILL.md                         the workflow: create → author → calibrate → generate → publish
    └── references/
        ├── calibration.md               black-box marking-map tuning, measured not guessed
        └── publishing.md                packages, images, tags, and what must never reach public CI
```

A plugin can also carry commands, agents and hooks; this one is deliberately a single skill, because what
session authors need is knowledge that loads when they touch a session — not new commands to remember.

## Working on the skill

The skill is plain markdown with YAML frontmatter, so editing it needs nothing installed. Two things are worth
keeping in mind:

- The **description** is the whole triggering mechanism. It has to name the situations a session author is
  actually in — "add another task to the session", "why did this competitor score so low" — because Claude
  decides whether to consult the skill from that text alone, before reading the body.
- Keep `SKILL.md` to the workflow and push detail into `references/`. The body loads whenever the skill
  triggers; reference files load only when something points at them.

After changing it, verify a session repo picks the change up with `/plugin marketplace update skill-suite`.
