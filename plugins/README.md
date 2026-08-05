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

```
/plugin install marking-scheme-authoring@skill-suite
```

The two are a pair and are usually both wanted: `session-authoring` owns the module, and
`marking-scheme-authoring` owns the sheet that decides what the module is worth.

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
plugins/marking-scheme-authoring/
├── .claude-plugin/plugin.json
└── skills/authoring-marking-schemes/
    ├── SKILL.md                         trace → derive → mark → verify → prove → bind → revise
    ├── references/
    │   ├── cis-format.md                the three-sheet workbook, column by column
    │   ├── expected-values.md           why only an independent model is admissible
    │   ├── discrimination.md            making an aspect losable, and the six blindnesses
    │   └── verification-battery.md      the gates, the four personas, the mutation caveats
    └── scripts/                         openpyxl validators; run them, do not reimplement them
```

A plugin can also carry commands, agents and hooks; `session-authoring` is deliberately a single skill,
because what session authors need is knowledge that loads when they touch a session — not new commands to
remember. `marking-scheme-authoring` adds `scripts/` for one reason: its subject is *measurement*, and a
validator that reconciles 59 aspects in a second is worth more than a recipe for writing one.

## Working on the skill

The skill is plain markdown with YAML frontmatter, so editing it needs nothing installed. Two things are worth
keeping in mind:

- The **description** is the whole triggering mechanism. It has to name the situations a session author is
  actually in — "add another task to the session", "why did this competitor score so low" — because Claude
  decides whether to consult the skill from that text alone, before reading the body.
- Keep `SKILL.md` to the workflow and push detail into `references/`. The body loads whenever the skill
  triggers; reference files load only when something points at them.

After changing it, verify a session repo picks the change up with `/plugin marketplace update skill-suite`.

## Moving a plugin to its own marketplace repository

A plugin folder is self-contained, so extracting one is a copy plus a catalogue. Any GitHub repository can
be a Claude Code marketplace — it needs no build step and no release.

**1.** Copy the plugin folder to the new repo, keeping the `plugins/<name>/` prefix:

```bash
mkdir -p <new-repo>/plugins && cp -R plugins/<name> <new-repo>/plugins/
```

**2.** Add a catalogue at the **repo root**, at `.claude-plugin/marketplace.json`. Only `name`,
`description` and `source` are required on a plugin entry; `owner` is required at the top level:

```json
{
  "name": "<marketplace-name>",
  "owner": { "name": "Ingenimind Kft.", "email": "zsolt.nagy@ingenimind.com" },
  "metadata": { "description": "<what this marketplace is for>", "version": "1.0.0" },
  "plugins": [
    {
      "name": "<name>",
      "source": "./plugins/<name>",
      "description": "<one line; this is what shows in /plugin>"
    }
  ]
}
```

`source` stays a relative path because the plugin lives in this same repo. The other forms — a git URL, a
`git-subdir` with `path` and `ref`, or `{"source": "github", "repo": "owner/repo"}` — are for cataloguing
plugins that live elsewhere.

**3.** Remove the entry from this repo's `.claude-plugin/marketplace.json` and delete `plugins/<name>/`, or
the plugin exists twice under two marketplace names and installs race.

**4.** Push to the **default branch** and install:

```
/plugin marketplace add <owner>/<new-repo>
```

```
/plugin install <name>@<marketplace-name>
```

Three things bite here. Claude Code reads the catalogue **from the default branch**, so an unmerged branch
changes nothing. The marketplace is keyed by the manifest's `name` field, **not** the repository name — the
install suffix is `@<marketplace-name>`. And `plugin.json` must sit at `.claude-plugin/plugin.json` inside
the plugin folder while `skills/`, `commands/` and `agents/` sit at the plugin root, *beside* that folder
rather than inside it.

To pin consumers to a reviewed revision, add the marketplace from a tag or commit rather than the bare
`owner/repo` shorthand.
