using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using System.Reflection;

namespace Nuterra.BlockInjector
{
    public static class GameObjectJSON
    {
        private static Dictionary<Type, Dictionary<string, UnityEngine.Object>> LoadedResources = new Dictionary<Type, Dictionary<string, UnityEngine.Object>>();

        public static Material MaterialFromShader(string ShaderName = "Standard")
        {
            var shader = Shader.Find(ShaderName);
            return new Material(shader);
        }

        public static T GetObjectFromUserResources<T>(string targetName) where T : UnityEngine.Object
        {
            return GetObjectFromUserResources<T>(typeof(T), targetName);
        }
        public static T GetObjectFromUserResources<T>(Type t, string targetName) where T : UnityEngine.Object
        {
            if (LoadedResources.ContainsKey(t) && LoadedResources[t].ContainsKey(targetName))
            {
                return LoadedResources[t][targetName] as T;
            }
            return null;
        }

        internal static Type ManSpawnT = typeof(ManSpawn);

        public static GameObject GetBlockFromAssetTable(string NameOfBlock)
        {
            var allblocks = ((BlockTable)ManSpawnT.GetField("m_BlockTable", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ManSpawn.inst)).m_Blocks;
            foreach (var block in allblocks)
            {
                if (block.name.StartsWith(NameOfBlock))
                    return block;
            }
            string NewSearch = NameOfBlock.Replace("(", "").Replace(")", "").Replace("_", "").Replace(" ", "").ToLower();
            foreach (var block in allblocks)
            {
                if (block.name.Replace("_", "").Replace(" ", "").ToLower().StartsWith(NewSearch))
                {
                    return block;
                }
            }
            return null;
        }

        public static List<T> GetObjectsFromGameResources<T>(Type t, string startOfTargetName) where T : UnityEngine.Object
        {
            List<T> searchresult = new List<T>();
            T[] search = Resources.FindObjectsOfTypeAll(t) as T[];
            for (int i = 0; i < search.Length; i++)
            {
                if (search[i].name.StartsWith(startOfTargetName))
                {
                    searchresult.Add(search[i]);
                }
            }
            return searchresult;
        }
        public static List<T> GetObjectsFromGameResources<T>(string startOfTargetName) where T : UnityEngine.Object
        {
            return GetObjectsFromGameResources<T>(typeof(T), startOfTargetName);
        }

        public static T GetObjectFromGameResources<T>(Type t, string targetName, bool Log = false) where T : UnityEngine.Object
        {
            T searchresult = null;
            T[] search = Resources.FindObjectsOfTypeAll(t) as T[];
            string failedsearch = "";
            for (int i = 0; i < search.Length; i++)
            {
                if (search[i].name == targetName)
                {
                    searchresult = search[i];
                    break;
                }
                failedsearch += search[i].name + "; ";
            }
            if (searchresult == null)
            {
                for (int i = 0; i < search.Length; i++)
                {
                    if (search[i].name.StartsWith(targetName))
                    {
                        searchresult = search[i];
                        break;
                    }
                    failedsearch += search[i].name + "; ";
                }
                if (searchresult == null && Log)
                {
                    Console.WriteLine("Could not find resource: " + targetName + "\n\nThis is what exists for that type:\n" + (failedsearch == "" ? "Nothing. Nothing exists for that type." : failedsearch));
                }
            }
            return searchresult;
        }
        public static T GetObjectFromGameResources<T>(string targetName, bool Log = false) where T : UnityEngine.Object
        {
            return GetObjectFromGameResources<T>(typeof(T), targetName, Log);
        }

        public static Texture2D ImageFromFile(byte[] DATA)
        {
            Texture2D texture;
            texture = new Texture2D(2, 2);
            texture.LoadImage(DATA);
            return texture;
        }

        public static Texture2D CropImage(Texture2D source, Rect AreaNormalized)
        {
            int startX = Mathf.RoundToInt(source.width * AreaNormalized.x),
                startY = Mathf.RoundToInt(source.height * AreaNormalized.y),
                extentX = Mathf.RoundToInt(source.width * AreaNormalized.width),
                extentY = Mathf.RoundToInt(source.height * AreaNormalized.height);
            int Sizeof = extentX * extentY;
            Texture2D Result = new Texture2D(extentX, extentY);
            Result.SetPixels(source.GetPixels(startX, startY, extentX, extentY));
            Result.Apply();
            return Result;
        }

