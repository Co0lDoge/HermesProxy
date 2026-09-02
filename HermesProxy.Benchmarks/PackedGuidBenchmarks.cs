using System;
using BenchmarkDotNet.Attributes;
using Framework.IO;
using HermesProxy.World;
using HermesProxy.World.Enums;

namespace HermesProxy.Benchmarks;

// Packed GUID encode/decode, the most common composite field on both wire formats.
// WorldPacket.WritePackedGuid128 goes through PackUInt64, which allocates a byte[8] scratch
// per half (two per GUID); PackedGuidHelper does the same work over stackalloc.
// The WorldPacket read side includes the read-mode WorldPacket object because that is how
// every inbound packet reaches ReadPackedGuid128 today.
[MemoryDiagnoser]
[ShortRunJob]
public class PackedGuidBenchmarks
{
    private WowGuid128 _guid;
    private WorldPacket _writer = null!;
    private byte[] _spanBuffer = null!;
    private byte[] _encoded = null!;

    [GlobalSetup]
    public void Setup()
    {
        _guid = WowGuid128.Create(HighGuidType703.Player, 0, 1, 0x1234_5678_9ABC);
        _writer = new WorldPacket(1u);
        _spanBuffer = new byte[PackedGuidHelper.MaxPackedGuid128Size];

        var writer = new SpanPacketWriter(_spanBuffer);
        writer.WritePackedGuid128(_guid.Low, _guid.High);
        _encoded = _spanBuffer.AsSpan(0, writer.Position).ToArray();
    }

    [GlobalCleanup]
    public void Cleanup() => _writer.Dispose();

    [Benchmark(Baseline = true)]
    public uint WorldPacket_WritePackedGuid128()
    {
        _writer.Clear();
        _writer.WritePackedGuid128(_guid);
        return _writer.GetSize();
    }

    [Benchmark]
    public int SpanWriter_WritePackedGuid128()
    {
        var writer = new SpanPacketWriter(_spanBuffer);
        writer.WritePackedGuid128(_guid.Low, _guid.High);
        return writer.Position;
    }

    [Benchmark]
    public ulong WorldPacket_ReadPackedGuid128()
    {
        var packet = new WorldPacket(1u, _encoded);
        return packet.ReadPackedGuid128().Low;
    }

    [Benchmark]
    public ulong SpanReader_ReadPackedGuid128()
    {
        var reader = new SpanPacketReader(_encoded);
        reader.ReadPackedGuid128(out ulong low, out _);
        return low;
    }
}
