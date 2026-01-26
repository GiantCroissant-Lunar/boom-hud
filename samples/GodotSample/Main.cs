using Godot;

namespace GodotSample;

public partial class Main : Control
{
    public override void _Ready()
    {
        // 1. Create the ViewModel
        var viewModel = new GameHudViewModel();

        // 2. Create the Generated View
        var view = new GameHudView();
        
        // 3. Set layout to fill screen
        view.SetAnchorsPreset(LayoutPreset.FullRect);
        
        // 4. Bind them
        view.SetViewModel(viewModel);

        // 5. Add to scene
        AddChild(view);
    }
}
