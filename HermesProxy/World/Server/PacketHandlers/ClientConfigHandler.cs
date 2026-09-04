using Framework.Logging;
using HermesProxy.World.Enums;
using HermesProxy.World.Server.Packets;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace HermesProxy.World.Server;

public partial class WorldSocket
{
    // Handlers for CMSG opcodes coming from the modern client
    [PacketHandler(Opcode.CMSG_UPDATE_ACCOUNT_DATA)]
    void HandleUpdateAccountData(UserClientUpdateAccountData data)
    {
        byte[] compressed = data.CompressedData;
        Log.Print(LogType.Trace,
            $"[ActionBarTrace] CMSG_UPDATE_ACCOUNT_DATA type={data.DataType} ({(AccountDataType)data.DataType}) " +
            $"uncompressedSize={data.Size} compressedSize={compressed.Length} preview={DescribeCompressedConfigBlob(compressed, data.Size)}");
        GetSession().AccountDataMgr.SaveData(data.PlayerGuid, data.Time, data.DataType, data.Size, compressed);
    }

    [PacketHandler(Opcode.CMSG_REQUEST_ACCOUNT_DATA)]
    void HandleRequestAccountData(RequestAccountData data)
    {
        bool hadSlot = data.DataType < GetSession().AccountDataMgr.Data.Length
            && GetSession().AccountDataMgr.Data[data.DataType] != null;
        Log.Print(LogType.Trace,
            $"[ActionBarTrace] CMSG_REQUEST_ACCOUNT_DATA type={data.DataType} ({(AccountDataType)data.DataType}) hadSlot={hadSlot}");

        if (GetSession().AccountDataMgr.Data[data.DataType] == null)
        {
            Log.Print(LogType.Error, $"Client requested missing account data {data.DataType}.");
            GetSession().AccountDataMgr.Data[data.DataType] = new();
            GetSession().AccountDataMgr.Data[data.DataType].Type = data.DataType;
            GetSession().AccountDataMgr.Data[data.DataType].Timestamp = Time.UnixTime;
            GetSession().AccountDataMgr.Data[data.DataType].UncompressedSize = 0;
            GetSession().AccountDataMgr.Data[data.DataType].CompressedData = new byte[0];
        }

        GetSession().AccountDataMgr.Data[data.DataType].Guid = data.PlayerGuid;
        AccountData stored = GetSession().AccountDataMgr.Data[data.DataType];

        UpdateAccountData update = new(stored);
        SendPacket(update);
    }

    // [ActionBarTrace] Decompress the wire blob and return a printable preview.
    // CMSG_UPDATE_ACCOUNT_DATA payloads are zlib-wrapped Lua-style "SET key val"
    // text; we want to confirm whether the V3_4_3 client sends action bar CVars
    // (bottomLeftActionBar etc.) and via which AccountDataType.
    private static string DescribeCompressedConfigBlob(byte[] compressed, uint uncompressedSize)
    {
        if (compressed == null || compressed.Length == 0)
            return "<empty>";
        try
        {
            using var src = new MemoryStream(compressed);
            using var inflater = new ZLibStream(src, CompressionMode.Decompress);
            int cap = (int)Math.Min(uncompressedSize, 256u);
            if (cap <= 0) cap = 256;
            byte[] buf = new byte[cap];
            int read = 0;
            int n;
            while (read < buf.Length && (n = inflater.Read(buf, read, buf.Length - read)) > 0)
                read += n;
            string text = Encoding.UTF8.GetString(buf, 0, read).Replace('\n', '|').Replace('\r', ' ');
            bool hasActionBarCVar = text.Contains("ActionBar", StringComparison.OrdinalIgnoreCase)
                || text.Contains("actionBar", StringComparison.Ordinal);
            return $"hasActionBarCVar={hasActionBarCVar} text=\"{text}\"";
        }
        catch (Exception ex)
        {
            return $"<inflate failed: {ex.GetType().Name}: {ex.Message}>";
        }
    }

    [PacketHandler(Opcode.CMSG_SAVE_CUF_PROFILES)]
    void HandleUpdateAccountData(SaveCUFProfiles cuf)
    {
        GetSession().AccountDataMgr.SaveCUFProfiles(cuf.Data);
    }
}
