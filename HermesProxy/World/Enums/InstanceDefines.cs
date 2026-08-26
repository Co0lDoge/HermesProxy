using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HermesProxy.World.Enums;

 public enum RaidGroupReason
{
    None         = 0,
    Lowlevel     = 1, // "You are too low level to enter this instance."
    Only         = 2, // "You must be in a raid group to enter this instance."
    Full         = 3, // "The instance is full."
    Requirements = 4  // "You do not meet the requirements to enter this instance."
}

public enum ResetFailedReason
{
    Failed  = 0,  // "Cannot reset %s.  There are players still inside the instance."
    Zoning  = 1,  // "Cannot reset %s.  There are players in your party attempting to zone into an instance."
    Offline = 2   // "Cannot reset %s.  There are players offline in your party."
}

public enum InstanceResetWarningType
{
    WarningHours   = 1,                    // WARNING! %s is scheduled to reset in %d hour(s).
    WarningMin     = 2,                    // WARNING! %s is scheduled to reset in %d minute(s)!
    WarningMinSoon = 3,                    // WARNING! %s is scheduled to reset in %d minute(s). Please exit the zone or you will be returned to your bind location!
    Welcome        = 4,                    // Welcome to %s. This raid instance is scheduled to reset in %s.
    Expired        = 5
}

public enum DifficultyLegacy : byte
{
    Normal = 0,
    Heroic = 1,
}

// 3.3.5a raid difficulty. Distinct from DifficultyLegacy (5-man 0/1).
public enum RaidDifficultyLegacy : byte
{
    Raid10Normal = 0,
    Raid25Normal = 1,
    Raid10Heroic = 2,
    Raid25Heroic = 3,
}

public enum DifficultyModern : byte
{
    None = 0,
    Normal = 1,
    Heroic = 2,
    Raid10N = 3,
    Raid25N = 4,
    Raid10HC = 5,
    Raid25HC = 6,
    Raid40 = 9,
    Raid20 = 148,
    RaidClassic10N = 175,
    RaidClassic25N = 176,
    RaidClassic10HC = 193,
    RaidClassic25HC = 194,
}

public static class RaidDifficulties
{
    public static DifficultyModern ToLegacyId(byte acMode)
    {
        return acMode switch
        {
            (byte)RaidDifficultyLegacy.Raid10Normal => DifficultyModern.Raid10N,
            (byte)RaidDifficultyLegacy.Raid25Normal => DifficultyModern.Raid25N,
            (byte)RaidDifficultyLegacy.Raid10Heroic => DifficultyModern.Raid10HC,
            (byte)RaidDifficultyLegacy.Raid25Heroic => DifficultyModern.Raid25HC,
            _ => DifficultyModern.Raid10N,
        };
    }

    public static DifficultyModern ToClassicId(byte acMode)
    {
        return acMode switch
        {
            (byte)RaidDifficultyLegacy.Raid10Normal => DifficultyModern.RaidClassic10N,
            (byte)RaidDifficultyLegacy.Raid25Normal => DifficultyModern.RaidClassic25N,
            (byte)RaidDifficultyLegacy.Raid10Heroic => DifficultyModern.RaidClassic10HC,
            (byte)RaidDifficultyLegacy.Raid25Heroic => DifficultyModern.RaidClassic25HC,
            _ => DifficultyModern.RaidClassic10N,
        };
    }

    public static uint ToLegacy(int modern)
    {
        return modern switch
        {
            (int)DifficultyModern.Raid10N or (int)DifficultyModern.RaidClassic10N => (uint)RaidDifficultyLegacy.Raid10Normal,
            (int)DifficultyModern.Raid25N or (int)DifficultyModern.RaidClassic25N => (uint)RaidDifficultyLegacy.Raid25Normal,
            (int)DifficultyModern.Raid10HC or (int)DifficultyModern.RaidClassic10HC => (uint)RaidDifficultyLegacy.Raid10Heroic,
            (int)DifficultyModern.Raid25HC or (int)DifficultyModern.RaidClassic25HC => (uint)RaidDifficultyLegacy.Raid25Heroic,
            _ => (uint)RaidDifficultyLegacy.Raid10Normal,
        };
    }
}
