using System;
using System.Collections.Generic;
using Framework.GameMath;
using HermesProxy.Enums;
using Framework.Logging;
using HermesProxy.World.Enums;

namespace HermesProxy.World.Server.Packets;

public class SupportTicketSubmitComplaint : ClientPacket
{
    public SupportTicketSubmitComplaint(WorldPacket packet) : base(packet) { }

    public override void Read()
    {
        if (ModernVersion.Build == ClientVersionBuild.V3_4_3_54261)
        {
            ReadV343(_worldPacket);
            return;
        }

        Header.Read(_worldPacket, withProgram: false);
        TargetCharacterGuid = _worldPacket.ReadPackedGuid128();

        ChatLog.Read(_worldPacket);

        ComplaintType = (GmTicketComplaintType)_worldPacket.ReadBits<uint>(5);

        var noteLength = _worldPacket.ReadBits<uint>(10);

        var hasMailInfo = _worldPacket.ReadBit();
        var unk2 = _worldPacket.ReadBit();
        var unk3 = _worldPacket.ReadBit();
        var hasGuildInfo = _worldPacket.ReadBit();
        var unk5 = _worldPacket.ReadBit();
        var unk6 = _worldPacket.ReadBit();
        var hasClubMessage = _worldPacket.ReadBit();
        var unk8 = _worldPacket.ReadBit();
        var unk9 = _worldPacket.ReadBit();

        _worldPacket.ResetBitPos();

        if (hasClubMessage)
        {
            bool isUsingVoice = _worldPacket.ReadBit();
            _worldPacket.ResetBitPos();
        }

        var unkAlwaysZero = _worldPacket.ReadUInt32();
        if  (unkAlwaysZero != 0)
        {
            Log.Print(LogType.Error, "You reported something that we do not handle (?)");
            Log.Print(LogType.Error, "Please create a new issue on GitHub and tell us what you did");
            return;
        }

        if (hasMailInfo)
        {
            SelectedMailInfo = new MailInfo();
            SelectedMailInfo.Read(_worldPacket);
        }

        TextNote = _worldPacket.ReadString(noteLength);
    }

    /// <summary>
    /// V3_4_3 layout, byte-verified against six native captures covering every report category the
    /// client offers. It differs from the older builds in three ways: three int32 category fields
    /// sit between the target GUID and the chat log, the note is read *after* the Horus chat log
    /// rather than after the optional blocks, and the header carries a trailing Program FourCC.
    /// </summary>
    internal void ReadV343(WorldPacket _worldPacket)
    {
        Header.Read(_worldPacket, withProgram: true);
        TargetCharacterGuid = _worldPacket.ReadPackedGuid128();

        ReportType = (ReportType)_worldPacket.ReadInt32();
        MajorCategory = (ReportMajorCategory)_worldPacket.ReadInt32();
        MinorCategoryFlags = (ReportMinorCategory)_worldPacket.ReadInt32();

        ChatLog.Read(_worldPacket);

        var noteLength = _worldPacket.ReadBits<uint>(10);

        var hasMailInfo = _worldPacket.ReadBit();
        var hasCalendarInfo = _worldPacket.ReadBit();
        var hasPetInfo = _worldPacket.ReadBit();
        var hasGuildInfo = _worldPacket.ReadBit();
        var hasLFGListSearchResult = _worldPacket.ReadBit();
        var hasLFGListApplicant = _worldPacket.ReadBit();
        var hasClubMessage = _worldPacket.ReadBit();
        var hasClubFinderResult = _worldPacket.ReadBit();
        var hasUnk910 = _worldPacket.ReadBit();

        _worldPacket.ResetBitPos();

        if (hasClubMessage)
        {
            _worldPacket.ReadBit(); // IsPlayerUsingVoice
            _worldPacket.ResetBitPos();
        }

        // HorusChatLog: a line count followed by that many lines. Always empty in the captures,
        // and the proxy has nothing to do with community chat, so the lines are not decoded -
        // bail out rather than read past a structure we cannot forward anyway.
        var horusLineCount = _worldPacket.ReadUInt32();
        if (horusLineCount != 0)
        {
            Log.Print(LogType.Error, "Support ticket carried a community chat log, which is not translated");
            return;
        }

        TextNote = _worldPacket.ReadString(noteLength);

        if (hasMailInfo)
        {
            SelectedMailInfo = new MailInfo();
            SelectedMailInfo.Read(_worldPacket);
        }
    }

