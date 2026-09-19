# Backing up and moving this project

Three copies exist, and they do different jobs.

| Where | Job | Updates |
|---|---|---|
| **GitHub** | the real backup, and how you move between computers | only when you run `git push` |
| **OneDrive** | automatic safety net | by itself, constantly |
| **USB** | offline copy for when there is no internet | when you run `git pull` on it |

Repo: <https://github.com/Zeref538/Shadow>

---

## Why you never copy the whole folder

| | Size |
|---|---|
| Everything git stores | **38 MB** |
| `Shadow2d/Library/` | 3,332 MB |
| `archive/` | 1,061 MB |

`Library/` is Unity's cache. It is rebuilt from `Assets/` every time the
project opens on a new machine, so copying it is pointless and slow. The
`.gitignore` file already excludes it.

A clone is 38 MB and complete. A drag-and-drop is 4.4 GB and mostly junk.

---

## Is a clone enough to open the project?

Yes. Unity only needs three folders, and all three are in git:

- `Shadow2d/Assets` — 141 files, including all 77 `.meta` files
- `Shadow2d/ProjectSettings` — 29 files
- `Shadow2d/Packages` — 2 files

**The `.meta` files are the important part.** Every asset has one, and it holds
that asset's GUID — the ID that animations, scenes and prefabs use to point at
it. If `.meta` files went missing, Unity would invent new IDs and every sprite
reference in the game would break.

---

## Saving your work (do this before you leave any computer)

```bash
cd "C:/Users/johna/OneDrive/Documents/Portfolio/Shadow"
git add -A
git commit -m "say what changed"
git push
```

`git add -A` stages every change, `commit` records it locally, `push` sends it
to GitHub. **Until you push, GitHub does not have it.**

Check nothing is left behind:

```bash
git status
```

You want to see `nothing to commit, working tree clean`.

---

## Opening it on another computer

### 1. Get the files

```bash
cd ~/Desktop
git clone https://github.com/Zeref538/Shadow.git
```

If the repo is still **private**, this will ask for a login. A school lab
machine usually cannot give one. Either make the repo public first, from your
own laptop:

```bash
gh repo edit Zeref538/Shadow --visibility public --accept-visibility-change-consequences
```

or download a ZIP through the browser instead: github.com, sign in, open the
repo, click the green **Code** button, then **Download ZIP**. The ZIP route
has no git history and you cannot push back from it.

### 2. Open it in Unity

1. Open **Unity Hub**.
2. Click **Add** (top right), then **Add project from disk**.
3. Select the `Shadow2d` folder — the one containing `Assets`,
   `ProjectSettings` and `Packages`. **Not** the `Shadow` folder above it.
4. Click **Add Project**, then click the project name to open it.
5. Wait. The first open rebuilds `Library/` from scratch, several minutes.
6. In the **Project** window open `Assets/Scenes` and double-click
   `Game.unity`.

Built with Unity **6000.5.10f1**. A different version will still open but Hub
will warn you, and small things may shift.

### 3. Send your work back

```bash
cd ~/Desktop/Shadow
git add -A
git commit -m "work from the lab"
git push
```

Then on your own laptop, before you start working again:

```bash
git pull
```

**Always `git pull` first.** If both machines change the same file without
pulling, git stops and asks you to merge, which is annoying to untangle.

---

## The USB copy

Clone onto the USB rather than copying the folder. You get the history, and
`Library/` is skipped automatically.

With the USB plugged in and showing as drive `E:`:

```bash
git clone "C:/Users/johna/OneDrive/Documents/Portfolio/Shadow" /d/Shadow-backup
```

The USB copy has two remotes (places it can sync with):

- `origin` -> GitHub, works on any computer
- `laptop` -> the folder on your laptop, works with no internet

To refresh it from your laptop, with the USB plugged in:

```bash
cd /d/Shadow-backup
git pull laptop master
```

Or from GitHub, on any machine with internet:

```bash
git pull
```

To open the USB copy in Unity, use **Add project from disk** and pick
`D:\Shadow-backup\Shadow2d`.

**A clone only contains what you have committed.** Commit and push before you
clone, every time.

### "dubious ownership" error on the USB

```
fatal: detected dubious ownership in repository at 'D:/Shadow-backup'
```

USB sticks are usually formatted FAT32, which does not store file owners, so
git cannot confirm the repo is yours and refuses to run. A repo can contain
hooks - scripts that run automatically - so this check exists for a reason.
Whitelisting your own USB is safe:

```bash
git config --global --add safe.directory D:/Shadow-backup
```

This is per-computer, so you may need it again on another machine.

---

## OneDrive warning

This project lives inside your OneDrive folder, so OneDrive is syncing
`Library/` — thousands of files Unity rewrites constantly. It works, but it is
the most likely cause of strange Unity behaviour. If the Editor starts acting
up, pause OneDrive sync before assuming the project is broken.

---

## If something goes wrong

Unity refuses to open, says the project is already open:

```bash
rm "Shadow2d/Temp/UnityLockfile"
```

That file is left behind when Unity is force-closed.

Everything looks broken and you want to go back to the last saved state:

```bash
git checkout -- .
```

That throws away uncommitted changes and restores the last commit. It cannot
be undone, so be sure.
