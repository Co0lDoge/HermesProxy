using System;
using BenchmarkDotNet.Attributes;
using Framework.IO;
using HermesProxy.Enums;
using HermesProxy.World;
using HermesProxy.World.Enums;
using HermesProxy.World.Server.Packets;

namespace HermesProxy.Benchmarks;

// Inbound modern-client dispatch, from framed bytes to a populated packet object.
//
// *_Activator reproduces WorldSocket.PacketHandler.Invoke as it runs in production:
// Activator.CreateInstance over the packet type (boxes the argument array), Read(), then a
// closure delegate hop with a downcast. *_Direct drops the reflection so what remains is the
// ClientPacket + WorldPacket objects and the parse itself. *_Span is the floor the planned
// struct + SpanPacketReader path can reach: same bytes, no packet object, no WorldPacket.
//
// The handler target is a dummy object because the real handlers are instance methods on
// WorldSocket and need a live session; the delegate shape is what costs, not the target.
[MemoryDiagnoser]
[ShortRunJob]
public class PacketDispatchBenchmarks
{
    private static readonly object HandlerTarget = new();
    private static uint s_sink;

    private byte[] _buyBackItem = null!;
    private byte[] _setActionButton = null!;
    private byte[] _attackSwing = null!;
    private byte[] _whisper = null!;

    private Action<object, ClientPacket> _buyBackItemHandler = null!;
    private Action<object, ClientPacket> _setActionButtonHandler = null!;
    private Action<object, ClientPacket> _attackSwingHandler = null!;
    private Action<object, ClientPacket> _whisperHandler = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (VersionBootstrap.ModernBuild == ClientVersionBuild.Zero)
            VersionBootstrap.ModernBuild = ClientVersionBuild.V3_4_3_54261;
        if (VersionBootstrap.LegacyBuild == ClientVersionBuild.Zero)
            VersionBootstrap.LegacyBuild = ClientVersionBuild.V3_3_5a_12340;

        var vendor = WowGuid128.Create(HighGuidType703.Creature, 0, 1234, 77);

        _buyBackItem = Frame(w => { w.WritePackedGuid128(vendor); w.WriteUInt32(3); });
        _setActionButton = Frame(w => { w.WriteUInt16(1234); w.WriteUInt16(0); w.WriteUInt8(12); });
        _attackSwing = Frame(w => w.WritePackedGuid128(vendor));
        _whisper = Frame(w =>
        {
            w.WriteUInt32(7);
            w.WriteBits(6, 9);
            w.WriteBits(21, 11);
            w.WriteString("Thrall");
            w.WriteString("hello from the proxy!");
        });

        _buyBackItemHandler = Wrap<BuyBackItem>(static (_, p) => s_sink = p.Slot);
        _setActionButtonHandler = Wrap<SetActionButton>(static (_, p) => s_sink = p.Action);
        _attackSwingHandler = Wrap<AttackSwing>(static (_, p) => s_sink = (uint)p.Victim.Low);
        _whisperHandler = Wrap<ChatMessageWhisper>(static (_, p) => s_sink = (uint)(p.Text.Length + p.Target.Length));

