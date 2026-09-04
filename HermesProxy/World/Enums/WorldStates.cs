using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HermesProxy.World.Enums;

public enum WorldStates : uint
{
    WsgFlagStateHorde    = 2338,
    WsgFlagStateAlliance = 2339,

    // WotLK PvP "Battlegrounds" frame enable flags. The 3.4.3 client gates these BG
    // rows behind BattlemasterList.Required_Player_Condition_ID -> PlayerCondition ->
    // WorldStateExpression, which tests these world states. Must be >= 1 (IoC == 1) for
    // the BG to list. EOTS/SOTA/Random use Required_Player_Condition_ID 0 (always show).
    BattlegroundAlteracValleyEnabled  = 17224,
    BattlegroundWarsongGulchEnabled   = 17225,
    BattlegroundArathiBasinEnabled    = 17227,
    BattlegroundIsleOfConquestEnabled = 21975,
}