        public static Texture2D ImageFromFile(string localPath)
        {
            string _localPath = System.IO.Path.Combine(Assembly.GetCallingAssembly().Location, "../" + localPath);
            byte[] data;
            if (System.IO.File.Exists(_localPath))
                data = System.IO.File.ReadAllBytes(_localPath);
            else if (System.IO.File.Exists(localPath))
                data = System.IO.File.ReadAllBytes(localPath);
            else throw new NullReferenceException("The file specified could not be found in " + localPath + " or " + _localPath);
            return ImageFromFile(data);
        }

        public static Sprite SpriteFromImage(Texture2D texture, float Scale = 1f)
        {
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(texture.width * 0.5f, texture.height * 0.5f), Mathf.Max(texture.width, texture.height) * Scale);
        }

        [Obsolete("Please use MeshFromData")]
        public static Mesh MeshFromFile(string FILEDATA, string name)
        {
            return MeshFromData(FILEDATA);
        }

        public static Mesh MeshFromData(string FILEDATA)
        {
            return FastObjImporter.Instance.ImportFileFromData(FILEDATA);
        }

        public static Mesh MeshFromFile(string localPath)
        {
            string _localPath = System.IO.Path.Combine(Assembly.GetCallingAssembly().Location, "../" + localPath);
            if (!System.IO.File.Exists(_localPath))
            {
                if (System.IO.File.Exists(localPath))
                {
                    _localPath = localPath;
                }
                else throw new NullReferenceException("The file specified could not be found in " + localPath + " or " + _localPath);
            }
            return FastObjImporter.Instance.ImportFileFromPath(_localPath);
        }

        public static void AddObjectToUserResources<T>(Type type, T Object, string Name) where T : UnityEngine.Object
        {
            if (!LoadedResources.ContainsKey(type))
            {
                LoadedResources.Add(type, new Dictionary<string, UnityEngine.Object>());
            }
            if (LoadedResources[type].ContainsKey(Name))
            {
                LoadedResources[type][Name] = Object;
            }
            else
            {
                LoadedResources[type].Add(Name, Object);
            }
        }

        public static void AddObjectToUserResources<T>(T Object, string Name) where T : UnityEngine.Object
        {
            AddObjectToUserResources<T>(typeof(T), Object, Name);
        }

        public static GameObject CreateGameObject(string json)
        {
           return CreateGameObject(Newtonsoft.Json.Linq.JObject.Parse(json));
        }

        public static GameObject CreateGameObject(JObject json, GameObject GameObjectToPopulate = null, string Spacing = "")
        {
            GameObject result;
            if (GameObjectToPopulate == null)
            {
                result = new GameObject("Deserialized Object");
            }
            else
                result = GameObjectToPopulate;
            foreach (JProperty property in json.Properties())
            {
                try
                {
                    if (property.Name.StartsWith("GameObject") || property.Name.StartsWith("UnityEngine.GameObject"))
                    {
                        string name = "Object Child";
                        int GetCustomName = property.Name.IndexOf('|');
                        if (GetCustomName != -1)
                        {
                            name = property.Name.Substring(GetCustomName + 1);
                        }

                        GameObject newGameObject = result.transform.Find(name)?.gameObject;
                        if (!newGameObject) newGameObject = new GameObject(name);
                        newGameObject.transform.parent = result.transform;
                        CreateGameObject(property.Value as JObject, newGameObject, Spacing +  " ");
                    }
                    else
                    {
                        Type componentType = Type.GetType(property.Name);
                        if (componentType == null)
                        {
                            componentType = Type.GetType(property.Name + ", " + typeof(TankBlock).Assembly.FullName);
                            if (componentType == null)
                            {
                                componentType = Type.GetType(property.Name + ", " + typeof(GameObject).Assembly.FullName);
                                if (componentType == null)
                                {
                                    Console.WriteLine(property.Name + " is not a known type! If you are using a Unity type, you might need to prefix the class with \"UnityEngine.\", for example, \"UnityEngine.LineRenderer\". If it is not from Unity or the game itself, it needs the class's Assembly's `FullName`, for example: \"" + typeof(TankBlock).Assembly.FullName + "\", in which it'd be used as \"" + typeof(ModuleDrill).AssemblyQualifiedName+"\"");
                                    continue;
                                }
                            }
                        }
                        object component = result.GetComponent(componentType);
                        if (component as Component == null)
                        {
                            component = result.AddComponent(componentType);
                            if (component == null)
                            {
                                Console.WriteLine(property.Name + " is a null component, but does not throw an exception...");
                                continue;
                            }
                            Console.WriteLine(Spacing+"Created " + property.Name);
                        }
                        ApplyValues(component, componentType, property.Value as JObject, Spacing);
                        Console.WriteLine(Spacing+"Set values of " + property.Name);
                    }
                }
                catch (Exception E)
                {
                    Console.WriteLine(E.Message + "\n" + E.StackTrace);
                }
            }

            return result;
        }

