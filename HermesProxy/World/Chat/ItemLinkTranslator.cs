using System;
using System.Text;

namespace HermesProxy.World.Chat;

// Rewrites item hyperlinks in chat text as it crosses the proxy boundary.
//
// Direction matters, and the two directions are independent: an outbound link only has to
// satisfy the legacy server's validator, and an inbound one only has to satisfy the modern
// client's parser. Neither side ever sees the other's format, so no single wire form has to
// serve both and no backend-flavour detection is required.
//
// Only eras whose legacy format has been verified against a real parser are registered.
// An unregistered era falls through untouched, which is the behaviour that ships today and
// is known to work against mangos-family servers.

public static class ItemLinkTranslator
{
    private const string LinkPrefix = "|Hitem:";
    private const string LinkSuffix = "|h";

    /// <summary>
    /// Legacy codec for the connected backend, or null when this era is not supported yet
    /// and links should pass through unchanged.
    /// </summary>
    private static IItemLinkCodec? LegacyCodec => LegacyVersion.ExpansionVersion switch
    {
        // Vanilla 1.12 and TBC 2.4.3 are deliberately absent: their token layouts have not
        // been read from a parser the way WotLK's has, and mangos-family servers accept the
        // modern string unchanged below ChatStrictLinkChecking.Severity 3. Guessing a format
        // here would risk breaking a path that currently works.
        3 => WotLkItemLinkCodec.Instance,
        _ => null,
    };

    /// <summary>
    /// Converts modern item links to the legacy format before forwarding to the game server.
    /// Returns the original instance when there is nothing to rewrite.
    /// </summary>
    public static string ModernToLegacy(string text)
    {
        var legacy = LegacyCodec;
        return legacy == null ? text : Rewrite(text, ModernItemLinkCodec.Instance, legacy);
    }

    /// <summary>
    /// Converts legacy item links to the modern format before forwarding to the game client.
    /// Returns the original instance when there is nothing to rewrite.
    /// </summary>
    public static string LegacyToModern(string text)
    {
        var legacy = LegacyCodec;
        return legacy == null ? text : Rewrite(text, legacy, ModernItemLinkCodec.Instance);
    }

    private static string Rewrite(string text, IItemLinkCodec from, IItemLinkCodec to)
    {
        // Fast path: most chat lines contain no item link at all, and those must not allocate.
        int firstLink = text.IndexOf(LinkPrefix, StringComparison.Ordinal);
        if (firstLink < 0)
            return text;

        StringBuilder? builder = null;
        int copiedUpTo = 0;
        int searchFrom = firstLink;

        while (searchFrom < text.Length)
        {
            int linkStart = text.IndexOf(LinkPrefix, searchFrom, StringComparison.Ordinal);
            if (linkStart < 0)
                break;

            int bodyStart = linkStart + LinkPrefix.Length;
            int bodyEnd = text.IndexOf(LinkSuffix, bodyStart, StringComparison.Ordinal);
            if (bodyEnd < 0)
                break;

            var body = text.AsSpan(bodyStart, bodyEnd - bodyStart);
            if (from.TryParse(body, out var fields))
            {
                builder ??= new StringBuilder(text.Length + 16);
                builder.Append(text, copiedUpTo, bodyStart - copiedUpTo);
                to.Format(fields, builder);
                copiedUpTo = bodyEnd;
            }
            // A body we cannot parse is left exactly as-is. Corrupting a link we do not
            // understand would be worse than forwarding it unchanged — that at least
            // preserves whatever behaviour it has today.

            searchFrom = bodyEnd + LinkSuffix.Length;
        }

        if (builder == null)
            return text;

        builder.Append(text, copiedUpTo, text.Length - copiedUpTo);
        return builder.ToString();
    }
}
