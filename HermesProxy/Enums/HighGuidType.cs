﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HermesProxy.Enums
{
    public enum HighGuidType
    {
        Null = 0,
        Uniq,
        Player,
        Item,
        WorldTransaction,
        StaticDoor,
        Transport,
        Conversation,
        Creature,
        Vehicle,
        Pet,
        GameObject,
        DynamicObject,
        AreaTrigger,
        Corpse,
        LootObject,
        SceneObject,
        Scenario,
        AIGroup,
        DynamicDoor,
        ClientActor,
        Vignette,
        CallForHelp,
        AIResource,
        AILock,
        AILockTicket,
        ChatChannel,
        Party,
        Guild,
        WowAccount,
        BNetAccount,
        GMTask,
        MobileSession,
        RaidGroup,
        Spell,
        Mail,
        WebObj,
        LFGObject,
        LFGList,
        UserRouter,
        PVPQueueGroup,
        UserClient,
        PetBattle,
        UniqUserClient,
        BattlePet,
        CommerceObj,
        ClientSession,
        Cast,
        Invalid
    };

    public enum HighGuidType624
    {
        Null = 0,
        Uniq = 1,
        Player = 2,
        Item = 3,
        WorldTransaction = 4,
        StaticDoor = 5,
        Transport = 6,
        Conversation = 7,
        Creature = 8,
        Vehicle = 9,
        Pet = 10,
        GameObject = 11,
        DynamicObject = 12,
        AreaTrigger = 13,
        Corpse = 14,
        LootObject = 15,
        SceneObject = 16,
        Scenario = 17,
        AIGroup = 18,
        DynamicDoor = 19,
        ClientActor = 20,
        Vignette = 21,
        CallForHelp = 22,
        AIResource = 23,
        AILock = 24,
        AILockTicket = 25,
        ChatChannel = 26,
        Party = 27,
        Guild = 28,
        WowAccount = 29,
        BNetAccount = 30,
        GMTask = 31,
        MobileSession = 32,
        RaidGroup = 33,
        Spell = 34,
        Mail = 35,
        WebObj = 36,
        LFGObject = 37,
        LFGList = 38,
        UserRouter = 39,
        PVPQueueGroup = 40,
        UserClient = 41,
        PetBattle = 42,
        UniqUserClient = 43,
        BattlePet = 44,
        CommerceObj = 45,
        ClientSession = 46,
    };

    public enum HighGuidType623
    {
        Null = 0,
        Uniq = 1,
        Player = 2,
        Item = 3,
        StaticDoor = 4,
        Transport = 5,
        Conversation = 6,
        Creature = 7,
        Vehicle = 8,
        Pet = 9,
        GameObject = 10,
        DynamicObject = 11,
        AreaTrigger = 12,
        Corpse = 13,
        LootObject = 14,
        SceneObject = 15,
        Scenario = 16,
        AIGroup = 17,
        DynamicDoor = 18,
        ClientActor = 19,
        Vignette = 20,
        CallForHelp = 21,
        AIResource = 22,
        AILock = 23,
        AILockTicket = 24,
        ChatChannel = 25,
        Party = 26,
        Guild = 27,
        WowAccount = 28,
        BNetAccount = 29,
        GMTask = 30,
        MobileSession = 31,
        RaidGroup = 32,
        Spell = 33,
        Mail = 34,
        WebObj = 35,
        LFGObject = 36,
        LFGList = 37,
        UserRouter = 38,
        PVPQueueGroup = 39,
        UserClient = 40,
        PetBattle = 41,
        UniqueUserClient = 42,
        BattlePet = 43
    }

    public enum HighGuidType703
    {
        Null = 0,
        Uniq = 1,
        Player = 2,
        Item = 3,
        WorldTransaction = 4,
        StaticDoor = 5,
        Transport = 6,
        Conversation = 7,
        Creature = 8,
        Vehicle = 9,
        Pet = 10,
        GameObject = 11,
        DynamicObject = 12,
        AreaTrigger = 13,
        Corpse = 14,
        LootObject = 15,
        SceneObject = 16,
        Scenario = 17,
        AIGroup = 18,
        DynamicDoor = 19,
        ClientActor = 20,
        Vignette = 21,
        CallForHelp = 22,
        AIResource = 23,
        AILock = 24,
        AILockTicket = 25,
        ChatChannel = 26,
        Party = 27,
        Guild = 28,
        WowAccount = 29,
        BNetAccount = 30,
        GMTask = 31,
        MobileSession = 32,
        RaidGroup = 33,
        Spell = 34,
        Mail = 35,
        WebObj = 36,
        LFGObject = 37,
        LFGList = 38,
        UserRouter = 39,
        PVPQueueGroup = 40,
        UserClient = 41,
        PetBattle = 42,
        UniqUserClient = 43,
        BattlePet = 44,
        CommerceObj = 45,
        ClientSession = 46,
        Cast = 47,

        Invalid = 63
    }

    public enum HighGuidTypeLegacy
    {
        None = -1,
        Player = 0x000, // Seen 0x280 for players too
        BattleGround1 = 0x101,
        InstanceSave = 0x104,
        Group = 0x105,
        BattleGround2 = 0x109,
        MOTransport = 0x10C,
        Unknown270 = 0x10E, // pets and mounts?
        Guild = 0x10F,
        Item = 0x400, // Container
        DynObject = 0xF00, // Corpses
        GameObject = 0xF01,
        Transport = 0xF02,
        Unit = 0xF03,
        Pet = 0xF04,
        Vehicle = 0xF05
    }
}
