using ExileCore.Shared.Helpers;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using Color = System.Drawing.Color;

namespace MercScanner;

public class MercScannerSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);
    public ToggleNode IgnoreFullscreenPanels { get; set; } = new ToggleNode(false);

    public RangeNode<float> AlertMinChaosValue { get; set; } = new RangeNode<float>(10f, 0f, 100000f);

    public ColorNode MatchColor { get; set; } = new ColorNode(Color.Lime.ToSharpDx());
    public ColorNode ForbiddenSkillColor { get; set; } = new ColorNode(Color.OrangeRed.ToSharpDx());
    public ColorNode RequiredSkillColor { get; set; } = new ColorNode(Color.DeepSkyBlue.ToSharpDx());
    public ColorNode ValuableItemColor { get; set; } = new ColorNode(Color.Gold.ToSharpDx());
}
