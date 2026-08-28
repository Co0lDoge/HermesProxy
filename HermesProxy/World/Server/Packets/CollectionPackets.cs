using Framework.Constants;
using HermesProxy.World.Enums;
using HermesProxy.World.Objects;
using System.Collections.Generic;
using System.Linq;

namespace HermesProxy.World.Server.Packets;

// SMSG_ACCOUNT_HEIRLOOM_UPDATE — refresh trigger for the V3_4_3 client's
// Collections → Heirlooms panel. Wire layout (TC `MiscPackets.cpp::AccountHeirloomUpdate::Write`):
//
//   bit       IsFullUpdate  (1 = full set; partial deltas use the same packet with false)
//   FlushBits
//   int32     Unk           (TC always 0)
//   uint32    ItemCount     (number of owned heirloom item IDs)
//   uint32    FlagsCount    (must equal ItemCount per TC comment)
//   int32[]   ItemIDs       (one entry per ItemCount)
//   uint32[]  Flags         (one entry per FlagsCount; HeirloomPlayerFlags, e.g. UPGRADE_LEVEL_*)
//
// We ship the full 38-item modern heirloom set (Option α): every account sees
// every heirloom as "owned". Flags are always 0 (no upgrade tiers / no PVP marker)
// since the legacy 3.3.5a server has no account-bound collection state to mirror.
// The actual "owned count" the client renders in the panel header comes from the
// matching ActivePlayerData::Heirlooms DynamicUpdateField, not this packet — this
// packet just triggers a panel refresh against descriptor state.
public class AccountHeirloomUpdate : ServerPacket
{
    public AccountHeirloomUpdate() : base(Opcode.SMSG_ACCOUNT_HEIRLOOM_UPDATE, ConnectionType.Instance) { }

    public override void Write()
    {
        var heirlooms = GameData.Heirlooms;

        _worldPacket.WriteBit(true);                      // IsFullUpdate
        _worldPacket.FlushBits();
        _worldPacket.WriteInt32(0);                       // Unk
        _worldPacket.WriteUInt32((uint)heirlooms.Count);  // ItemCount
        _worldPacket.WriteUInt32((uint)heirlooms.Count);  // FlagsCount (== ItemCount)
        foreach (var itemId in heirlooms)
            _worldPacket.WriteInt32(itemId);              // ItemIDs[i]
        for (int i = 0; i < heirlooms.Count; i++)
            _worldPacket.WriteUInt32(0u);                 // Flags[i] (HeirloomPlayerFlags = NONE)
    }
}

// SMSG_ACCOUNT_MOUNT_UPDATE — lineagedr/3.4.3_Source MiscPackets.cpp
// AccountMountUpdate::Write:
//   bit     IsFullUpdate
//   uint32  Mounts.size()
//   for each: int32 SpellID + bits<4> MountStatusFlags
//   FlushBits
// Favorite flag is MOUNT_IS_FAVORITE = 0x02.
public class AccountMountUpdate : ServerPacket
{
    public const uint FavoriteFlag = 0x02;

    public AccountMountUpdate() : base(Opcode.SMSG_ACCOUNT_MOUNT_UPDATE, ConnectionType.Instance) { }

    public override void Write()
    {
        _worldPacket.WriteBit(IsFullUpdate);
        _worldPacket.WriteUInt32((uint)Mounts.Count);
        foreach (var (spellId, flags) in Mounts)
        {
            _worldPacket.WriteInt32((int)spellId);
            _worldPacket.WriteBits(flags, 4);
        }
        _worldPacket.FlushBits();
    }

    public bool IsFullUpdate = true;
    public List<(uint SpellId, uint Flags)> Mounts = [];

    public static AccountMountUpdate FromSession(GameSessionData state)
    {
        var favorites = state.CollectionFavorites?.FavoriteMountSpells;
        var packet = new AccountMountUpdate { IsFullUpdate = true };
        foreach (uint spellId in state.KnownSpells.OrderBy(id => id))
        {
            if (!GameData.MountSpells.Contains(spellId))
                continue;
            uint flags = favorites != null && favorites.Contains(spellId) ? FavoriteFlag : 0u;
            packet.Mounts.Add((spellId, flags));
        }
        return packet;
    }
}

public class MountSetFavorite : ClientPacket
{
    public MountSetFavorite(WorldPacket packet) : base(packet) { }

    public override void Read()
    {
        MountSpellID = _worldPacket.ReadUInt32();
        IsFavorite = _worldPacket.ReadBit();
    }

    public uint MountSpellID;
    public bool IsFavorite;
}

// SMSG_ACCOUNT_TOY_UPDATE — Wrathion ToyPackets.cpp AccountToyUpdate::Write
public class AccountToyUpdate : ServerPacket
{
    public AccountToyUpdate() : base(Opcode.SMSG_ACCOUNT_TOY_UPDATE, ConnectionType.Instance) { }

    public override void Write()
    {
        _worldPacket.WriteBit(IsFullUpdate);
        _worldPacket.FlushBits();
        _worldPacket.WriteInt32(Toys.Count);
        _worldPacket.WriteInt32(Toys.Count);
        _worldPacket.WriteInt32(Toys.Count);
        foreach (var (itemId, _, _) in Toys)
            _worldPacket.WriteUInt32(itemId);
        foreach (var (_, isFavorite, _) in Toys)
            _worldPacket.WriteBit(isFavorite);
        foreach (var (_, _, hasFanfare) in Toys)
            _worldPacket.WriteBit(hasFanfare);
        _worldPacket.FlushBits();
    }

    public bool IsFullUpdate = true;
    public List<(uint ItemId, bool IsFavorite, bool HasFanfare)> Toys = [];

    public static AccountToyUpdate FromSession(GameSessionData state)
    {
        var favorites = state.CollectionFavorites;
        var packet = new AccountToyUpdate { IsFullUpdate = true };
        if (favorites == null)
            return packet;

        foreach (uint itemId in state.GetUsableToysOrdered())
        {
            bool isFavorite = favorites.FavoriteToys.Contains(itemId);
            packet.Toys.Add((itemId, isFavorite, false));
        }
        return packet;
    }
}

public class ToyClearFanfare : ClientPacket
{
    public ToyClearFanfare(WorldPacket packet) : base(packet) { }

    public override void Read()
    {
        ItemID = _worldPacket.ReadUInt32();
    }

    public uint ItemID;
}

public class AddToy : ClientPacket
{
    public AddToy(WorldPacket packet) : base(packet) { }

    public override void Read()
    {
        Guid = _worldPacket.ReadPackedGuid128();
    }

    public WowGuid128 Guid = WowGuid128.Empty;
}

public class UseToy : ClientPacket
{
    public UseToy(WorldPacket packet) : base(packet)
    {
        Cast = new SpellCastRequest();
    }

    public override void Read()
    {
        Cast.Read(_worldPacket);
    }

    public SpellCastRequest Cast;

    public uint ItemId => Cast.Misc[0];
}

public enum ItemCollectionType : byte
{
    None = 0,
    Toy = 1,
    Heirloom = 2,
    Transmog = 3,
    TransmogSetFavorite = 4,
}

public class CollectionItemSetFavorite : ClientPacket
{
    public CollectionItemSetFavorite(WorldPacket packet) : base(packet) { }

    public override void Read()
    {
        // Wrathion CollectionPackets.cpp: CollectionType is int32, not uint8
        Type = (ItemCollectionType)_worldPacket.ReadInt32();
        ID = _worldPacket.ReadUInt32();
        IsFavorite = _worldPacket.ReadBit();
    }

    public ItemCollectionType Type;
    public uint ID;
    public bool IsFavorite;
}
