# Saving and moving this project

Repo: <https://github.com/Zeref538/Shadow> — public, no login needed to clone.

Unity **6000.5.10f1**. The project folder is **`Shadow2d`**, not the folder above it.

---

## Save your work

Before you close your laptop, every time:

```bash
cd "C:/Users/johna/OneDrive/Documents/Portfolio/Shadow"
git add -A
git commit -m "what you changed"
git push
```

Check it took:

```bash
git status
```

You want `nothing to commit, working tree clean`.

---

## Open it on another computer

**1. Download**

```bash
git clone https://github.com/Zeref538/Shadow.git
```

**2. Open in Unity**

1. Open **Unity Hub**
2. **Add** → **Add project from disk**
3. Pick the **`Shadow2d`** folder inside `Shadow`
4. **Add Project**, then click the name to open
5. First open takes a few minutes, Unity is rebuilding its cache
6. **Project** window → `Assets/Scenes` → double-click **`Game.unity`**

Hub only lists real Unity projects. If the picker looks empty you are one
level too high — go into `Shadow2d`.

**3. Send work back**

```bash
cd Shadow
git add -A
git commit -m "work from the lab"
git push
```

**4. On your laptop, before starting again**

```bash
git pull
```

Always pull first, or git will make you merge by hand.

---

## Mac lab

Open **Terminal**: **Cmd + Space**, type `Terminal`, press Return.

```bash
cd ~/Desktop
git clone https://github.com/Zeref538/Shadow.git
```

| | Windows | Mac |
|---|---|---|
| Home folder | `%USERPROFILE%` | `~` |
| Right-click | right-click | **Control + click** |

`~` only works on Mac and in Git Bash. Windows Command Prompt does not know
it, and silently leaves you where you were.

If `git` is missing, macOS offers to install developer tools — click
**Install**, wait, then clone again.

**Lab accounts often wipe on logout. Push before you leave.**

---

## USB copy

Already at `D:\Shadow-backup`.

Refresh from your laptop:

```bash
cd /d/Shadow-backup && git pull laptop master
```

Refresh from GitHub:

```bash
cd /d/Shadow-backup && git pull
```

Open it: **Add project from disk** → `D:\Shadow-backup\Shadow2d`.

New copy on a different stick (check its letter with `ls /[d-z] -d`):

```bash
git clone "C:/Users/johna/OneDrive/Documents/Portfolio/Shadow" /e/Shadow-backup
```

**Eject the USB properly** before unplugging. A clone only holds **committed**
work, so commit first.

---

## When something breaks

| Message | Fix |
|---|---|
| `detected dubious ownership` | `git config --global --add safe.directory D:/Shadow-backup` |
| Unity says project already open | `rm "Shadow2d/Temp/UnityLockfile"` |
| Hub picker shows no project | You picked `Shadow`, pick `Shadow2d` |
| Want to undo uncommitted changes | `git checkout -- .` — cannot be undone |

---

## Do not

- **Copy the folder by hand.** `Library/` is 3 GB of cache Unity rebuilds. A
  clone is 38 MB and complete.
- **Work in two copies on one machine.** You will split your history.
- **Trust OneDrive alone.** If Unity acts strange, pause OneDrive sync first.
