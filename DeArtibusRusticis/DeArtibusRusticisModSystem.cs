using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;

namespace DeArtibusRusticis;

public class DeArtibusRusticisModSystem : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockClass("BlockHerbRack", typeof(BlockHerbRack));
        api.RegisterBlockEntityClass("HerbRack", typeof(BlockEntityHerbRack));
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);
        var herbRackTransformConfig = new TransformConfig
        {
            AttributeName = "herbRackTransform",
            Title = "On herb rack"
        };
        GuiDialogTransformEditor.extraTransforms.Add(herbRackTransformConfig);
    }
}
