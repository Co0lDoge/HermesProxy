using HermesProxy.Enums;
using HermesProxy.World;
using HermesProxy.World.Enums;
using HermesProxy.World.Server.Packets;

namespace HermesProxy.World.Server;

public partial class WorldSocket
{
    const int EquipmentSetSlots = 19;

    [PacketHandler(Opcode.CMSG_SAVE_EQUIPMENT_SET)]
    void HandleSaveEquipmentSet(SaveEquipmentSet save)
    {
        if (ModernVersion.Build != ClientVersionBuild.V3_4_3_54261)
            return;

        WorldPacket packet = new WorldPacket(Opcode.CMSG_SAVE_EQUIPMENT_SET);
        packet.WritePackedGuid(new WowGuid64(save.Set.Guid));
        packet.WriteUInt32(save.Set.SetID);
        packet.WriteCString(save.Set.SetName);
        packet.WriteCString(save.Set.SetIcon);

        for (int i = 0; i < EquipmentSetSlots; i++)
        {
            if ((save.Set.IgnoreMask & (1u << i)) != 0 || save.Set.Pieces[i] == EquipmentSetModern.IgnoredSlot)
                packet.WritePackedGuid(new WowGuid64(1));
            else if (save.Set.Pieces[i].IsEmpty())
                packet.WritePackedGuid(default);
            else
                packet.WritePackedGuid(save.Set.Pieces[i].To64());
        }

        SendPacketToServer(packet);
    }

    [PacketHandler(Opcode.CMSG_DELETE_EQUIPMENT_SET)]
    void HandleDeleteEquipmentSet(DeleteEquipmentSet delete)
    {
        if (ModernVersion.Build != ClientVersionBuild.V3_4_3_54261)
            return;

        WorldPacket packet = new WorldPacket(Opcode.CMSG_EQUIPMENT_SET_DELETE);
        packet.WritePackedGuid(new WowGuid64(delete.ID));
        SendPacketToServer(packet);
    }

    [PacketHandler(Opcode.CMSG_USE_EQUIPMENT_SET)]
    void HandleUseEquipmentSet(UseEquipmentSet use)
    {
        if (ModernVersion.Build != ClientVersionBuild.V3_4_3_54261)
            return;

        GetSession().GameState.LastUsedEquipmentSetGuid = use.GUID;

        WorldPacket packet = new WorldPacket(Opcode.CMSG_EQUIPMENT_SET_USE);
        for (int i = 0; i < EquipmentSetSlots; i++)
        {
            EquipmentSetItem slot = use.Items[i];
            if (slot.Item == EquipmentSetModern.IgnoredSlot)
                packet.WritePackedGuid(new WowGuid64(1));
            else if (slot.Item.IsEmpty())
                packet.WritePackedGuid(default);
            else
                packet.WritePackedGuid(slot.Item.To64());

            packet.WriteUInt8(slot.ContainerSlot);
            packet.WriteUInt8(slot.Slot);
        }

        SendPacketToServer(packet);
    }
}