        public static object ApplyValues(object instance, Type instanceType, JObject json, string Spacing)
        {
            Console.WriteLine(Spacing+"Going down");
            object _instance = instance;
            BindingFlags bind = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (JProperty property in json.Properties())
            {
                try
                {
                    Console.WriteLine(Spacing+" "+property.Name);
                    FieldInfo tField = instanceType.GetField(property.Name, bind);
                    PropertyInfo tProp = instanceType.GetProperty(property.Name, bind);
                    bool UseField = tProp == null;
                    if (UseField)
                    {
                        if (tField == null)
                        {
                            Console.WriteLine(Spacing + " skipping...");
                            continue;
                        }
                    }
                    if (property.Value is JObject)
                    {
                        if (UseField)
                        {
                            object original = tField.GetValue(instance);
                            object rewrite = ApplyValues(original, tField.FieldType, property.Value as JObject, Spacing + " ");
                            try { tField.SetValue(_instance, rewrite); } catch { }
                        }
                        else
                        {
                            object original = tProp.GetValue(instance, null);
                            object rewrite = ApplyValues(original, tProp.PropertyType, property.Value as JObject, Spacing + " ");
                            if (tProp.CanWrite)
                                try { tProp.SetValue(_instance, rewrite, null); } catch { }
                        }
                    }
                    if (property.Value is JValue || property.Value is JArray)
                    {
                        try
                        {
                            Console.WriteLine(Spacing+" Setting value");
                            if (UseField)
                            {
                                tField.SetValue(_instance, property.Value.ToObject(tField.FieldType));
                            }
                            else
                            {
                                tProp.SetValue(_instance, property.Value.ToObject(tProp.PropertyType), null);
                            }
                        }
                        catch
                        {
                            string cache = property.Value.ToObject<string>();
                            string targetName;
                            Type type;
                            if (cache.Contains('|'))
                            {
                                string[] cachepart = cache.Split('|');
                                type = Type.GetType(cachepart[0]);
                                targetName = cachepart[1];
                            }
                            else
                            {
                                type = UseField ? tField.FieldType : tProp.PropertyType;
                                targetName = cache;
                            }
                            UnityEngine.Object searchresult = null;
                            if (LoadedResources.ContainsKey(type) && LoadedResources[type].ContainsKey(targetName))
                            {
                                searchresult = LoadedResources[type][targetName];
                                Console.WriteLine(Spacing+" Setting value from user resource reference");
                            }
                            else
                            {
                                UnityEngine.Object[] search = Resources.FindObjectsOfTypeAll(type);
                                string failedsearch = "";
                                for (int i = 0; i < search.Length; i++)
                                {
                                    if (search[i].name == targetName)
                                    {
                                        searchresult = search[i];
                                        Console.WriteLine(Spacing+" Setting value from existing resource reference");
                                        break;
                                    }
                                    failedsearch += "(" + search[i].name + ") ";
                                }
                                if (searchresult == null)
                                {
                                    Console.WriteLine("Could not find resource: " + targetName + "\n\nThis is what exists for that type:\n" + (failedsearch == "" ? "Nothing. Nothing exists for that type." : failedsearch));
                                }
                            }
                            if (UseField)
                            {
                                tField.SetValue(_instance, searchresult);
                            }
                            else
                            {
                                tProp.SetValue(_instance, searchresult, null);
                            }
                        }
                    }
                }
                catch (Exception E) { Console.WriteLine(E.Message+"\n"+E.StackTrace); }
            }
            Console.WriteLine(Spacing+"Going up");
            return _instance;
        }
    }
}