        // Fail loudly if the span parse disagrees with the ByteBuffer parse; a wrong floor
        // is worse than no floor.
        using (var reference = new ChatMessageWhisper(new WorldPacket(_whisper)))
        {
            reference.Read();
            var r = new SpanPacketReader(_whisper.AsSpan(2));
            r.ReadUInt32();
            int targetLen = (int)r.ReadBits<uint>(9);
            int textLen = (int)r.ReadBits<uint>(11);
            string target = r.ReadString(targetLen);
            string text = r.ReadString(textLen);
            if (target != reference.Target || text != reference.Text)
                throw new InvalidOperationException($"Span parse mismatch: '{target}'/'{text}' vs '{reference.Target}'/'{reference.Text}'");
        }
    }

    // Same closure shape as WorldSocket.PacketHandler.CreateDelegate<P1>.
    private static Action<object, ClientPacket> Wrap<P1>(Action<object, P1> typed) where P1 : ClientPacket
        => (target, p) => typed(target, (P1)p);

    // WorldPacket(byte[]) consumes a 2-byte opcode prefix before the body, exactly like the
    // buffer WorldSocket.ReadData hands to PacketHandler.Invoke.
    private static byte[] Frame(Action<WorldPacket> body)
    {
        using var payload = new WorldPacket(1u);
        body(payload);
        byte[] data = payload.GetData();
        var framed = new byte[data.Length + 2];
        data.CopyTo(framed, 2);
        return framed;
    }

    private static uint InvokeViaActivator(Type packetType, byte[] frame, Action<object, ClientPacket> handler)
    {
        var worldPacket = new WorldPacket(frame);
        using var clientPacket = (ClientPacket)Activator.CreateInstance(packetType, worldPacket)!;
        clientPacket.Read();
        handler(HandlerTarget, clientPacket);
        return s_sink;
    }

    // ---- BuyBackItem: packed GUID + uint ----

    [Benchmark(Baseline = true)]
    public uint BuyBackItem_Activator() => InvokeViaActivator(typeof(BuyBackItem), _buyBackItem, _buyBackItemHandler);

    [Benchmark]
    public uint BuyBackItem_Direct()
    {
        using var packet = new BuyBackItem(new WorldPacket(_buyBackItem));
        packet.Read();
        _buyBackItemHandler(HandlerTarget, packet);
        return s_sink;
    }

    [Benchmark]
    public uint BuyBackItem_Span()
    {
        var r = new SpanPacketReader(_buyBackItem.AsSpan(2));
        r.ReadPackedGuid128(out ulong low, out ulong high);
        var vendor = new WowGuid128(low, high);
        uint slot = r.ReadUInt32();
        s_sink = slot + (uint)vendor.Low;
        return s_sink;
    }

    // ---- SetActionButton: three small integers ----

    [Benchmark]
    public uint SetActionButton_Activator() => InvokeViaActivator(typeof(SetActionButton), _setActionButton, _setActionButtonHandler);

    [Benchmark]
    public uint SetActionButton_Direct()
    {
        using var packet = new SetActionButton(new WorldPacket(_setActionButton));
        packet.Read();
        _setActionButtonHandler(HandlerTarget, packet);
        return s_sink;
    }

    [Benchmark]
    public uint SetActionButton_Span()
    {
        var r = new SpanPacketReader(_setActionButton.AsSpan(2));
        ushort action = r.ReadUInt16();
        ushort type = r.ReadUInt16();
        byte index = r.ReadUInt8();
        s_sink = (uint)(action + type + index);
        return s_sink;
    }

    // ---- AttackSwing: single packed GUID, the most frequent combat CMSG ----

    [Benchmark]
    public uint AttackSwing_Activator() => InvokeViaActivator(typeof(AttackSwing), _attackSwing, _attackSwingHandler);

    [Benchmark]
    public uint AttackSwing_Direct()
    {
        using var packet = new AttackSwing(new WorldPacket(_attackSwing));
        packet.Read();
        _attackSwingHandler(HandlerTarget, packet);
        return s_sink;
    }

    [Benchmark]
    public uint AttackSwing_Span()
    {
        var r = new SpanPacketReader(_attackSwing.AsSpan(2));
        r.ReadPackedGuid128(out ulong low, out _);
        s_sink = (uint)low;
        return s_sink;
    }

    // ---- ChatMessageWhisper: bit-packed lengths + two strings (strings must allocate) ----

    [Benchmark]
    public uint Whisper_Activator() => InvokeViaActivator(typeof(ChatMessageWhisper), _whisper, _whisperHandler);

    [Benchmark]
    public uint Whisper_Direct()
    {
        using var packet = new ChatMessageWhisper(new WorldPacket(_whisper));
        packet.Read();
        _whisperHandler(HandlerTarget, packet);
        return s_sink;
    }

    [Benchmark]
    public uint Whisper_Span()
    {
        var r = new SpanPacketReader(_whisper.AsSpan(2));
        r.ReadUInt32();
        int targetLen = (int)r.ReadBits<uint>(9);
        int textLen = (int)r.ReadBits<uint>(11);
        string target = r.ReadString(targetLen);
        string text = r.ReadString(textLen);
        s_sink = (uint)(text.Length + target.Length);
        return s_sink;
    }
}
