namespace HermesProxy.World.Enums;

enum LegacyGmTicketResponse : uint
{
    TicketDoesNotExist  = 0,
    AlreadyExist        = 1,
    CreateSuccess       = 2,
    CreateError         = 3,
    UpdateSuccess       = 4,
    UpdateError         = 5,
    TicketDeleted       = 9,
};

public enum GmTicketSystemStatus
{
    TicketQueueDisables = 0,
    TicketQueueEnabled = 1,
}

/// <summary>
/// V3_4_3 replaced the single <see cref="GmTicketComplaintType"/> with a report type plus a
/// major/minor category pair. Values confirmed against native captures of every report category
/// the 3.4.3 client offers, and against SupportMgr.h in the native source.
/// </summary>
public enum ReportType
{
    Chat = 0,
    InWorld = 1,
    ClubFinderPosting = 2,
    ClubFinderApplicant = 3,
    GroupFinderPosting = 4,
    GroupFinderApplicant = 5,
    ClubMember = 6,
    GroupMember = 7,
    Friend = 8,
    Pet = 9,
    BattlePet = 10,
    Calendar = 11,
    Mail = 12,
    PvP = 13,
}

public enum ReportMajorCategory
{
    InappropriateCommunication = 0,
    GameplaySabotage = 1,
    Cheating = 2,
    InappropriateName = 3,
}

[System.Flags]
public enum ReportMinorCategory
{
    None = 0,
    TextChat = 0x0001,
    Boosting = 0x0002,
    Spam = 0x0004,
    Afk = 0x0008,
    IntentionallyFeeding = 0x0010,
    BlockingProgress = 0x0020,
    Hacking = 0x0040,
    Botting = 0x0080,
    Advertisement = 0x0100,
    BTag = 0x0200,
    GroupName = 0x0400,
    CharacterName = 0x0800,
    GuildName = 0x1000,
    Description = 0x2000,
    Name = 0x4000,
}

public enum GmTicketComplaintType
{
    Unknown = 0,
    Name = 3,
    Cheating = 4,
    ChatSpam = 9,
    BadLanguageUsed = 11,
    GuildName = 12,
    MailSpam = 15,
}
