using System.Collections.Generic;
using HermesProxy.World.Server.Packets;

namespace HermesProxy.World;

internal static class PetitionShowListCompat
{
    public const uint GuildCharterEntry = 5863;

    public static bool ShouldDropArenaList(IReadOnlyList<PetitionEntry> rows) =>
        rows.Count > 0 && rows[0].CharterEntry != GuildCharterEntry;
}
