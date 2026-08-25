using System;
using System.Collections.Generic;
using System.IO;
using HermesProxy.World.Server;
using Xunit;

namespace HermesProxy.Tests.World.Server;

public class LastSelectedCharacterTests : IDisposable
{
    private readonly string _accountName = "HermesTests_" + Guid.NewGuid().ToString("N");
    private readonly AccountMetaDataManager _mgr;

    public LastSelectedCharacterTests()
    {
        _mgr = new AccountMetaDataManager(_accountName);
    }

    private string FilePath =>
        Path.GetFullPath(Path.Combine("AccountData", _accountName, "last_character.txt"));

    public void Dispose()
    {
        var accountDir = Path.GetFullPath(Path.Combine("AccountData", _accountName));
        if (Directory.Exists(accountDir))
            Directory.Delete(accountDir, recursive: true);
    }

    [Fact]
    public void SaveThenGet_RoundTripsRealmNameGuidAndTime()
    {
        _mgr.SaveLastSelectedCharacter("AzerothCore", "Brahand", 532, 1787593705);

        var got = _mgr.GetLastSelectedCharacter();

        Assert.True(got.HasValue);
        Assert.Equal("AzerothCore", got.Value.realmName);
        Assert.Equal("Brahand", got.Value.charName);
        Assert.Equal(532ul, got.Value.charLowerGuid);
        Assert.Equal(1787593705L, got.Value.lastLoginUnixSec);
    }

    [Fact]
    public void Get_WithEmptyFile_ReturnsNull()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, "");

        Assert.Null(_mgr.GetLastSelectedCharacter());
    }

    [Fact]
    public void Invalidate_DeletesTheFile()
    {
        _mgr.SaveLastSelectedCharacter("AzerothCore", "Brahand", 532, 1);
        _mgr.InvalidateLastSelectedCharacter();

        Assert.False(File.Exists(FilePath));
        Assert.Null(_mgr.GetLastSelectedCharacter());
    }

    [Fact]
    public void RememberRealm_WithNoSave_PicksFirstCharacter()
    {
        _mgr.RememberRealmFromCharacterList("AzerothCore", new List<(string, ulong)>
        {
            ("Naria", 531),
            ("Brahand", 532),
        });

        var got = _mgr.GetLastSelectedCharacter();
        Assert.True(got.HasValue);
        Assert.Equal("AzerothCore", got.Value.realmName);
        Assert.Equal("Naria", got.Value.charName);
        Assert.Equal(531ul, got.Value.charLowerGuid);
    }

    [Fact]
    public void RememberRealm_KeepsPreviousCharacterIfStillOnTheList()
    {
        _mgr.SaveLastSelectedCharacter("AzerothCore", "Brahand", 532, 1);

        _mgr.RememberRealmFromCharacterList("AzerothCore", new List<(string, ulong)>
        {
            ("Naria", 531),
            ("Brahand", 532),
        });

        var got = _mgr.GetLastSelectedCharacter();
        Assert.True(got.HasValue);
        Assert.Equal("Brahand", got.Value.charName);
        Assert.Equal(532ul, got.Value.charLowerGuid);
    }
}
