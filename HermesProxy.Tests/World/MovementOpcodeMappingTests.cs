using System;
using System.Collections.Generic;
using HermesProxy.World.Enums;
using Xunit;

namespace HermesProxy.Tests.World;

/// <summary>
/// WorldSocket.HandlePlayerMove translates the modern client's CMSG_MOVE_* opcode into the
/// legacy MSG_MOVE_* wire value. That used to run, per movement packet, an enum ToString, a
/// string Replace and a reflection-backed Enum.TryParse against the version's opcode enum;
/// it now uses a prebuilt CMSG->MSG map plus LegacyVersion's array lookup.
///
/// These tests pin the two mechanisms to the same answer. Movement is the highest-rate client
/// opcode, and a silent divergence here would send the backend the wrong opcode for a whole
/// class of movement -- the sort of thing that shows up as rubber-banding, not as an error.
/// </summary>
public class MovementOpcodeMappingTests
{
    private static IEnumerable<Opcode> ClientMoveOpcodes()
    {
        foreach (Opcode op in Enum.GetValues<Opcode>())
        {
            if (op.ToString().StartsWith("CMSG_MOVE", StringComparison.Ordinal))
                yield return op;
        }
    }

    [Fact]
    public void ArrayLookup_MatchesReflectionLookup_ForEveryClientMoveOpcode()
    {
        var divergences = new List<string>();
        int compared = 0;

        foreach (Opcode op in ClientMoveOpcodes())
        {
            // Exactly the string the old code built: Replace("CMSG", "MSG").
            string legacyName = op.ToString().Replace("CMSG", "MSG");
            uint viaReflection = Opcodes.GetOpcodeValueForVersion(legacyName, global::HermesProxy.LegacyVersion.Build);

            if (!Enum.TryParse(legacyName, out Opcode universalMsg))
            {
                // No MSG_ twin in the universal enum means the new map has no entry and the
                // handler falls back. That is only safe if the old path also found nothing.
                if (viaReflection != 0)
                    divergences.Add($"{op}: universal enum has no {legacyName}, but the version enum resolves it to {viaReflection}");
                continue;
            }

            uint viaArray = global::HermesProxy.LegacyVersion.GetCurrentOpcode(universalMsg);
            if (viaArray != viaReflection)
                divergences.Add($"{op} -> {legacyName}: reflection={viaReflection} array={viaArray}");
            compared++;
        }

        Assert.True(compared > 0, "no CMSG_MOVE_* opcodes were compared - the enum scan found nothing");
        Assert.Empty(divergences);
    }

    [Fact]
    public void FallbackOpcode_ResolvesIdentically()
    {
        Assert.Equal(
            Opcodes.GetOpcodeValueForVersion("MSG_MOVE_SET_FACING", global::HermesProxy.LegacyVersion.Build),
            global::HermesProxy.LegacyVersion.GetCurrentOpcode(Opcode.MSG_MOVE_SET_FACING));
    }
}
