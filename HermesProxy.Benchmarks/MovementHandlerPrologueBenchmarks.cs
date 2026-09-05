using BenchmarkDotNet.Attributes;
using HermesProxy.Enums;
using HermesProxy.World.Enums;

namespace HermesProxy.Benchmarks;

/// <summary>
/// Isolates the opcode-translation prologue that <c>WorldSocket.HandlePlayerMove</c> runs on
/// every movement packet (MovementHandler.cs). The live <c>--metrics</c> allocation figure for
/// CMSG_MOVE_* averaged ~18 KB per packet in a battleground, which the handler body — one
/// pooled WorldPacket and a MovementInfo write — cannot account for. These benchmarks split
/// the prologue into its three steps so the cost can be attributed instead of guessed at.
///
/// <c>Prologue_AsWritten</c> reproduces the handler exactly:
///     string opcodeName = movement.GetUniversalOpcode().ToString();
///     opcodeName = opcodeName.Replace("CMSG", "MSG");
///     uint opcode = Opcodes.GetOpcodeValueForVersion(opcodeName, LegacyVersion.Build);
///
/// <c>Prologue_ArrayPath</c> is the comparison: the array-backed translation the rest of the
/// proxy uses (the one OpcodeLookupBenchmarks covers), which needs no string round trip.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class MovementHandlerPrologueBenchmarks
{
    private const int Iterations = 256;

    private Opcode[] _movementOpcodes = null!;
    private System.Collections.Frozen.FrozenDictionary<Opcode, Opcode> _clientMoveToLegacyMsg = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (global::HermesProxy.VersionBootstrap.ModernBuild == ClientVersionBuild.Zero)
            global::HermesProxy.VersionBootstrap.ModernBuild = ClientVersionBuild.V3_4_3_54261;
        if (global::HermesProxy.VersionBootstrap.LegacyBuild == ClientVersionBuild.Zero)
            global::HermesProxy.VersionBootstrap.LegacyBuild = ClientVersionBuild.V3_3_5a_12340;

        _ = global::HermesProxy.LegacyVersion.GetUniversalOpcode(0);
        _ = global::HermesProxy.ModernVersion.GetUniversalOpcode(0);

        // Same construction as WorldSocket.BuildClientMoveToLegacyMsg.
        var map = new System.Collections.Generic.Dictionary<Opcode, Opcode>();
        foreach (Opcode op in System.Enum.GetValues<Opcode>())
        {
            string n = op.ToString();
            if (!n.StartsWith("CMSG_MOVE", System.StringComparison.Ordinal))
                continue;
            if (System.Enum.TryParse("MSG_" + n.Substring("CMSG_".Length), out Opcode legacyMsg))
                map[op] = legacyMsg;
        }
        _clientMoveToLegacyMsg = System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(map);

        // The opcodes that actually dominate the live C->S table, in the observed mix.
        _movementOpcodes =
        [
            Opcode.CMSG_MOVE_SET_FACING_HEARTBEAT,
            Opcode.CMSG_MOVE_HEARTBEAT,
            Opcode.CMSG_MOVE_SET_FACING,
            Opcode.CMSG_MOVE_STOP_STRAFE,
            Opcode.CMSG_MOVE_SET_PITCH,
            Opcode.CMSG_MOVE_STOP,
            Opcode.CMSG_MOVE_START_STRAFE_LEFT,
            Opcode.CMSG_MOVE_START_STRAFE_RIGHT,
        ];
    }

    /// <summary>Exactly what HandlePlayerMove does today, once per movement packet.</summary>
    [Benchmark(Baseline = true)]
    public uint Prologue_AsWritten()
    {
        uint acc = 0;
        for (int i = 0; i < Iterations; i++)
        {
            var universal = _movementOpcodes[i % _movementOpcodes.Length];
            string opcodeName = universal.ToString();
            opcodeName = opcodeName.Replace("CMSG", "MSG");
            uint opcode = Opcodes.GetOpcodeValueForVersion(opcodeName, global::HermesProxy.LegacyVersion.Build);
            if (opcode == 0)
                opcode = Opcodes.GetOpcodeValueForVersion("MSG_MOVE_SET_FACING", global::HermesProxy.LegacyVersion.Build);
            acc += opcode;
        }
        return acc;
    }

    /// <summary>Just the enum-name round trip, no version lookup.</summary>
    [Benchmark]
    public int Prologue_StringRoundTripOnly()
    {
        int acc = 0;
        for (int i = 0; i < Iterations; i++)
        {
            var universal = _movementOpcodes[i % _movementOpcodes.Length];
            string opcodeName = universal.ToString();
            opcodeName = opcodeName.Replace("CMSG", "MSG");
            acc += opcodeName.Length;
        }
        return acc;
    }

    /// <summary>Just the reflection-backed Enum.TryParse lookup, on a pre-built name.</summary>
    [Benchmark]
    public uint Prologue_VersionLookupOnly()
    {
        uint acc = 0;
        for (int i = 0; i < Iterations; i++)
            acc += Opcodes.GetOpcodeValueForVersion("MSG_MOVE_SET_FACING", global::HermesProxy.LegacyVersion.Build);
        return acc;
    }

    /// <summary>The shipped replacement: prebuilt CMSG-&gt;MSG map, then the array lookup.</summary>
    [Benchmark]
    public uint Prologue_AsShipped()
    {
        uint acc = 0;
        for (int i = 0; i < Iterations; i++)
        {
            var universal = _movementOpcodes[i % _movementOpcodes.Length];
            uint opcode = _clientMoveToLegacyMsg.TryGetValue(universal, out Opcode legacyMsg)
                ? global::HermesProxy.LegacyVersion.GetCurrentOpcode(legacyMsg)
                : 0u;
            if (opcode == 0)
                opcode = global::HermesProxy.LegacyVersion.GetCurrentOpcode(Opcode.MSG_MOVE_SET_FACING);
            acc += opcode;
        }
        return acc;
    }
}
