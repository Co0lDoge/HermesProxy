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
    [PacketHandler(Opcode.CMSG_PARTY_INVITE)]
    void HandleUpdateRaidTarget(PartyInviteClient invite)
    {
        WorldPacket packet = new WorldPacket(Opcode.CMSG_PARTY_INVITE);
        packet.WriteCString(invite.TargetName);
        if (LegacyVersion.AddedInVersion(ClientVersionBuild.V3_0_2_9056))
            packet.WriteUInt32(0);
        SendPacketToServer(packet);
    }

    [PacketHandler(Opcode.CMSG_PARTY_INVITE_RESPONSE)]
    void HandlePartyInviteResponse(PartyInviteResponse invite)
    {
        if (invite.Accept)
        {
            WorldPacket packet = new WorldPacket(Opcode.CMSG_GROUP_ACCEPT);
            if (LegacyVersion.AddedInVersion(ClientVersionBuild.V3_0_2_9056))
                packet.WriteUInt32(0);
            SendPacketToServer(packet);
        }
        else
        {
            WorldPacket packet = new WorldPacket(Opcode.CMSG_GROUP_DECLINE);
            SendPacketToServer(packet);
        }
    }

    [PacketHandler(Opcode.CMSG_LEAVE_GROUP)]
    void HandleLeaveGroup(LeaveGroup leave)
    {
        GetSession().GameState.WeWantToLeaveGroup = true;
        WorldPacket packet = new WorldPacket(Opcode.CMSG_GROUP_DISBAND);
        SendPacketToServer(packet);
    }

    [PacketHandler(Opcode.CMSG_PARTY_UNINVITE)]
    void HandlePartyUninvite(PartyUninvite kick)
    {
        WorldPacket packet = new WorldPacket(Opcode.CMSG_GROUP_UNINVITE_GUID);
        packet.WriteGuid(kick.TargetGUID.To64());
        if (LegacyVersion.AddedInVersion(ClientVersionBuild.V3_0_2_9056))
            packet.WriteCString(kick.Reason);
        SendPacketToServer(packet);
    }

    [PacketHandler(Opcode.CMSG_SET_ASSISTANT_LEADER)]
    void HandleSetAssistantLeader(SetAssistantLeader assist)
    {
        WorldPacket packet = new WorldPacket(Opcode.CMSG_SET_ASSISTANT_LEADER);
        packet.WriteGuid(assist.TargetGUID.To64());
        packet.WriteBool(assist.Apply);
        SendPacketToServer(packet);
    }

    [PacketHandler(Opcode.CMSG_SET_EVERYONE_IS_ASSISTANT)]
    void HandleSetAssistantLeader(SetEveryoneIsAssistant assist)
    {
        var groupMembers = GetSession().GameState.GetCurrentGroup()!.PlayerList;
        foreach (var member in groupMembers)
        {
            if (member.GUID == GetSession().GameState.CurrentPlayerGuid)
                continue;

            WorldPacket packet = new WorldPacket(Opcode.CMSG_SET_ASSISTANT_LEADER);
            packet.WriteGuid(member.GUID.To64());
            packet.WriteBool(assist.Apply);
            SendPacketToServer(packet);
        }
    }

    [PacketHandler(Opcode.CMSG_SET_PARTY_LEADER)]
    void HandleSetPartyLeader(SetPartyLeader leader)
    {
        WorldPacket packet = new WorldPacket(Opcode.CMSG_SET_PARTY_LEADER);
        packet.WriteGuid(leader.TargetGUID.To64());
        SendPacketToServer(packet);
    }

    [PacketHandler(Opcode.CMSG_CONVERT_RAID)]
    void HandleConvertRaid(ConvertRaid raid)
    {
        WorldPacket packet = new WorldPacket(Opcode.CMSG_CONVERT_RAID);
        // wotlk_classic TC reads a single bit: true = ConvertToRaid, false = ConvertToGroup.
        // Without this bit the server reads past EOF, defaults to "raid", and "Convert to Party" silently no-ops.
        if (LegacyVersion.AddedInVersion(ClientVersionBuild.V3_4_3_54261))
        {
            packet.WriteBit(raid.Raid);
            packet.FlushBits();
        }
        SendPacketToServer(packet);
    }

    [PacketHandler(Opcode.CMSG_DO_READY_CHECK)]
    void HandlReadyCheck(DoReadyCheck raid)
    {
        WorldPacket packet = new WorldPacket(Opcode.MSG_RAID_READY_CHECK);
        SendPacketToServer(packet);
    }

    [PacketHandler(Opcode.CMSG_READY_CHECK_RESPONSE)]
    void HandlReadyCheckResponse(ReadyCheckResponseClient raid)
    {
        WorldPacket packet = new WorldPacket(Opcode.MSG_RAID_READY_CHECK);
        packet.WriteBool(raid.IsReady);
        SendPacketToServer(packet);

        // The legacy server broadcasts MSG_RAID_READY_CHECK_CONFIRM to the leader and
        // assistants only, so a plain member never sees its own answer come back. Echo it
        // locally under the real party GUID - a placeholder GUID does not match the party
        // the client is tracking, so the echo was discarded.
        ReadyCheckResponse ready = new ReadyCheckResponse();
        ready.Player = GetSession().GameState.CurrentPlayerGuid;
        ready.IsReady = raid.IsReady;
        ready.PartyGUID = GetSession().GameState.GetCurrentGroupGuid();
        SendPacket(ready);
    }

    [PacketHandler(Opcode.CMSG_UPDATE_RAID_TARGET)]
    void HandleUpdateRaidTarget(UpdateRaidTarget update)
    {
        WorldPacket packet = new WorldPacket(Opcode.MSG_RAID_TARGET_UPDATE);
        packet.WriteInt8(update.Symbol);
        packet.WriteGuid(update.Target.To64());
        SendPacketToServer(packet);
    }

    [PacketHandler(Opcode.CMSG_SUMMON_RESPONSE)]
    void HandleSummonResponse(SummonResponse update)
    {
        if (update.Accept || LegacyVersion.AddedInVersion(ClientVersionBuild.V2_0_1_6180))
        {
            WorldPacket packet = new WorldPacket(Opcode.CMSG_SUMMON_RESPONSE);
            packet.WriteGuid(update.SummonerGUID.To64());
            packet.WriteBool(update.Accept);
            SendPacketToServer(packet);
        }
    }

    [PacketHandler(Opcode.CMSG_MINIMAP_PING)]
    void HandleMinimapPing(MinimapPingClient ping)
    {
        WorldPacket packet = new WorldPacket(Opcode.MSG_MINIMAP_PING);
        packet.WriteVector2(ping.Position);
        SendPacketToServer(packet);
    }

    [PacketHandler(Opcode.CMSG_RANDOM_ROLL)]
    void HandleMinimapPing(RandomRollClient roll)
    {
        WorldPacket packet = new WorldPacket(Opcode.MSG_RANDOM_ROLL);
        packet.WriteInt32(roll.Min);
        packet.WriteInt32(roll.Max);
        SendPacketToServer(packet);
    }

    [PacketHandler(Opcode.CMSG_REQUEST_PARTY_MEMBER_STATS)]
    void HandleRequestPartyMemberStats(RequestPartyMemberStats request)
    {
        WorldPacket packet = new WorldPacket(Opcode.CMSG_REQUEST_PARTY_MEMBER_STATS);
        packet.WriteGuid(request.TargetGUID.To64());
        SendPacketToServer(packet);
    }

    [PacketHandler(Opcode.CMSG_GROUP_CHANGE_SUB_GROUP)]
    void HandleGroupChangeSubGroup(ChangeSubGroup group)
    {
        WorldPacket packet = new WorldPacket(Opcode.CMSG_GROUP_CHANGE_SUB_GROUP);
        packet.WriteCString(GetSession().GameState.GetPlayerName(group.TargetGUID));
        packet.WriteUInt8(group.NewSubGroup);
        SendPacketToServer(packet);
    }

    [PacketHandler(Opcode.CMSG_GROUP_SWAP_SUB_GROUP)]
    void HandleGroupSwapSubGroup(SwapSubGroups group)
    {
        WorldPacket packet = new WorldPacket(Opcode.CMSG_GROUP_SWAP_SUB_GROUP);
        packet.WriteCString(GetSession().GameState.GetPlayerName(group.FirstTarget));
        packet.WriteCString(GetSession().GameState.GetPlayerName(group.SecondTarget));
        SendPacketToServer(packet);
    }

    [PacketHandler(Opcode.CMSG_SET_ROLE)]
    void HandleSetRole(SetRole packet)
    {
        // AC has no CMSG_GROUP_SET_ROLES. Native 3.4.3 replies with
        // SMSG_ROLE_CHANGED_INFORM and stores the role on the group.
        byte newRole = packet.Role;
        byte oldRole = 0;
        var assigned = GetSession().GameState.GroupAssignedRoles;
        if (assigned.TryGetValue(packet.ChangedUnit, out var stored))
            oldRole = stored;

        PartyUpdate? group = FindGroupForRoleAssign(packet.PartyIndex, packet.ChangedUnit);

        if (group != null)
        {
            for (int i = 0; i < group.PlayerList.Count; i++)
            {
                var member = group.PlayerList[i];
                if (member.GUID != packet.ChangedUnit)
                    continue;
                if (!assigned.ContainsKey(packet.ChangedUnit))
                    oldRole = member.RolesAssigned;
                member.RolesAssigned = newRole;
                group.PlayerList[i] = member;
                break;
            }
        }

        if (oldRole == newRole)
            return;

        assigned[packet.ChangedUnit] = newRole;

        if (packet.ChangedUnit == GetSession().GameState.CurrentPlayerGuid)
        {
            GetSession().GameState.LfgRequestedRoles = newRole;
            if (LegacyVersion.AddedInVersion(ClientVersionBuild.V3_3_0_10958))
            {
                WorldPacket legacy = new WorldPacket(Opcode.CMSG_LFG_SET_ROLES);
                legacy.WriteUInt8(newRole);
                SendPacketToServer(legacy);
            }
        }

        RoleChangedInform inform = new();
        inform.PartyIndex = packet.PartyIndex;
        inform.From = GetSession().GameState.CurrentPlayerGuid;
        inform.ChangedUnit = packet.ChangedUnit;
        inform.OldRole = oldRole;
        inform.NewRole = packet.Role;
        SendPacket(inform);

        // INFORM is the chat line. The Set Role radio and UnitGroupRolesAssigned
        // come from SMSG_PARTY_UPDATE.RolesAssigned.
        if (group != null)
        {
            for (int i = 0; i < group.PlayerList.Count; i++)
            {
                var member = group.PlayerList[i];
                if (assigned.TryGetValue(member.GUID, out var role))
                {
                    member.RolesAssigned = role;
                    group.PlayerList[i] = member;
                }
            }

            var update = group.CloneUnwritten();
            update.SequenceNum = GetSession().GameState.GroupUpdateCounter++;
            if (update.PartyIndex < GetSession().GameState.CurrentGroups.Length)
                GetSession().GameState.CurrentGroups[update.PartyIndex] = update;
            SendPacket(update);
        }
    }

    PartyUpdate? FindGroupForRoleAssign(byte partyIndex, WowGuid128 changedUnit)
    {
        var groups = GetSession().GameState.CurrentGroups;
        if (partyIndex < groups.Length && GroupContains(groups[partyIndex], changedUnit))
            return groups[partyIndex];

        foreach (var candidate in groups)
        {
            if (GroupContains(candidate, changedUnit))
                return candidate;
        }

        if (GetSession().GameState.LastAnnouncedPartyIndex < groups.Length)
        {
            var last = groups[GetSession().GameState.LastAnnouncedPartyIndex];
            if (last != null)
                return last;
        }

        return GetSession().GameState.GetCurrentGroup();
    }

    static bool GroupContains(PartyUpdate? group, WowGuid128 guid)
    {
        if (group == null)
            return false;
        foreach (var member in group.PlayerList)
        {
            if (member.GUID == guid)
                return true;
        }
        return false;
    }
}
