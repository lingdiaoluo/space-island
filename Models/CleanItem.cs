using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SpaceIsland.Models;

/// <summary>三级分诊：Safe 直接删、Caution 必须确认、Info 只展示不动手。</summary>
public enum RiskLevel
{
    Safe,
    Caution,
    Info
}

public enum CleanMethod
{
    /// <summary>按 Roots 递归删除文件与空目录。</summary>
    Files,
    /// <summary>按通配符匹配文件（例如 thumbcache_*.db）。</summary>
    Glob,
    /// <summary>清空回收站（走系统 API，不是删目录）。</summary>
    RecycleBin
}

public sealed class CleanItem : INotifyPropertyChanged
{
    private long _size;
    private int _fileCount;
    private bool _exists;
    private bool _selected;
    private bool _scanned;

    public CleanItem(string id, string title, string description, RiskLevel risk,
        CleanMethod method, IEnumerable<string> roots, bool needsAdmin = false)
    {
        Id = id;
        Title = title;
        Description = description;
        Risk = risk;
        Method = method;
        Roots = roots.ToArray();
        NeedsAdmin = needsAdmin;
        _selected = risk == RiskLevel.Safe;
    }

    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public RiskLevel Risk { get; }
    public CleanMethod Method { get; }
    public IReadOnlyList<string> Roots { get; }
    public bool NeedsAdmin { get; }

    public long Size
    {
        get => _size;
        set { if (_size == value) return; _size = value; Raise(); Raise(nameof(SizeText)); Raise(nameof(SizeLabel)); Raise(nameof(HasContent)); }
    }

    public int FileCount
    {
        get => _fileCount;
        set { if (_fileCount == value) return; _fileCount = value; Raise(); Raise(nameof(FileCountText)); }
    }

    public bool Exists
    {
        get => _exists;
        set { if (_exists == value) return; _exists = value; Raise(); Raise(nameof(HasContent)); }
    }

    public bool Scanned
    {
        get => _scanned;
        set
        {
            if (_scanned == value) return;
            _scanned = value;
            Raise();
            Raise(nameof(StatusText));
            Raise(nameof(SizeText));
            Raise(nameof(SizeLabel));
            Raise(nameof(HasContent));
        }
    }

    public bool Selected
    {
        get => _selected;
        set { if (_selected == value) return; _selected = value; Raise(); }
    }

    public bool HasContent => Scanned && Size > 0;

    public bool IsInfo => Risk == RiskLevel.Info;

    public string SizeText => !Scanned ? "扫描中" : Format(Size);

    /// <summary>「仅查看」的项不报体积，避免让人以为能一键清掉。</summary>
    public string SizeLabel => IsInfo ? "仅查看" : SizeText;

    public string FileCountText => IsInfo ? "系统占用" : FileCount > 0 ? $"{FileCount} 个文件" : "暂无文件";

    public string StatusText => !Scanned ? "扫描中…" : Size == 0 ? "无需清理" : Format(Size);

    public string RiskLabel => Risk switch
    {
        RiskLevel.Safe => "可直接清理",
        RiskLevel.Caution => "需要确认",
        _ => "仅展示"
    };

    public string Glyph => Risk switch
    {
        RiskLevel.Safe => "\uE73E",      // 勾
        RiskLevel.Caution => "\uE7BA",   // 警告
        _ => "\uE946"                    // 信息
    };

    public static string Format(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{(long)value} B" : $"{value:0.#} {units[unit]}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
