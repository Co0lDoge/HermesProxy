using Framework;
using HermesProxy.Enums;
using HermesProxy.World.Enums;
using HermesProxy.World.Objects;
using HermesProxy.World.Server.Packets;
using System;

namespace HermesProxy.World.Client;

public partial class WorldClient
{
    // Handlers for SMSG opcodes coming the legacy world server
    [PacketHandler(Opcode.SMSG_PONG)]
    void HandlePingResponse(WorldPacket packet)
    {
        uint serial = packet.ReadUInt32();
        if ((serial & 0x80000000) != 0)
            return; // keepalive pong, don't forward to modern client
        SendPacketToClient(new Pong(serial));
    }

    [PacketHandler(Opcode.SMSG_TUTORIAL_FLAGS)]
    void HandleTutorialFlags(WorldPacket packet)
    {
        TutorialFlags tutorials = new TutorialFlags();
        for (byte i = 0; i < (byte)Tutorials.Max; ++i)
            tutorials.TutorialData[i] = packet.ReadUInt32();
        SendPacketToClient(tutorials);
    }

    [PacketHandler(Opcode.SMSG_ACCOUNT_DATA_TIMES)]
    void HandleAccountDataTimes(WorldPacket packet)
    {
        GetSession().RealmSocket.SendAccountDataTimes();

        // These packets don't exist in Vanilla and we must send them here.
        if (LegacyVersion.RemovedInVersion(ClientVersionBuild.V2_0_1_6180))
        {
            GetSession().RealmSocket.SendFeatureSystemStatus();
            GetSession().RealmSocket.SendMotd();
            GetSession().RealmSocket.SendSetTimeZoneInformation();
            GetSession().RealmSocket.SendSeasonInfo();
        }
    }

    [PacketHandler(Opcode.SMSG_BIND_POINT_UPDATE)]
    void HandleBindPointUpdate(WorldPacket packet)
    {
        BindPointUpdate point = new BindPointUpdate();
        point.BindPosition = packet.ReadVector3();
        point.BindMapID = packet.ReadUInt32();
        point.BindAreaID = packet.ReadUInt32();
        SendPacketToClient(point);
    }

    [PacketHandler(Opcode.SMSG_PLAYER_BOUND)]
    void HandlePlayerBound(WorldPacket packet)
    {
        PlayerBound bound = new PlayerBound();
        bound.BinderGUID = packet.ReadGuid().To128(GetSession().GameState);
        bound.AreaID = packet.ReadUInt32();
        SendPacketToClient(bound);
    }

    [PacketHandler(Opcode.SMSG_DEATH_RELEASE_LOC)]
    void HandleDeathReleaseLoc(WorldPacket packet)
    {
        DeathReleaseLoc death = new();
        death.MapID = packet.ReadInt32();
        death.Location = packet.ReadVector3();
        SendPacketToClient(death);
    }

    [PacketHandler(Opcode.SMSG_PRE_RESSURECT)]
    void HandlePreRessurect(WorldPacket packet)
    {
        PreRessurect pre = new();
        pre.PlayerGUID = packet.ReadPackedGuid().To128(GetSession().GameState);
        SendPacketToClient(pre);
    }

    [PacketHandler(Opcode.SMSG_CORPSE_RECLAIM_DELAY)]
    void HandleCorpseReclaimDelay(WorldPacket packet)
    {
        CorpseReclaimDelay delay = new CorpseReclaimDelay();
        delay.Remaining = packet.ReadUInt32();
        SendPacketToClient(delay);
    }

    [PacketHandler(Opcode.SMSG_TIME_SYNC_REQUEST)]
    void HandleTimeSyncRequest(WorldPacket packet)
    {
        TimeSyncRequest sync = new TimeSyncRequest();
        sync.SequenceIndex = packet.ReadUInt32();
        SendPacketToClient(sync);
    }

