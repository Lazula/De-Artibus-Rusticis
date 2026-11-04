using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace DeArtibusRusticis;

public class DeArtibusRusticis : ModSystem
{
    private static Harmony? harmony;
    public static ILogger Logger = null!;

    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockClass("BlockHerbRack", typeof(BlockHerbRack));
        api.RegisterBlockEntityClass("HerbRack", typeof(BlockEntityHerbRack));
        api.RegisterBlockEntityClass("DAR.ExpandedCheeseCurdsBundle", typeof(BlockEntityExpandedCheeseCurdsBundle));

        // Namespaced for ACA compatibility
        api.RegisterItemClass("DAR.ExpandedCheese", typeof(ItemExpandedCheese));
        api.RegisterBlockClass("DAR.BlockExpandedCheese", typeof(BlockExpandedCheese));
        api.RegisterBlockEntityClass("DAR.ExpandedCheese", typeof(BlockEntityExpandedCheese));
        api.RegisterBlockClass("DAR.BlockExpandedLinen", typeof(BlockExpandedLinen));
        api.RegisterBlockClass("DAR.BlockExpandedCheeseCurdsBundle", typeof(BlockExpandedCheeseCurdsBundle));

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

    public override void AssetsFinalize(ICoreAPI api)
    {
        base.AssetsFinalize(api);
        CookingRecipe.NamingRegistry["mashmeal"] = new DeArtibusRusticisRecipeNames();
        CookingRecipe.NamingRegistry["poachmeal"] = new DeArtibusRusticisRecipeNames();
    }

    public override void Dispose()
    {
        harmony?.UnpatchAll(harmony.Id);
        harmony = null;
    }
}
