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

        const string m_tab = "  ";

        static Type t_mat = typeof(Shader);
        static Type t_uobj = typeof(UnityEngine.Object);
        public static Material MaterialFromShader(string ShaderName = "StandardTankBlock")
        {
            var shader = GetObjectFromGameResources<Shader>(t_mat, ShaderName, true);
            return new Material(shader);
        }

        public static Material SetTexturesToMaterial(bool MakeCopy, Material material, Texture2D Alpha = null, Texture2D MetallicGloss = null, Texture2D Emission = null)
        {
            bool flag1 = Alpha != null, flag2 = MetallicGloss != null, flag3 = Emission!=null;
            if (MakeCopy && (flag1 || flag2 || flag3))
            {
                material = new Material(material);
            }
            if (flag1)
            {
                material.SetTexture("_MainTex", Alpha);
            }
            if (flag2)
            {
                material.SetTexture("_MetallicGlossMap", MetallicGloss);
            }
            if (flag3)
            {
                material.SetTexture("_EmissionMap", Emission);
            }
            return material;
        }

        public static T GetObjectFromUserResources<T>(string targetName) where T : UnityEngine.Object
        {
            return GetObjectFromUserResources<T>(typeof(T), targetName);
        }
        public static T GetObjectFromUserResources<T>(Type t, string targetName) where T : UnityEngine.Object
        {
            if (LoadedResources.TryGetValue(t, out var bucket) && bucket.TryGetValue(targetName, out var item))
            {
                return item as T;
            }
            return null;
        }
        public static bool UserResourcesContains(Type t, string targetName) => LoadedResources.ContainsKey(t) && LoadedResources[t].ContainsKey(targetName);

        internal static Type ManSpawnT = typeof(ManSpawn);
        static FieldInfo m_BlockTable = ManSpawnT.GetField("m_BlockTable", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

        public static GameObject GetBlockFromAssetTable(string NameOfBlock)
        {
            var allblocks = ((BlockTable)m_BlockTable.GetValue(ManSpawn.inst)).m_Blocks;
            foreach (var block in allblocks)
            {
                if (block.name.StartsWith(NameOfBlock))
                    return block;
            }
            string NewSearch = NameOfBlock.Replace("(", "").Replace(")", "").Replace("_", "").Replace(m_tab, "").ToLower();
            foreach (var block in allblocks)
            {
                if (block.name.Replace("_", "").Replace(m_tab, "").ToLower().StartsWith(NewSearch))
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

        public static void DumpAnimation(ModuleAnimator animator)
        {
            Console.WriteLine("Clip names for animator " + animator.name + ":");
            for (int i = 0; i < animator.Animator.layerCount; i++)
            {
                Console.WriteLine(">Layer " + i.ToString() + "("+ animator.Animator.GetLayerName(i) + "):");
                foreach (var clip in animator.Animator.GetCurrentAnimatorClipInfo(i))
                {
                    Console.WriteLine(" >" + clip.clip.name);
                }
            } 
        }

        public struct AnimationCurveStruct
        {
            internal static AnimationCurveStruct[] ConvertToStructArray(DirectoryBlockLoader.BlockBuilder.SubObj.AnimInfo.Curve[] curves)
            {
                var result = new AnimationCurveStruct[curves.Length];
                for (int i = 0; i < curves.Length; i++)
                {
                    result[i] = new AnimationCurveStruct(curves[i].ComponentName, curves[i].PropertyName, curves[i].ToAnimationCurve());
                }
                return result;
            }

            public AnimationCurveStruct(string Type, string PropertyName, AnimationCurve Curve)
            {
                this.Type = GameObjectJSON.GetType(Type);
                this.PropertyName = PropertyName;
                this.Curve = Curve;
            }
            public Type Type;
            public string PropertyName;
            public AnimationCurve Curve;
        }

        public static void ModifyAnimation(Animator animator, string clipName, string path, AnimationCurveStruct[] curves)
        {
            for(int i = 0; i < animator.layerCount; i++)
            {
                var clips = animator.GetCurrentAnimatorClipInfo(i);
                for (int j = 0; j < clips.Length; j++)
                {
                    var clip = clips[j];
                    if (clips[j].clip.name == clipName)
                    {
                        foreach (var curve in curves)
                        {
                            clip.clip.SetCurve(path, curve.Type, curve.PropertyName, curve.Curve);
                            clips[j] = clip;
                        }
                        return;
                    }
                }
            }
        }

        public static object GetValueFromPath(this Component component, string PropertyPath)
        {
            Type currentType = component.GetType();
            object currentObject = component;
            var props = PropertyPath.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            foreach(string pprop in props)
            {
                var tfield = currentType.GetField(pprop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
                if (tfield != null)
                {
                    currentObject = tfield.GetValue(currentObject);
                    currentType = tfield.FieldType;
                }
                else
                {
                    var tproperty = currentType.GetProperty(pprop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
                    if (tproperty != null)
                    {
                        currentObject = tproperty.GetValue(currentObject, null);
                        currentType = tproperty.PropertyType;
                    }
                    else return null;
                }
            }
            return currentObject;
        }

        public static object RecursiveFindWithProperties(this Transform transform, string NameOfProperty)
        {
            try
            {
                int propIndex = NameOfProperty.IndexOf('.');
                if (propIndex == -1)
                {
                    return transform.RecursiveFind(NameOfProperty);
                }
                var result = transform;

                while (true)
                {
                    propIndex = NameOfProperty.IndexOf('.');
                    if (propIndex == -1)
                    {
                        return result.RecursiveFind(NameOfProperty);
                    }
                    int reIndex = NameOfProperty.IndexOf('/', propIndex);
                    int lastIndex = NameOfProperty.LastIndexOf('/', propIndex);
                    if (lastIndex > 0)
                    {
                        string transPath = NameOfProperty.Substring(0, lastIndex);
                        result = result.RecursiveFind(transPath);
                        if (result == null) return null;
                    }

                    string propPath = NameOfProperty.Substring(propIndex, Math.Max(reIndex - propIndex, 0));
                    string propClass = NameOfProperty.Substring(lastIndex + 1, Math.Max(propIndex - lastIndex - 1, 0));

                    var component = result.GetComponent(propClass);
                    var value = component.GetValueFromPath(propPath);


                    if (reIndex == -1) return value;
                    result = (value as Component).transform;
                    NameOfProperty = NameOfProperty.Substring(reIndex);
                }
            }
            catch (Exception E)
            {
                Console.WriteLine("RecursiveFindWithParameters failed! " + E);
                return null;
            }
        }

        public static Transform RecursiveFind(this Transform transform, string NameOfChild, string HierarchyBuildup = "")
        {
            if (NameOfChild == "/") return transform;
            string cName = NameOfChild.Substring(NameOfChild.LastIndexOf('/') + 1);
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                //Console.WriteLine(child.name);
                if (child.name == cName)
                {
                    HierarchyBuildup += "/" + cName;
                    //Console.WriteLine(HierarchyBuildup + "  " + NameOfChild);
                    if (HierarchyBuildup.EndsWith(NameOfChild))
                    {
                        return child;
                    }
                }
            }
            for (int i = 0; i < transform.childCount; i++)
            {
                var c = transform.GetChild(i);
                var child = c.RecursiveFind(NameOfChild, HierarchyBuildup + "/" + c.name);
                if (child != null)
                {
                    return child;
                }
            }
            return null;
        }

        static Dictionary<string, Type> stringtypecache = new Dictionary<string, Type>();
        public static Type GetType(string Name)
        {
            Type type;
            if (stringtypecache.TryGetValue(Name, out type)) return type;
            type = Type.GetType(Name, new Func<AssemblyName, Assembly>(AssemblyResolver), new Func<Assembly, string, bool, Type>(TypeResolver), false, true);
            if (type == null)
            {
                //type = Type.GetType(Name + ", " + typeof(TankBlock).Assembly.FullName);
                //if (type == null)
                //{
                //    type = Type.GetType(Name + ", " + typeof(GameObject).Assembly.FullName);
                //    if (type == null)
                //    {
                Console.WriteLine(Name + " is not a known type! If you are using a Unity type, you might need to prefix the class with \"UnityEngine.\", for example, \"UnityEngine.LineRenderer\". If it is not from Unity or the game itself, it needs the class's Assembly's `FullName`, for example: \"" + typeof(TankBlock).Assembly.FullName + "\", in which it'd be used as \"" + typeof(TankBlock).AssemblyQualifiedName + "\"");
                stringtypecache.Add(Name, null);
                return null;
                //    }
                //}
            }
            stringtypecache.Add(Name, type);
            return type;
        }

        private static Type TypeResolver(Assembly arg1, string arg2, bool arg3)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(arg2, false, arg3);
                if (type != null) return type;
            }
            return null;
        }

        private static Assembly AssemblyResolver(AssemblyName arg)
        {
            return typeof(GameObject).Assembly;
        }

        public static GameObject CreateGameObject(string json)
        {
           return CreateGameObject(Newtonsoft.Json.Linq.JObject.Parse(json));
        }

        public static GameObject CreateGameObject(JObject json, GameObject GameObjectToPopulate = null, string Spacing = "")
        {
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
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
                    bool Duplicate = property.Name.StartsWith("Duplicate");
                    if (Duplicate || property.Name.StartsWith("GameObject") || property.Name.StartsWith("UnityEngine.GameObject"))
                    {
                        string name = "Object Child";
                        int GetCustomName = property.Name.IndexOf('|');
                        if (GetCustomName != -1)
                        {
                            name = property.Name.Substring(GetCustomName + 1);
                        }

                        GameObject newGameObject = result.transform.Find(name)?.gameObject;
                        if (!newGameObject)
                        {
                            if (property.Value == null)
                            {
                                Console.WriteLine(Spacing + "Could not find object " + property.Name + " to delete");
                                continue;
                            }
                            Duplicate = false;
                            newGameObject = new GameObject(name);
                            newGameObject.transform.parent = result.transform;
                        }
                        else
                        {
                            if (property.Value == null)
                            {
                                GameObject.DestroyImmediate(newGameObject);
                               //Console.WriteLine(Spacing + "Deleted " + property.Name);
                                continue;
                            }
                            if (Duplicate)
                            {
                                bool Active = newGameObject.activeInHierarchy;
                                newGameObject = GameObject.Instantiate(newGameObject);
                                string newName = name + "_copy";
                                int count = 1;
                                while (result.transform.Find(newName))
                                {
                                    newName = name + "_copy_" + (++count).ToString(); 
                                }
                                newGameObject.name = newName;
                                newGameObject.transform.parent = result.transform;
                            }
                        }
                        CreateGameObject(property.Value as JObject, newGameObject, Spacing +  m_tab);
                    }
                    else
                    {
                        Type componentType = GetType(property.Name);
                        if (componentType == null) continue;
                        object component = result.GetComponent(componentType);
                        if (property.Value == null)
                        {
                            Component c = component as Component;
                            if (c != null)
                            {
                                Component.DestroyImmediate(c);
                               //Console.WriteLine(Spacing + "Deleted " + property.Name);
                            }
                            else Console.WriteLine(Spacing + "Could not find component " + property.Name + " to delete");
                        }
                        else
                        {
                            if (component as Component == null)
                            {
                                component = result.AddComponent(componentType);
                                if (component == null)
                                {
                                    Console.WriteLine(property.Name + " is a null component, but does not throw an exception...");
                                    continue;
                                }
                               //Console.WriteLine(Spacing + "Created " + property.Name);
                            }
                            SearchTransform = result.transform;
                            ApplyValues(component, componentType, property.Value as JObject, Spacing);
                           //Console.WriteLine(Spacing + "Set values of " + property.Name);
                        }
                    }
                }
                catch (Exception E)
                {
                    Console.WriteLine(E.Message + "\n" + E.StackTrace);
                }
            }
           //Console.WriteLine($"Took {stopwatch.ElapsedMilliseconds} ms to pass {result.name} through JSON parser");
            stopwatch.Stop();

            return result;
        }

        static Transform SearchTransform;
        static object ApplyValues(object instance, Type instanceType, JObject json, string Spacing)
        {
           //Console.WriteLine(Spacing+"Going down");
            object _instance = instance;
            BindingFlags bind = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (JProperty property in json.Properties())
            {
                try
                {
                    string name = property.Name;
                   //Console.WriteLine(Spacing + m_tab + property.Name);
                    int GetCustomName = property.Name.IndexOf('|');
                    bool Wipe = false, Instantiate = false;
                    if (GetCustomName != -1)
                    {
                        Wipe = name.StartsWith("Wipe");
                        Instantiate = name.StartsWith("Instantiate");
                        name = property.Name.Substring(GetCustomName + 1);
                    }
                    FieldInfo tField = instanceType.GetField(name, bind);
                    PropertyInfo tProp = instanceType.GetProperty(name, bind);
                    MethodInfo tMethod = instanceType.GetMethod(name, bind);
                    bool UseField = tProp == null;
                    bool UseMethod = tMethod != null;
                    if (UseField)
                    {
                        if (tField == null)
                        {
                           //Console.WriteLine(Spacing + m_tab + "skipping...");
                            continue;
                        }
                    }
                    if (property.Value is JObject)
                    {
                        if (UseField)
                        {
                            object original, rewrite;
                            if (!Wipe)
                            {
                                original = tField.GetValue(instance);
                                if (Instantiate)
                                {
                                    bool isActive = ((GameObject)typeof(Component).GetProperty("gameObject").GetValue(original, null)).activeInHierarchy;
                                    var nObj = Component.Instantiate(original as Component);
                                    nObj.gameObject.SetActive(isActive);
                                   //Console.WriteLine(Spacing + m_tab + ">Instantiating");
                                    CreateGameObject(property.Value as JObject, nObj.gameObject, Spacing + m_tab + m_tab);
                                    Console.WriteLine(LogAllComponents(nObj.transform, false, Spacing + m_tab));
                                    rewrite = nObj;
                                }
                                else rewrite = ApplyValues(original, tField.FieldType, property.Value as JObject, Spacing + m_tab);
                            }
                            else
                            {
                                original = Activator.CreateInstance(tField.FieldType);
                                rewrite = ApplyValues(original, tField.FieldType, property.Value as JObject, Spacing + m_tab);
                            }
                            try { tField.SetValue(_instance, rewrite); } catch (Exception E) { Console.WriteLine(Spacing + m_tab + "!!!" + E.ToString()); }
                        }
                        else
                        {
                            object original, rewrite;
                            if (!Wipe)
                            {
                                original = tProp.GetValue(instance, null);
                                if (Instantiate)
                                {
                                    bool isActive = ((GameObject)typeof(Component).GetProperty("gameObject").GetValue(original, null)).activeInHierarchy;
                                    var nObj = Component.Instantiate(original as Component);
                                    nObj.gameObject.SetActive(isActive);
                                   //Console.WriteLine(Spacing + m_tab + ">Instantiating");
                                    CreateGameObject(property.Value as JObject, nObj.gameObject, Spacing + m_tab + m_tab);
                                    Console.WriteLine(LogAllComponents(nObj.transform, false, Spacing + m_tab));
                                    rewrite = nObj;
                                }
                                else rewrite = ApplyValues(original, tProp.PropertyType, property.Value as JObject, Spacing + m_tab);
                            }
                            else
                            {
                                original = Activator.CreateInstance(tProp.PropertyType);
                                rewrite = ApplyValues(original, tProp.PropertyType, property.Value as JObject, Spacing + m_tab);
                            }
                            if (tProp.CanWrite)
                                try { tProp.SetValue(_instance, rewrite, null); } catch (Exception E) { Console.WriteLine(Spacing + m_tab + "!!!" + E.ToString()); }
                        }
                    }
                    if (property.Value is JValue || property.Value is JArray )
                    {
                        try
                        {
                            if (UseMethod)
                            {
                               //Console.WriteLine(Spacing + m_tab + ">Calling method (No parameters)");

                                var value = property.Value;
                                tMethod.Invoke(_instance, null);
                            }
                            else
                            {
                                if (UseField)
                                {
                                    tField.SetValue(_instance, property.Value.ToObject(tField.FieldType));
                                }
                                else
                                {
                                    tProp.SetValue(_instance, property.Value.ToObject(tProp.PropertyType), null);
                                }
                               //Console.WriteLine(Spacing + m_tab + ">Setting value");
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
                            object searchresult = null;
                            // TODO: Allow setting objects from hierarchy as value
                            if (LoadedResources.ContainsKey(type) && LoadedResources[type].ContainsKey(targetName))
                            {
                                searchresult = LoadedResources[type][targetName];
                               //Console.WriteLine(Spacing + m_tab + ">Setting value from user resource reference");
                            }
                            else
                            {
                                try
                                {
                                    searchresult = SearchTransform.RecursiveFindWithProperties(cache);
                                }
                                catch { }
                                if (searchresult == null && type.IsSubclassOf(t_uobj))
                                {
                                    UnityEngine.Object[] search = Resources.FindObjectsOfTypeAll(type);
                                    string failedsearch = "";
                                    for (int i = 0; i < search.Length; i++)
                                    {
                                        if (search[i].name == targetName)
                                        {
                                            searchresult = search[i];
                                            //Console.WriteLine(Spacing + m_tab + ">Setting value from existing resource reference");
                                            break;
                                        }
                                        failedsearch += "(" + search[i].name + ") ";
                                    }
                                    if (searchresult == null)
                                    {
                                        Console.WriteLine("Could not find resource: " + targetName + "\n\nThis is what exists for that type:\n" + (failedsearch == "" ? "Nothing. Nothing exists for that type." : failedsearch));
                                    }
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
                catch (Exception E) 
                { 
                    Console.WriteLine(Spacing + "!!!" + E/*+"\n"+E.StackTrace*/); 
                    while (E.InnerException != null) 
                    {
                        E = E.InnerException;
                        Console.WriteLine(E);
                    } 
                }
            }
           //Console.WriteLine(Spacing+"Going up");
            return _instance;
        }
        public static string LogAllComponents(Transform SearchIn, bool Reflection = false, string Indenting = "")
        {
            string result = "";
            Component[] c = SearchIn.GetComponents<Component>();
            foreach (Component comp in c)
            {
                result += "\n" + Indenting + comp.name + " : " + comp.GetType().Name;
                if (Reflection)
                {
                    var t = comp.GetType();
                    var f = t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    var p = t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    foreach (var field in f)
                    {
                        result += $"\n{Indenting} (F).{field.Name} ({field.FieldType.ToString()}) = {field.GetValue(comp)}";
                    }
                    foreach (var field in p)
                    {
                        result += $"\n{Indenting} (P).{field.Name} ({field.PropertyType.ToString()})";
                        try
                        {
                            result += $" = {field.GetValue(comp, null)}";
                        }
                        catch { }
                    }
                }
                else
                {
                    if (comp is MeshRenderer) result += " : Material (" + ((MeshRenderer)comp).material.name + ")";
                }
            }
            for (int i = SearchIn.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = SearchIn.transform.GetChild(i);
                result += LogAllComponents(child, Reflection, Indenting + "  ");
            }
            return result;
        }
    }
}