    [PacketHandler(Opcode.SMSG_WEATHER)]
    void HandleWeather(WorldPacket packet)
    {
        WeatherPkt weather = new WeatherPkt();
        if (LegacyVersion.RemovedInVersion(ClientVersionBuild.V2_0_1_6180))
        {
            WeatherType type = (WeatherType)packet.ReadUInt32();
            weather.Intensity = packet.ReadFloat();
            weather.WeatherID = Weather.ConvertWeatherTypeToWeatherState(type, weather.Intensity);
            packet.ReadUInt32(); // sound
            if (packet.CanRead())
                weather.Abrupt = packet.ReadBool();
        }
        else
        {
            weather.WeatherID = (WeatherState)packet.ReadUInt32();
            weather.Intensity = packet.ReadFloat();
            weather.Abrupt = packet.ReadBool();
        }
        SendPacketToClient(weather);
        SendPacketToClient(new StartLightningStorm());
    }

    [PacketHandler(Opcode.SMSG_LOGIN_SET_TIME_SPEED)]
    void HandleLoginSetTimeSpeed(WorldPacket packet)
    {
        if (!GetSession().GameState.IsFirstEnterWorld)
            return;

        LoginSetTimeSpeed login = new LoginSetTimeSpeed();
        login.ServerTime = packet.ReadUInt32();
        login.GameTime = login.ServerTime;
        login.NewSpeed = packet.ReadFloat();
        if (LegacyVersion.AddedInVersion(ClientVersionBuild.V3_1_2_9901))
        {
            login.ServerTimeHolidayOffset = packet.ReadInt32();
            login.GameTimeHolidayOffset = login.ServerTimeHolidayOffset;
        }
        SendPacketToClient(login);
    }

    [PacketHandler(Opcode.SMSG_AREA_TRIGGER_MESSAGE)]
    void HandleAreaTriggerMessage(WorldPacket packet)
    {
        uint length = packet.ReadUInt32();
        string message = packet.ReadString(length);

        if (GetSession().GameState.LastEnteredAreaTrigger != 0)
        {
            AreaTriggerMessage denied = new AreaTriggerMessage();
            denied.AreaTriggerID = GetSession().GameState.LastEnteredAreaTrigger;
            SendPacketToClient(denied);
        }
        else
        {
            ChatPkt chat = new ChatPkt(GetSession(), ChatMessageTypeModern.System, message);
            SendPacketToClient(chat);
        }
    }

    [PacketHandler(Opcode.MSG_CORPSE_QUERY)]
    void HandleCorpseQuery(WorldPacket packet)
    {
        CorpseLocation corpse = new()
        {
            Player = GetSession().GameState.CurrentPlayerGuid,
            Transport = WowGuid128.Empty,
        };

        corpse.Valid = packet.ReadBool();
        if (corpse.Valid)
        {
            corpse.ActualMapID = packet.ReadInt32();
            corpse.Position = packet.ReadVector3();
            corpse.MapID = packet.ReadInt32();
            if (LegacyVersion.AddedInVersion(ClientVersionBuild.V3_2_2_10482))
                packet.ReadInt32(); // Corpse Low GUID
        }
        else
        {
            corpse.MapID = corpse.ActualMapID = (int)GetSession().GameState.CurrentMapId!;
        }

        SendPacketToClient(corpse);
    }

    [PacketHandler(Opcode.SMSG_STAND_STATE_UPDATE)]
    void HandleStandStateUpdate(WorldPacket packet)
    {
        StandStateUpdate state = new();
        state.StandState = packet.ReadUInt8();
        SendPacketToClient(state);
    }

    [PacketHandler(Opcode.SMSG_EXPLORATION_EXPERIENCE)]
    void HandleExplorationExperience(WorldPacket packet)
    {
        ExplorationExperience explore = new();
        explore.AreaID = packet.ReadUInt32();
        explore.Experience = packet.ReadUInt32();
        SendPacketToClient(explore);
    }

