using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nuterra.BlockInjector
{
    public static class CustomRecipe
    {
        /*public static void RegisterScrappableRecipe(RecipeInput[] Inputs, RecipeOutput[] Outputs, RecipeTable.Recipe.OutputType OutputType = RecipeTable.Recipe.OutputType.Items, string NameOfFabricator = "gsofab", float BuildTime = 1f)
        {
            new GameObject().AddComponent<RecipeTimer>().CallRecipeRegister(new CustomRecipeStruct(Inputs, Outputs, OutputType, NameOfFabricator, BuildTime));
            new GameObject().AddComponent<RecipeTimer>().CallRecipeRegister(new CustomRecipeStruct(Inputs, Outputs, OutputType, NameOfFabricator + "_reverse", BuildTime));
        }*/

        internal static Dictionary<FactionSubTypes, string> FabNameDict = new Dictionary<FactionSubTypes, string>()
        {
            { FactionSubTypes.GSO, "gsofab" },
            { FactionSubTypes.GC, "gcfab" },
            { FactionSubTypes.VEN, "venfab" },
            { FactionSubTypes.HE, "hefab" },
            { FactionSubTypes.BF, "bffab" }
        };

        public static string FabricatorFromFactionType(FactionSubTypes faction)
        {
            if (FabNameDict.TryGetValue(faction, out string result))
                return result;
            return null;
        }

        public static void RegisterRecipe(ChunkTypes[] Inputs, int OutputID, string NameOfFabricator = "gsofab")
        {
            RegisterRecipe(RecipeInput.FromChunkTypesArray(Inputs), new RecipeOutput[] { new RecipeOutput(OutputID) }, NameOfFabricator: NameOfFabricator);
        }

        public static void RegisterRecipe(RecipeInput[] Inputs, int OutputID, string NameOfFabricator = "gsofab")
        {
            RegisterRecipe(Inputs, new RecipeOutput[] { new RecipeOutput(OutputID) }, NameOfFabricator: NameOfFabricator);
        }

        public static void RegisterRecipe(RecipeInput[] Inputs, RecipeOutput[] Outputs, RecipeTable.Recipe.OutputType OutputType = RecipeTable.Recipe.OutputType.Items, string NameOfFabricator = "gsofab", float BuildTime = 1f)
        {
            new GameObject().AddComponent<RecipeTimer>().CallRecipeRegister(new CustomRecipeStruct(Inputs, Outputs, OutputType, NameOfFabricator, BuildTime));
        }

        internal static void RegisterRecipe(CustomRecipeStruct customRecipe)
        {
            List<RecipeTable.Recipe> recipeList = null;
            var recipeTable = Singleton.Manager<RecipeManager>.inst.recipeTable;
            foreach (RecipeTable.RecipeList list in recipeTable.m_RecipeLists)
            {
                if (list.m_Name == customRecipe.NameOfFabricator)
                {
                    recipeList = list.m_Recipes;
                    break;
                }
            }

            if (recipeList == null)
            {
                Console.WriteLine("Creating new recipe table '" + customRecipe.NameOfFabricator + "'...");
                recipeList = new List<RecipeTable.Recipe>();
                var NewRecipeItem = new RecipeTable.RecipeList()
                {
                    m_Name = customRecipe.NameOfFabricator,
                    m_Recipes = recipeList,
                };
                recipeTable.m_RecipeLists.Add(NewRecipeItem);
            }

            var InputItems = new RecipeTable.Recipe.ItemSpec[customRecipe.Inputs.Length];
            for (int i = 0; i < customRecipe.Inputs.Length; i++)
            {
                var input = customRecipe.Inputs[i];
                if (input.IsValid)
                    InputItems[i] = input.ItemSpec();
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
            recipeList.Add(Recipe);
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
                Invoke("FinishRecipe",.5f);
            }

            private void FinishRecipe()
            {
                try
                {
                    RegisterRecipe(customRecipe);
                }
                catch(Exception E)
                {
                    Console.WriteLine("Exception trying to register recipe! " + E.Message + "\n" + E.StackTrace);
                }
                UnityEngine.GameObject.Destroy(this.gameObject);
            }
        }

        public struct RecipeInput
        {
            public static RecipeInput[] FromChunkTypesArray(ChunkTypes[] source)
            {
                var buildup = new Dictionary<int, int>(); // Group identical chunks
                for (int i = 0; i < source.Length; i++)
                {
                    int item = (int)source[i];
                    if (buildup.ContainsKey(item))
                        buildup[item]++;
                    else
                        buildup.Add(item, 1);
                }

                RecipeInput[] result = new RecipeInput[source.Length]; // Convert to RecipeInput array
                int inc = 0;
                foreach (var pair in buildup)
                {
                    result[inc++] = new RecipeInput(pair.Key, pair.Value);
                }
                return result;
            }

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
            public bool IsValid => Count != 0 && Type != ObjectTypes.Null;
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
