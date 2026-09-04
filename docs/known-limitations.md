# Known Limitations

Things HermesProxy **cannot** do, as opposed to things it does badly. Entries here are structural — they follow from what the protocol, the client or the remote server permits, not from a bug someone can fix in an afternoon.

For bugs that have workarounds and are expected to close, see [known-issues.md](known-issues.md).

---

## Warden-protected servers

**HermesProxy cannot connect to a legacy server that enforces Warden.** The connection either dies during the handshake or is kicked shortly after world-enter.

Warden is Blizzard's anti-cheat. The server periodically pushes a check module and demands a response computed from the client's own memory. HermesProxy does not implement Warden at any point:

- `SMSG_WARDEN_DATA` from the legacy server is on the handshake ignore list and is silently discarded (`HermesProxy/World/Client/WorldClient.cs`). It is dropped rather than answered, which is deliberate — without that, cores that push Warden data mid-auth would abort an otherwise healthy handshake ([#62](https://github.com/Xian55/HermesProxy/issues/62)).
- `CMSG_WARDEN_DATA` is unmapped on the 3.4.3 client (`V3_4_3_54261/Opcode.cs`), so there is no route to send a reply even in principle.

This is not a gap waiting to be filled. A modern Classic client runs a Warden module built for **its own** build; the legacy server's checks are written against the memory layout of the legacy client it expects. Forwarding the traffic unchanged would produce answers the server reads as tampering, and the correct answers cannot be synthesised without emulating a client the proxy does not have.

**Consequence:** the remote server must have Warden disabled. Every backend used to develop and test HermesProxy runs with it off — that is the default for `vmangos-deploy`, and TrinityCore, AzerothCore and CMaNGOS all ship it disabled or trivially disableable in config.

### Warmane

**Warmane does not work and is not supported.** It is the most common server people ask about, so it is called out by name.

Two independent blockers:

1. **Warden is enforced.** See above. This alone is decisive.
2. **Warmane runs a heavily customised core.** Its protocol has diverged from stock 3.3.5a in ways the translation layer does not model, so even with Warden off the session would not be trustworthy.

> **Do not attempt this with an account you care about.** Connecting through a proxy is indistinguishable from client tampering as far as Warden is concerned, and public servers ban for it. If you experiment anyway, use a throwaway account and accept that losing it is the expected outcome.

The same reasoning applies to any other public server that runs Warden or a custom core. Public servers known to work are listed in [wotlk.md](../wotlk.md); they work because they are close to stock AzerothCore and do not enforce Warden.