    [PacketHandler(Opcode.SMSG_PLAY_MUSIC)]
    void HandlePlayMusic(WorldPacket packet)
    {
        PlayMusic music = new();
        music.SoundEntryID = packet.ReadUInt32();
        SendPacketToClient(music);
    }

    [PacketHandler(Opcode.SMSG_PLAY_SOUND)]
    void HandlePlaySound(WorldPacket packet)
    {
        PlaySound sound = new();
        sound.SoundEntryID = packet.ReadUInt32();
        sound.SourceObjectGuid = GetSession().GameState.CurrentPlayerGuid;
        SendPacketToClient(sound);
    }

    [PacketHandler(Opcode.SMSG_PLAY_OBJECT_SOUND)]
    void HandlePlayObjectSound(WorldPacket packet)
    {
        PlayObjectSound sound = new();
        sound.SoundEntryID = packet.ReadUInt32();
        sound.SourceObjectGUID = packet.ReadGuid().To128(GetSession().GameState);
        sound.TargetObjectGUID = sound.SourceObjectGUID;
        SendPacketToClient(sound);
    }

    [PacketHandler(Opcode.SMSG_TRIGGER_CINEMATIC)]
    void HandleTriggerCinematic(WorldPacket packet)
    {
        TriggerCinematic cinematic = new();
        cinematic.CinematicID = packet.ReadUInt32();
        SendPacketToClient(cinematic);
    }

    [PacketHandler(Opcode.SMSG_SPECIAL_MOUNT_ANIM)]
    void HandleSpecialMountAnim(WorldPacket packet)
    {
        SpecialMountAnim mount = new();
        mount.UnitGUID = packet.ReadGuid().To128(GetSession().GameState);
        SendPacketToClient(mount);
    }

    [PacketHandler(Opcode.SMSG_START_MIRROR_TIMER)]
    void HandleStartMirrorTimer(WorldPacket packet)
    {
        StartMirrorTimer timer = new();
        timer.Timer = (MirrorTimerType)packet.ReadUInt32();
        timer.Value = packet.ReadInt32();
        timer.MaxValue = packet.ReadInt32();
        timer.Scale = packet.ReadInt32();
        timer.Paused = packet.ReadBool();
        timer.SpellID = packet.ReadInt32();
        SendPacketToClient(timer);
    }

    [PacketHandler(Opcode.SMSG_PAUSE_MIRROR_TIMER)]
    void HandlePauseMirrorTimer(WorldPacket packet)
    {
        PauseMirrorTimer timer = new();
        timer.Timer = (MirrorTimerType)packet.ReadUInt32();
        timer.Paused = packet.ReadBool();
        SendPacketToClient(timer);
    }

    [PacketHandler(Opcode.SMSG_STOP_MIRROR_TIMER)]
    void HandleStopMirrorTimer(WorldPacket packet)
    {
        StopMirrorTimer timer = new();
        timer.Timer = (MirrorTimerType)packet.ReadUInt32();
        SendPacketToClient(timer);
    }

    [PacketHandler(Opcode.SMSG_INVALIDATE_PLAYER)]
    void HandleInvalidatePlayer(WorldPacket packet)
    {
        InvalidatePlayer invalidate = new();
        invalidate.Guid = packet.ReadGuid().To128(GetSession().GameState);
        SendPacketToClient(invalidate);

        if (GetSession().GameState.CachedPlayers.ContainsKey(invalidate.Guid))
            GetSession().GameState.CachedPlayers.Remove(invalidate.Guid);
    }

    [PacketHandler(Opcode.SMSG_ZONE_UNDER_ATTACK)]
    void HandleZoneUnderAttack(WorldPacket packet)
    {
        ZoneUnderAttack zone = new();
        zone.AreaID = packet.ReadInt32();
        SendPacketToClient(zone);
    }

