using Framework.Constants;
using Framework.Logging;
using HermesProxy.Enums;
using HermesProxy.World;
using HermesProxy.World.Enums;
using HermesProxy.World.Objects;
using HermesProxy.World.Server.Packets;
using System;

namespace HermesProxy.World.Server;

public partial class WorldSocket
{
    // Handlers for CMSG opcodes coming from the modern client
    [PacketHandler(Opcode.CMSG_BATTLEMASTER_JOIN)]
    void HandleBattlefieldJoin(BattlemasterJoin join)
    {
        WorldPacket packet = new WorldPacket(Opcode.CMSG_BATTLEMASTER_JOIN);
        packet.WriteGuid(join.BattlemasterGuid.To64());
        if (LegacyVersion.RemovedInVersion(ClientVersionBuild.V2_0_1_6180))
            packet.WriteUInt32(GameData.GetMapIdFromBattlegroundId(join.BattlefieldListId));
        else
            packet.WriteUInt32(join.BattlefieldListId);
        packet.WriteInt32(join.BattlefieldInstanceID);
        packet.WriteBool(join.JoinAsGroup);
        SendPacketToServer(packet);
    }

    [PacketHandler(Opcode.CMSG_BATTLEFIELD_LIST)]
    void HandleBattlefieldList(BattlefieldListRequest request)
    {
        // V3_4_3-only: forwarding the PvP-UI BG-list query is part of the 54261 fix.
        // V1_14/V2_5 keep their original behaviour (request not forwarded) — no side effect.
        if (ModernVersion.Build != ClientVersionBuild.V3_4_3_54261)
            return;

        WorldPacket packet = new WorldPacket(Opcode.CMSG_BATTLEFIELD_LIST);
        if (LegacyVersion.RemovedInVersion(ClientVersionBuild.V2_0_1_6180))
            packet.WriteUInt32(GameData.GetMapIdFromBattlegroundId((uint)request.ListID));
        else
            packet.WriteUInt32((uint)request.ListID);

        if (LegacyVersion.AddedInVersion(ClientVersionBuild.V2_0_1_6180))
            packet.WriteUInt8(1); // fromWhere: 1 = PvP UI (lua RequestBattlegroundInstanceInfo)

        if (LegacyVersion.AddedInVersion(ClientVersionBuild.V3_3_3_11685))
            packet.WriteUInt8(0); // xpLocked

        Log.Print(LogType.Debug, $"[BG] CMSG_BATTLEFIELD_LIST request: client ListID={request.ListID} -> forwarding legacy bgTypeId={request.ListID} (size={packet.GetSize()}).");
        SendPacketToServer(packet);
    }

    [PacketHandler(Opcode.CMSG_BATTLEFIELD_PORT)]
    void HandleBattlefieldPort(BattlefieldPort port)
    {
        WorldPacket packet = new WorldPacket(Opcode.CMSG_BATTLEFIELD_PORT);
        uint bgTypeId = GetSession().GameState.GetBattleFieldQueueType(port.Ticket.Id);

        // arenatype byte. The legacy server derives BattlegroundQueueTypeId from
        // (bgTypeId, arenaType). A non-zero type on a real BG misses the queue (#102).
        // A hardcoded 2 on arenas misses 3v3/5v5. V1_14/V2_5 keep the prior constant.
        bool isArena = GameData.Battlegrounds.TryGetValue(bgTypeId, out var bg) && bg.IsArena;
        byte queuedArenaType = GetSession().GameState.GetBattleFieldQueueArenaType(port.Ticket.Id);
        byte arenaType = BattlefieldQueueArenaType.ForLegacyPort(
            ModernVersion.Build == ClientVersionBuild.V3_4_3_54261, isArena, queuedArenaType);

        if (LegacyVersion.AddedInVersion(ClientVersionBuild.V2_0_1_6180))
        {
            packet.WriteUInt8(arenaType);
            packet.WriteUInt8(GetSession().GameState.GetBattleFieldQueueBracketId(port.Ticket.Id));
            packet.WriteUInt32(bgTypeId);
            packet.WriteUInt16(0x1F90);
            packet.WriteBool(port.AcceptedInvite);
        }
        else
        {
            packet.WriteUInt32(bgTypeId);
            packet.WriteBool(port.AcceptedInvite);
        }
        Log.Print(LogType.Debug, $"[BG] CMSG_BATTLEFIELD_PORT: ticketId={port.Ticket.Id} AcceptedInvite={port.AcceptedInvite} -> legacy bgTypeId={bgTypeId} arenatype={arenaType} action={(port.AcceptedInvite ? 1 : 0)} size={packet.GetSize()}.");
        SendPacketToServer(packet);
    }

    [PacketHandler(Opcode.CMSG_REQUEST_BATTLEFIELD_STATUS)]
    void HandleRequestBattlefieldStatus(RequestBattlefieldStatus log)
    {
        WorldPacket packet = new WorldPacket(Opcode.CMSG_BATTLEFIELD_STATUS);
        SendPacketToServer(packet);
    }

    [PacketHandler(Opcode.CMSG_PVP_LOG_DATA)]
    void HandlePvPLogData(PVPLogDataRequest log)
    {
        WorldPacket packet = new WorldPacket(Opcode.MSG_PVP_LOG_DATA);
        SendPacketToServer(packet);
    }

    [PacketHandler(Opcode.CMSG_BATTLEFIELD_LEAVE)]
    void HandleBattlefieldLeave(BattlefieldLeave leave)
    {
        WorldPacket packet = new WorldPacket(Opcode.CMSG_BATTLEFIELD_LEAVE);
        if (LegacyVersion.AddedInVersion(ClientVersionBuild.V2_0_1_6180))
        {
            uint bgTypeId = GetSession().GameState.GetBattleFieldQueueType(1);
            bool isArena = GameData.Battlegrounds.TryGetValue(bgTypeId, out var bg) && bg.IsArena;
            byte arenaType = BattlefieldQueueArenaType.ForLegacyPort(
                ModernVersion.Build == ClientVersionBuild.V3_4_3_54261, isArena,
                GetSession().GameState.GetBattleFieldQueueArenaType(1));
            packet.WriteUInt8(arenaType);
            packet.WriteUInt8(GetSession().GameState.GetBattleFieldQueueBracketId(1));
            packet.WriteUInt32(bgTypeId);
            packet.WriteUInt16(0x1F90);
        }
        else
            packet.WriteUInt32((uint)GetSession().GameState.CurrentMapId!);
        SendPacketToServer(packet);
    }
}
