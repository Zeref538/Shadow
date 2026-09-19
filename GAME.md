# Shadow — game design

## The hook

You die a lot. Every time you die, your last attempt keeps running beside you
as a ghost. And your only tool, a thrown dagger, becomes a ledge where it
lands — so the ghost's throws build the path your next attempt walks on.

You are not fighting the level. You are cooperating with everyone you used to be.

---

## Core loop

1. Enter a room. You have one dagger.
2. Throw it to make a ledge, or keep it to stay armed. You can't do both.
3. Die.
4. Restart the room. Your previous attempt replays from the start, in real time.
5. Now two of you are solving it. Then three. Then four.
6. The room is beaten when the exit is reached — by you, not the ghosts.

Death is not a punishment here. Death is how you add a worker to the room.

---

## Mechanic 1 — The Dagger Ledge

**Press F to throw.** The dagger flies in an arc, sticks into the first solid
surface it hits, and becomes a small platform you can stand on.

**You only ever have one dagger.** To get it back you must touch it, which
usually means standing on it — and the moment you pick it up, the ledge you
are standing on stops existing. You fall.

That single rule is the whole mechanic. Every throw is a commitment, and every
retrieval is a controlled fall.

### Rules

| Situation | What happens |
|---|---|
| Thrown at a wall | Sticks flat, becomes a narrow ledge |
| Thrown at a ceiling | Sticks, becomes a hanging bar |
| Thrown into open air | Falls to the ground, lies there, can be picked up |
| You touch the stuck dagger | You collect it, the ledge vanishes, you fall |
| Press F with no dagger | Nothing — you are unarmed until you retrieve it |

### Why it creates decisions

- A gap needs the dagger thrown into its far wall. But then the dagger is
  behind you, on the wrong side.
- A high ledge needs the dagger low so you can jump off it. But a low dagger
  is useless for the next gap.
- Retrieving it always drops you. So you retrieve it *over* somewhere safe,
  or you retrieve it as a deliberate way to descend.

---

## Mechanic 2 — The Echo

When you die, the room resets and a recording of your failed attempt plays
back from the start, perfectly in time with you.

An Echo is not an enemy and not a helper you control. It is a video of your
past self that happens to be solid.

### Rules

| Property | Behaviour |
|---|---|
| Movement | Exactly what you did last attempt, frame for frame |
| Collision | Solid to the world, so it can stand on pressure plates |
| Collision with you | You can stand on an Echo's head |
| Its dagger | It re-throws at the same moment, and that ledge is real |
| Death | An Echo that died still dies at the same moment, then vanishes |
| Limit | Maximum 4 Echoes; the oldest is dropped when a fifth is made |
| Appearance | Faded purple, translucent, trailing |

### Why it creates decisions

Your Echoes are only as useful as your deaths were *deliberate*. A flailing
death gives you a useless ghost. So you start dying on purpose:

> "I will run to the pressure plate, stand on it, and let the spikes kill me.
> Next attempt, my ghost holds that plate open while I go through the door."

That is the moment the game clicks. **You start planning your own deaths.**

---

## Where the two mechanics meet

This is the real design, and neither half is as interesting alone.

Your Echo replays your dagger throw. That ledge exists again, at the same
moment, in the same place — but this time *you are not the one who paid for
it*. Your hands are free. Your dagger is still yours to throw somewhere else.

So a hard room is solved like this:

1. **Attempt 1** — throw the dagger at the left wall, stand on it, die to the
   spikes above.
2. **Attempt 2** — your Echo throws the left ledge for you. You keep your
   dagger, throw it at the *right* wall instead, and now two ledges exist.
   Die again, deliberately, somewhere useful.
3. **Attempt 3** — two Echoes, two thrown ledges, plus your own dagger.
   Three platforms from one dagger.

**One dagger becomes many, but only across time.** That sentence is the game.

---

## Controls

| Key | Action |
|---|---|
| A / D or arrows | Run |
| Space | Jump |
| F | Throw / retrieve dagger |
| R | Die on purpose (restart the room, bank the Echo) |

`R` matters. If the player has to wait for spikes to kill them, planning a
death is tedious. Let them commit instantly.

---

## Level design patterns

Rooms should teach one idea at a time.

1. **The lonely gap** — too wide to jump. Teaches: throw and stand.
2. **The one-way drop** — you can only retrieve the dagger over a pit.
   Teaches: retrieval is a fall.
3. **The held door** — a pressure plate that opens a door only while pressed.
   Teaches: your first deliberate death.
4. **The stack** — a ledge too high for one jump, reachable only by jumping
   off an Echo's head. Teaches: Echoes are solid.
5. **The relay** — needs two ledges at once, which needs an Echo's throw.
   Teaches: the whole game.
6. **The finale** — three plates, one dagger, four Echoes.

---

## Building it, in order

Each step is playable on its own. Do not start the next one until the last
one works.

**Phase 1 — the dagger**
- `Dagger.cs`: throw as a Rigidbody2D with an arc, freeze it on collision,
  enable a small BoxCollider2D on the Ground layer so it is standable.
- Touching it returns it to the player and disables that collider.

**Phase 2 — recording**
- Each `FixedUpdate`, append `(position, facing, animationState, threwDagger)`
  to a `List`. At 50 fixed steps a second, a 30-second attempt is 1,500
  entries — small enough to ignore memory concerns.

**Phase 3 — playback**
- On death, hand the list to an `Echo` prefab. It walks the list one entry per
  `FixedUpdate`, setting its transform directly with a `Kinematic` Rigidbody2D
  so physics never pushes it off course.

**Phase 4 — solid Echoes**
- Give the Echo a collider on the Ground layer so the player can stand on it
  and it can press plates.

**Phase 5 — plates and doors**
- A trigger that counts how many bodies are on it and opens a door while the
  count is above zero.

**Phase 6 — feel**
- Trail, fade, spawn flash, a sound when an Echo appears.

---

## Traps to watch for

- **Playback must be deterministic.** Record positions, not inputs. Replaying
  inputs drifts out of sync the moment physics differs by a hair.
- **Echoes must be kinematic.** If the player's weight pushes an Echo, it
  stops matching its recording and the puzzle breaks.
- **Record on `FixedUpdate`, not `Update`.** `Update` runs at whatever frame
  rate the machine manages, so the recording would play back at the wrong
  speed on a different computer.
- **Cap the Echo count.** Four is enough to feel powerful and few enough to
  read on screen.

---

## Where this sits today

Built already: player movement, jump, three animation states, a 132-unit
level built from the ruin tileset, four parallax background layers, music.

Not built yet: everything in this document. Phase 1 is the next step, and it
is the smallest one.