    [PacketHandler(Opcode.MSG_SET_DUNGEON_DIFFICULTY)]
    void HandleSetDungeonDifficulty(WorldPacket packet)
    {
        DungeonDifficultySet difficulty = new();
        int difficultyId = packet.ReadInt32();
        difficulty.DifficultyID = (byte)((DifficultyLegacy)difficultyId).CastEnum<DifficultyModern>();
        packet.ReadInt32(); // always 1
        packet.ReadInt32(); // IsInGroup
        SendPacketToClient(difficulty);

        RefreshGroupDifficulty(dungeon: (DifficultyModern)difficulty.DifficultyID, raidLegacyMode: null);
    }

    /// <summary>
    /// While in a group the 3.4.3 difficulty pickers read the party's own difficulty settings, not
    /// SMSG_SET_DUNGEON_DIFFICULTY / SMSG_RAID_DIFFICULTY_SET - those only drive the chat line.
    /// Legacy announces a change with MSG_SET_*_DIFFICULTY and never re-sends SMSG_GROUP_LIST, so
    /// the party settings the client received when the group formed were never updated and the
    /// picker kept showing whatever the group started on. Re-issue the party update with the new
    /// values so the tick lands on the entry the player actually chose.
    /// </summary>
    private void RefreshGroupDifficulty(DifficultyModern? dungeon, byte? raidLegacyMode)
    {
        var group = GetSession().GameState.GetCurrentGroup();
        if (group?.DifficultySettings == null)
            return;

        if (dungeon != null)
            group.DifficultySettings.DungeonDifficultyID = dungeon.Value;

        if (raidLegacyMode != null)
        {
            // A native 3.4.3 server puts the chosen difficulty in RaidDifficultyID (3-6) and
            // leaves LegacyRaidDifficultyID at its default - captured from Wrathion, where
            // picking 10N then 25N gives RaidDifficultyID 3 then 4 with LegacyRaidDifficultyID
            // pinned at 3 throughout. The picker reads RaidDifficultyID, so writing the Classic
            // ids (175/176/193/194) there left no row highlighted no matter what was chosen.
            group.DifficultySettings.RaidDifficultyID = RaidDifficulties.ToLegacyId(raidLegacyMode.Value);
        }

        // The cached PartyUpdate has already been written once and its buffer disposed, so
        // re-emitting the instance would resend the original bytes - the difficulty edits above
        // and even the new SequenceNum would never reach the client. Clone first.
        var refreshed = group.CloneUnwritten();
        refreshed.SequenceNum = GetSession().GameState.GroupUpdateCounter++;
        group.SequenceNum = refreshed.SequenceNum;
        SendPacketToClient(refreshed);
    }

    [PacketHandler(Opcode.MSG_SET_RAID_DIFFICULTY)]
    void HandleSetRaidDifficulty(WorldPacket packet)
    {
        RaidDifficultySet difficulty = new();
        byte legacyRaidMode = (byte)packet.ReadUInt32();
        difficulty.DifficultyID = (int)RaidDifficulties.ToLegacyId(legacyRaidMode);

        // Legacy selects which id family DifficultyID carries - native picks
        // GetLegacyRaidDifficultyID() when set and GetRaidDifficultyID() otherwise, and
        // HandleSetRaidDifficultyOpcode rejects a request whose flag disagrees with the
        // difficulty's DIFFICULTY_FLAG_LEGACY. The ids written above (3-6) are not legacy-flagged
        // on 3.4.3: the client itself sends them with Legacy 0 for all four raid sizes. Claiming
        // Legacy here filed the answer under the legacy slot, so the picker never showed a
        // selection and fell back to 25-player.
        difficulty.Legacy = (byte)(ModernVersion.Build == ClientVersionBuild.V3_4_3_54261 ? 0 : 1);
        packet.ReadInt32(); // always 1
        packet.ReadInt32(); // IsInGroup
        SendPacketToClient(difficulty);

        RefreshGroupDifficulty(dungeon: null, raidLegacyMode: legacyRaidMode);
    }
}
