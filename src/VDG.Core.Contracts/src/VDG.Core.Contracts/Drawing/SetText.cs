namespace VDG.Core.Drawing;

/// <summary>Command to set (or update) text on a node/edge.</summary>
public sealed class SetText : DrawCommand
{
    public string TargetId { get; }
    public string Text { get; }

    public SetText(string targetId, string text)
    {
        TargetId = targetId ?? throw new ArgumentNullException(nameof(targetId));
        Text = text ?? string.Empty;
    }
}
