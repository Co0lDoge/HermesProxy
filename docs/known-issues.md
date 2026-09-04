# Known Issues

Bugs and quirks that have workarounds. For things HermesProxy structurally cannot do — Warden-protected servers such as Warmane, for instance — see [known-limitations.md](known-limitations.md).

---

# Classic Era (1.14.x)

## Priest wand `Shoot` cancels in melee range (1.14.x client)

On modern 1.14.x Classic clients the `autoRangedCombat` CVar (default ON) treats wands as ranged weapons and auto-cancels `Shoot` the moment a mob enters melee range, then switches you into auto-attack. Vanilla 1.12 emulators (VMaNGOS, Kronos, CMaNGOS) never expected this — the wand simply dies, you can't finish the mob with it, and you get stuck swinging.

**Workaround — run once in chat:**
```
/console autoRangedCombat 0
```
Or make it persistent by adding this line to `WTF/Config.wtf` before launch:
```
SET autoRangedCombat "0"
```

Priest characters logging in on 1.14+ Classic Era clients receive a one-time chat reminder from the proxy on world-enter. Other classes that occasionally use a wand are affected the same way — apply the same CVar fix if you notice it. Tracked in [#80](https://github.com/Xian55/HermesProxy/issues/80).

---

# WotLK Classic (3.4.3) — beta

3.4.3 support is in beta. Most gameplay works end to end; the items below are the ones a player is likely to meet. Full audited status lives in [wotlk.md](../wotlk.md).

## Trading wedges after a "player is busy" refusal

If a trade is refused because the other player is busy (a mailbox or another window is open on their side), the initiating client cannot start any further trade for the rest of the session. The proxy's trade session and the client's trade UI fall permanently out of step.

**Workaround:** relog. Tracked in [#228](https://github.com/Xian55/HermesProxy/issues/228).

## Corpses stay as bodies instead of turning to bones (AzerothCore)

Delivering a corpse `CreateObject2` to the 3.4.3 client hard-crashes it, so the proxy withholds it. The visible cost is that a corpse keeps its pre-conversion model until you move out of range and back.

Cosmetic only — Remove Insignia and normal looting still work. Tracked in [#190](https://github.com/Xian55/HermesProxy/issues/190).

## Stable slots show as locked until you visit a stable master

3.3.5a has no update field for stable slot count; the number exists only in the reply to a stable-master interaction. At login the proxy has nothing to send, so owned slots render locked.

**Workaround:** open the stable window once. The count is then correct for the rest of the session, including live across purchases. Tracked in [#237](https://github.com/Xian55/HermesProxy/issues/237).

## Not bridged yet

| Feature | Issue |
|---|---|
| Barber shop | [#220](https://github.com/Xian55/HermesProxy/issues/220) |
| Calendar | [#222](https://github.com/Xian55/HermesProxy/issues/222) |
| Currency and honor panel (wired, unverified) | [#214](https://github.com/Xian55/HermesProxy/issues/214) |

The customer support window never finishes loading ([#230](https://github.com/Xian55/HermesProxy/issues/230)). A native 3.4.3 client does the same thing against a native server, so this is not a proxy defect.

## CMaNGOS WotLK backends

Dungeon Finder passes through without working ([#104](https://github.com/Xian55/HermesProxy/issues/104)) and MOTransports crash the client ([#101](https://github.com/Xian55/HermesProxy/issues/101)). Prefer TrinityCore or AzerothCore for 3.4.3 until those close.