    public HeaderInfo Header = new();
    public WowGuid128 TargetCharacterGuid;
    public ReportType ReportType;
    public ReportMajorCategory MajorCategory;
    public ReportMinorCategory MinorCategoryFlags;
    public ChatLogInfo ChatLog = new();
    public MailInfo? SelectedMailInfo = null;
    public GmTicketComplaintType ComplaintType;
    public string TextNote = string.Empty;
    
    public class HeaderInfo
    {
        public void Read(WorldPacket worldPacket, bool withProgram)
        {
            SelfPlayerMapId = worldPacket.ReadUInt32();
            SelfPlayerPos = worldPacket.ReadVector3();
            SelfPlayerOrientation = worldPacket.ReadFloat();

            // V3_4_3 appends a program FourCC - observed as 0x00576F57 ("WoW") in every native
            // capture. WowPacketParser omits it entirely, which is why its parse of this packet
            // desyncs on this build.
            if (withProgram)
                Program = worldPacket.ReadUInt32();
        }

        public uint SelfPlayerMapId;
        public Vector3 SelfPlayerPos;
        public float SelfPlayerOrientation;
        public uint Program;
    }

    public class ChatLogInfo
    {
        public void Read(WorldPacket worldPacket)
        {
            var chatLogLineCount = worldPacket.ReadUInt32();

            var hasReportedLineIndex = worldPacket.ReadBool();

            for (var i = 0; i < chatLogLineCount; i++)
            {
                var time = worldPacket.ReadTime64(); 
                var textLength = worldPacket.ReadBits<uint>(12);
                worldPacket.ResetBitPos();
                var text = worldPacket.ReadString(textLength);
                ChatLines.Add(new ChatLine
                {
                    Time = time,
                    Text = text,
                });
            }

            if (hasReportedLineIndex)
                ReportedLineIdx = worldPacket.ReadUInt32();
        }

        public List<ChatLine> ChatLines = new();
        public uint? ReportedLineIdx;

        public class ChatLine
        {
            public DateTime Time;
            public string Text = string.Empty;
        }
    }

    public class MailInfo
    {
        public void Read(WorldPacket worldPacket)
        {
            MailId = worldPacket.ReadUInt32();
            
            var textBodyLength = worldPacket.ReadBits<uint>(13);
            var subjectLength = worldPacket.ReadBits<uint>(9);
            worldPacket.ResetBitPos();

            MailTextBody = worldPacket.ReadString(textBodyLength);
            MailSubject = worldPacket.ReadString(subjectLength);
        }
        
        public uint MailId;
        public string MailTextBody = string.Empty;
        public string MailSubject = string.Empty;
    }
}

/// <summary>
/// Reply to <c>CMSG_GM_TICKET_GET_SYSTEM_STATUS</c>. Native writes a single int32; a capture of
/// Wrathion answering a 3.4.3 client shows exactly 4 bytes with Status = 1 (enabled).
/// </summary>
class GMTicketSystemStatus : ServerPacket
{
    public GMTicketSystemStatus() : base(Opcode.SMSG_GM_TICKET_SYSTEM_STATUS) { }

    public override void Write()
    {
        _worldPacket.WriteInt32(Status);
    }

    public int Status;
}

/// <summary>
/// Reply to <c>CMSG_GM_TICKET_GET_CASE_STATUS</c>. Legacy has no concept of GM cases, and native
/// 3.4.3 does not implement them either - its handler is a stub that returns an empty list - so
/// an empty list is the faithful answer. Native's capture is 4 bytes, CasesCount = 0.
/// </summary>
class GMTicketCaseStatus : ServerPacket
{
    public GMTicketCaseStatus() : base(Opcode.SMSG_GM_TICKET_CASE_STATUS) { }

    public override void Write()
    {
        _worldPacket.WriteInt32(0); // CasesCount - always empty, see above
        _worldPacket.FlushBits();
    }
}
