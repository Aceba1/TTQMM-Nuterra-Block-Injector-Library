using System.Collections.Generic;
using UnityEngine;

namespace Nuterra.BlockInjector
{
    static class CustomRecipe
    {
        public static void RegisterRecipe(RecipeInput[] Inputs, RecipeOutput[] Outputs, RecipeTable.Recipe.OutputType OutputType = RecipeTable.Recipe.OutputType.Items, string NameOfFabricator = "gsofab", float BuildTime = 1f)
        {
            new GameObject().AddComponent<RecipeTimer>().CallRecipeRegister(new CustomRecipeStruct(Inputs, Outputs, OutputType, NameOfFabricator, BuildTime));
        }

        internal static void RegisterRecipe(CustomRecipeStruct customRecipe)
        {
            List<RecipeTable.Recipe> GSORecipes = null;
            foreach (var list in Singleton.Manager<RecipeManager>.inst.recipeTable.m_RecipeLists)
            {
                if (list.m_Name == customRecipe.NameOfFabricator)
                {
                    GSORecipes = list.m_Recipes;
                    break;
                }
            }

            if (GSORecipes == null)
            {
                throw new System.Exception("Could not find recipe table of " + customRecipe.NameOfFabricator);
            }

            var InputItems = new RecipeTable.Recipe.ItemSpec[customRecipe.Inputs.Length];
            for (int i = 0; i < customRecipe.Inputs.Length; i++)
            {
                InputItems[i] = customRecipe.Inputs[i].ItemSpec();
            }

            var OutputItems = new RecipeTable.Recipe.ItemSpec[customRecipe.Outputs.Length];
            for (int j = 0; j < customRecipe.Outputs.Length; j++)
            {
                OutputItems[j] = customRecipe.Outputs[j].ItemSpec();
            }

            var Recipe = new RecipeTable.Recipe()
            {
                m_BuildTimeSeconds = customRecipe.BuildTime,
                m_InputItems = InputItems,
                m_OutputType = customRecipe.OutputType,
                m_OutputItems = OutputItems
            };
            GSORecipes.Add(Recipe);
        }
        internal struct CustomRecipeStruct
        {
            public CustomRecipeStruct(RecipeInput[] Inputs, RecipeOutput[] Outputs, RecipeTable.Recipe.OutputType OutputType, string NameOfFabricator, float BuildTime)
            {
                this.Inputs = Inputs;
                this.Outputs = Outputs;
                this.OutputType = OutputType;
                this.NameOfFabricator = NameOfFabricator;
                this.BuildTime = BuildTime;
            }
            public RecipeInput[] Inputs;
            public RecipeOutput[] Outputs;
            public RecipeTable.Recipe.OutputType OutputType;
            public string NameOfFabricator;
            public float BuildTime;
        }

        internal class RecipeTimer : MonoBehaviour
        {
            public CustomRecipeStruct customRecipe;
            public void CallRecipeRegister(CustomRecipeStruct CustomBlock)
            {
                customRecipe = CustomBlock;
                BlockLoader.DelayAfterSingleton(FinishRecipe);
            }

            private void FinishRecipe()
            {
                RegisterRecipe(customRecipe);
                UnityEngine.GameObject.Destroy(this.gameObject);
            }
        }

        public struct RecipeInput
        {
            public RecipeInput(int ID, int Quantity = 1, ObjectTypes Type = ObjectTypes.Chunk)
            {
                this.ID = ID;
                this.Type = Type;
                Count = Quantity;
            }

            public RecipeTable.Recipe.ItemSpec ItemSpec()
            {
                return new RecipeTable.Recipe.ItemSpec(new ItemTypeInfo(Type, ID), Count);
            }

            int ID;
            ObjectTypes Type;
            int Count;
        }

        public struct RecipeOutput
        {
            public RecipeOutput(int ID, int Quantity = 1, ObjectTypes Type = ObjectTypes.Block)
            {
                this.ID = ID;
                this.Type = Type;
                Count = Quantity;
            }

            public RecipeTable.Recipe.ItemSpec ItemSpec()
            {
                return new RecipeTable.Recipe.ItemSpec(new ItemTypeInfo(Type, ID), Count);
            }

            int ID;
            ObjectTypes Type;
            int Count;
        }
    }
}
