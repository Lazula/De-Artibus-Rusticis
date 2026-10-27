using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace DeArtibusRusticis;

public class DeArtibusRusticisRecipeNames : VanillaCookingRecipeNames
{
    protected override string GetNameForMergedIngredients(IWorldAccessor worldForResolve, CookingRecipe recipe, Vintagestory.API.Datastructures.OrderedDictionary<ItemStack, int> quantitiesByStack)
    {
        string recipeCode = recipe.Code!;

        switch (recipeCode)
        {
            case "mashmeal":
                {
                    List<string> MainIngredientNames = [];
                    string MainIngredientFormat = "{0}";
                    CookingRecipeIngredient? ingred = null;
                    int max = 0;

                    foreach ((ItemStack stack, int quantity) in quantitiesByStack)
                    {
                        ingred = recipe.GetIngrendientFor(stack);
                        if (ingred?.Code == "vegetable")
                        {
                            MainIngredientNames.Add(ingredientName(stack));

                            max += quantity;
                        }
                    }

                    if (MainIngredientNames.Count == 2) MainIngredientFormat = "multi-main-ingredients-format";

                    string MealFormat = getMaxMealFormat("meal", recipeCode, max);
                    MealFormat = Lang.Get(MealFormat, getMainIngredientsString(MainIngredientNames, MainIngredientFormat));

                    return MealFormat.Trim().UcFirst();
                }
            case "poachmeal":
                {
                    List<string> ExtraIngredientNames = [];
                    CookingRecipeIngredient? ingred = null;
                    int typesOfGreen = quantitiesByStack.Count(val => recipe.GetIngrendientFor(val.Key)?.Code == "poachedlettuce");
                    int max = 0;

                    foreach ((ItemStack stack, int quantity) in quantitiesByStack)
                    {
                        string itemName;

                        ingred = recipe.GetIngrendientFor(stack);

                        if (ingred?.Code is "poachedlettuce" or "alcohol") continue;

                        itemName = ingredientName(stack, EnumIngredientNameType.InsturmentalCase);
                        if (!ExtraIngredientNames.Contains(itemName)) ExtraIngredientNames.Add(itemName);
                    }

                    string ExtraIngredientsFormat = "meal-adds-generic";

                    string MealFormat = getMaxMealFormat("meal", recipeCode, max);
                    MealFormat = Lang.Get(MealFormat, getMealAddsString(ExtraIngredientsFormat, ExtraIngredientNames));

                    return MealFormat.Trim().UcFirst();
                }
        }

        return Lang.Get("unknown");
    }
}
