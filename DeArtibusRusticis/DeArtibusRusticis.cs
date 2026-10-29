using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;

namespace DeArtibusRusticis;

public class DeArtibusRusticis : ModSystem
{
    private static Harmony? harmony;
    public static ILogger Logger = null!;

    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockClass("BlockHerbRack", typeof(BlockHerbRack));
        api.RegisterBlockEntityClass("HerbRack", typeof(BlockEntityHerbRack));

        Logger = api.Logger;

        if (harmony is null)
        {
            harmony = new Harmony("deartibusrusticis");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
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

    public override void Dispose()
    {
        harmony?.UnpatchAll(harmony.Id);
        harmony = null;
    }
}
