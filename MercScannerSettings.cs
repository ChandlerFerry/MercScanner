using ExileCore.Shared.Helpers;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using Color = System.Drawing.Color;

namespace MercScanner;

public class MercScannerSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);
    public ToggleNode IgnoreLargePanels { get; set; } = new ToggleNode(false);
    public ToggleNode IgnoreFullscreenPanels { get; set; } = new ToggleNode(false);

    /// <summary>Draw every skill on idle mercs (not only match-related ones).</summary>
    public ToggleNode ShowAllSkills { get; set; } = new ToggleNode(true);

    /// <summary>Draw a big MATCH banner when a skill set fully matches.</summary>
    public ToggleNode ShowSetMatchBanner { get; set; } = new ToggleNode(true);

    /// <summary>Alert when valuable items are seen in the merc encounter inventories.</summary>
    public ToggleNode AlertValuableItems { get; set; } = new ToggleNode(true);

    public ColorNode MatchColor { get; set; } = new ColorNode(Color.Lime.ToSharpDx());
    public ColorNode ForbiddenSkillColor { get; set; } = new ColorNode(Color.OrangeRed.ToSharpDx());
    public ColorNode RequiredSkillColor { get; set; } = new ColorNode(Color.DeepSkyBlue.ToSharpDx());
    public ColorNode DefaultSkillColor { get; set; } = new ColorNode(Color.White.ToSharpDx());
    public ColorNode ValuableItemColor { get; set; } = new ColorNode(Color.Gold.ToSharpDx());
    public ColorNode BackgroundColor { get; set; } = new ColorNode(Color.Black.ToSharpDx());
}
