using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using System.Reflection;
using Newtonsoft.Json.Serialization;

namespace Nuterra.BlockInjector
{
    public static class GameObjectJSON
    {
        private static Dictionary<Type, Dictionary<string, UnityEngine.Object>> LoadedResources = new Dictionary<Type, Dictionary<string, UnityEngine.Object>>();

        const string m_tab = "  ";

        static Type t_shader = typeof(Shader);
        static Type t_uobj = typeof(UnityEngine.Object);
        static Type t_comp = typeof(UnityEngine.Component);
        public static Material MaterialFromShader(string ShaderName = "StandardTankBlock")
        {
            var shader = GetObjectFromGameResources<Shader>(t_shader, ShaderName, true);
            return new Material(shader);
        }

        public static Material SetTexturesToMaterial(bool MakeCopy, Material material, Texture2D Alpha = null, Texture2D MetallicGloss = null, Texture2D Emission = null)
        {
            bool flag1 = Alpha != null, flag2 = MetallicGloss != null, flag3 = Emission != null;
            List<string> shaderKeywords = new List<string>(material.shaderKeywords);
            Material m = material;
            if (MakeCopy && (flag1 || flag2 || flag3))
            {
                m = new Material(material);
            }
            if (flag1)
            {
                m.SetTexture("_MainTex", Alpha);
            }
            if (flag2)
            {
                m.SetTexture("_MetallicGlossMap", MetallicGloss);
                string value = "_METALLICGLOSSMAP";
                if (!shaderKeywords.Contains(value)) shaderKeywords.Add(value);
            }
            if (flag3)
            {
                m.SetTexture("_EmissionMap", Emission);
                string value = "_EMISSION";
                if (!shaderKeywords.Contains(value)) shaderKeywords.Add(value);
            }
            m.shaderKeywords = shaderKeywords.ToArray();
            return m;
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

        public static GameObject GetBlockFromAssetTable(string BlockName)
        {
            BlockPrefabBuilder.GameBlocksByName(BlockName, out GameObject Block);
            return Block;
        }
        public static GameObject GetBlockFromAssetTable(int BlockID)
        {
            BlockPrefabBuilder.GameBlocksByID(BlockID, out GameObject Block);
            return Block;
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

        static Dictionary<Type, Dictionary<string, UnityEngine.Object>> GameResourceCache = new Dictionary<Type, Dictionary<string, UnityEngine.Object>>();

        public static T GetObjectFromGameResources<T>(Type t, string targetName, bool Log = false) where T : UnityEngine.Object
        {
            if (GameResourceCache.TryGetValue(t, out var CacheLookup))
            {
                if (CacheLookup.TryGetValue(targetName, out var result))
                    return result as T;
            }
            else
            {
                GameResourceCache.Add(t, new Dictionary<string, UnityEngine.Object>());
            }
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
            GameResourceCache[t].Add(targetName, searchresult);
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
                string prop = pprop;
                int arr = prop.IndexOf('[');
                string[] ind = null;
                if (arr != -1)
                {
                    ind = prop.Substring(arr + 1).TrimEnd(']').Split(',');

                    prop = prop.Substring(0, arr);
                }
                var tfield = currentType.GetField(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
                if (tfield != null)
                {
                    currentObject = tfield.GetValue(currentObject);
                    if (arr != -1)
                    {
                        //currentObject = tfield.FieldType.
                    }
                    currentType = tfield.FieldType;
                }
                else
                {
                    var tproperty = currentType.GetProperty(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
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
                    //Console.WriteLine($"<FindTrans:{NameOfProperty}>{(t == null ? "EMPTY" : "RETURN")}");
                    return transform.RecursiveFind(NameOfProperty);
                }
                Transform result = transform;

                while (true)
                {
                    propIndex = NameOfProperty.IndexOf('.');
                    if (propIndex == -1)
                    {
                        var t = result.RecursiveFind(NameOfProperty);
                        Console.WriteLine($"<FindTrans:{NameOfProperty}>{(t == null ? "EMPTY" : "RETURN")}");
                        return t;
                    }
                    int reIndex = NameOfProperty.IndexOf('/', propIndex);
                    int lastIndex = NameOfProperty.LastIndexOf('/', propIndex);
                    if (lastIndex > 0)
                    {
                        string transPath = NameOfProperty.Substring(0, lastIndex);
                        Console.Write($"<Find:{transPath}>");
                        result = result.RecursiveFind(transPath);
                        if (result == null)
                        {
                            Console.WriteLine("EMPTY");
                            return null;
                        }
                    }

                    string propPath;
                    if (reIndex == -1) propPath = NameOfProperty.Substring(propIndex);
                    else propPath = NameOfProperty.Substring(propIndex, Math.Max(reIndex - propIndex, 0));
                    string propClass = NameOfProperty.Substring(lastIndex + 1, Math.Max(propIndex - lastIndex - 1, 0));

                    Console.Write($"<Class:{propClass}>");
                    Component component = result.GetComponent(propClass);
                    Console.Write($"<Property:{propPath}>");
                    object value = component.GetValueFromPath(propPath);

                    if (reIndex == -1)
                    {
                        Console.WriteLine(value == null ? "EMPTY" : "RETURN");
                        return value;
                    }

                    Console.Write("<GetTrans>");
                    result = (value as Component).transform;
                    NameOfProperty = NameOfProperty.Substring(reIndex);
                }
            }
            catch (Exception E)
            {
                Console.WriteLine("RecursiveFindWithProperties failed! " + E);
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
           return CreateGameObject(JObject.Parse(json));
        }

        static bool GetReferenceFromBlockResource(string blockPath, out object reference)
        {
            reference = null;
            int separator = blockPath.IndexOfAny(new char[] { '.', '/' });
            if (separator == -1)
            {
                Console.WriteLine("Reference path is invalid! Expected block name and path to GameObject (" + blockPath + ")");
                return false;
            }
            string sRefBlock = blockPath.Substring(0, separator);
            
            GameObject refBlock;
            if (int.TryParse(sRefBlock, out int ID))
                refBlock = GetBlockFromAssetTable(ID);
            else
                refBlock = GetBlockFromAssetTable(sRefBlock);

            if (refBlock == null)
            {
                Console.WriteLine("Reference block is nonexistent! (" + sRefBlock + ")");
                return false;
            }
            string sRefPath = blockPath.Substring(separator + 1);
            reference = RecursiveFindWithProperties(refBlock.transform, sRefPath);
            if (reference == null)
            {
                Console.WriteLine("Reference result is null! (block" + sRefBlock + ", path " + sRefPath + ")");
                return false;
            }
            return true;
        }

        public static GameObject CreateGameObject(JObject json, GameObject GameObjectToPopulate = null, string Spacing = "")
        {
            if (GameObjectToPopulate == null)
            {
                GameObjectToPopulate = new GameObject("New Deserialized Object");
            }
            SearchTransform = GameObjectToPopulate.transform;
            return CreateGameObject_Internal(json, GameObjectToPopulate, Spacing);
        }
        static GameObject CreateGameObject_Internal(JObject json, GameObject GameObjectToPopulate, string Spacing)
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
                    bool Reference = property.Name.StartsWith("Reference");
                    if (Duplicate || Reference || property.Name.StartsWith("GameObject") || property.Name.StartsWith("UnityEngine.GameObject"))
                    {
                        string name = "Object Child";
                        int GetCustomName = property.Name.LastIndexOf('|');
                        if (GetCustomName != -1)
                        {
                            name = property.Name.Substring(GetCustomName + 1);
                        }

                        GameObject newGameObject = null;
                        if (Reference)
                        {
                            if (GetReferenceFromBlockResource(name, out object refTarget))
                            {
                                GameObject refObject = null;
                                if (refTarget is GameObject _refObject)
                                {
                                    refObject = _refObject;
                                }
                                else
                                { 
                                    if (refTarget is Component)
                                    {
                                        if (refTarget is Transform refTrans)
                                        {
                                            refObject = refTrans.gameObject;
                                        }
                                        else // Duplicate component
                                        {
                                            Type refType = refTarget.GetType();
                                            object component = result.GetComponent(refType);
                                            if (component as Component == null)
                                                component = result.AddComponent(refType);
                                            ShallowCopy(refType, refTarget, component);
                                            ApplyValues(component, refType, property.Value as JObject, Spacing);
                                            continue;
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Property of Reference path is not a component or object! (" + name + ")");
                                        continue;
                                    }
                                }
                                newGameObject = GameObject.Instantiate(refObject);
                                string newName = refObject.name;
                                int count = 1;
                                while (result.transform.Find(newName))
                                {
                                    newName = name + "_" + (++count).ToString();
                                }
                                newGameObject.name = newName;
                                newGameObject.transform.parent = result.transform;
                                newGameObject.transform.localPosition = refObject.transform.localPosition;
                                newGameObject.transform.localRotation = refObject.transform.localRotation;
                                newGameObject.transform.localScale = refObject.transform.localScale;
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else
                        {
                            if (Duplicate && name.Contains('/'))
                                newGameObject = (SearchTransform.RecursiveFindWithProperties(name) as Component)?.gameObject;
                            if (newGameObject == null)
                                newGameObject = result.transform.Find(name)?.gameObject;
                        }

                        if (!newGameObject)
                        {
                            if (property.Value.Type == JTokenType.Null)
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
                            if (property.Value.Type == JTokenType.Null)
                            {
                                GameObject.DestroyImmediate(newGameObject);
                                continue;
                            }
                            if (Duplicate)
                            {
                                newGameObject = GameObject.Instantiate(newGameObject);
                                name = name.Substring(1 + name.LastIndexOfAny(new char[] { '/', '.' }));
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
                        CreateGameObject_Internal(property.Value as JObject, newGameObject, Spacing +  m_tab);
                    }
                    else
                    {
                        Type componentType = GetType(property.Name);
                        if (componentType == null) continue;
                        object component = result.GetComponent(componentType);
                        if (property.Value.Type == JTokenType.Null)
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
            BindingFlags bind = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (JProperty jsonProperty in json.Properties())
            {
                try
                {
                    string name = jsonProperty.Name;
                    //Console.WriteLine(Spacing + m_tab + property.Name);
                    int GetCustomName = jsonProperty.Name.IndexOf('|');
                    bool Wipe = false, Instantiate = false;
                    if (GetCustomName != -1)
                    {
                        Wipe = name.StartsWith("Wipe");
                        Instantiate = name.StartsWith("Instantiate");
                        name = jsonProperty.Name.Substring(GetCustomName + 1);
                    }
                    FieldInfo tField = instanceType.GetField(name, bind);
                    PropertyInfo tProp = instanceType.GetProperty(name, bind);
                    //MethodInfo tMethod = instanceType.GetMethod(name, bind);
                    bool UseField = tProp == null;
                    //bool UseMethod = false;
                    if (UseField && tField == null)
                    {
                        //UseMethod = tMethod != null;
                        //if (!UseMethod)

                        Console.WriteLine(Spacing + "!!! Property '" + name + "' does not exist in type '" + instanceType.Name + "'");
                        continue;
                    }
                    #region if (UseMethod)
                    //if (UseMethod)
                    //{
                    //    try
                    //    {
                    //        //Console.WriteLine(Spacing + m_tab + ">Calling method");
                    //        var pValues = jsonProperty.Value as JObject;
                    //        var parameters = tMethod.GetParameters();
                    //        object[] invokeParams = new object[parameters.Length];
                    //        for (int p = 0; p < parameters.Length; p++)
                    //        {
                    //            var param = parameters[p];
                    //            if (pValues.TryGetValue(param.Name, out JToken pValue))
                    //            {
                    //                invokeParams[p] = pValue.ToObject(param.ParameterType);
                    //            }
                    //            else if (param.HasDefaultValue)
                    //            {
                    //                invokeParams[p] = param.DefaultValue;
                    //            }
                    //            else
                    //            {
                    //                invokeParams[p] = null;
                    //            }
                    //        }
                    //        tMethod.Invoke(_instance, invokeParams);
                    //    }
                    //    catch(Exception e)
                    //    {
                    //        Console.WriteLine(e + "\nMethod use failed! (" + jsonProperty.Name + ")" + (jsonProperty.Value is JObject ? "" : " - Property value is not an object with parameters"));
                    //    }
                    //    continue;
                    //}
                    #endregion 

                    if (jsonProperty.Value is JObject jObject)
                    {
                        SetJSONObject(jObject, ref instance, Spacing, Wipe, Instantiate, tField, tProp, UseField);
                    }
                    else if (jsonProperty.Value is JArray jArray)
                    {
                        object sourceArray = Wipe ? null : (
                            UseField ? tField.GetValue(instance) : (
                                tProp.CanRead ? tProp.GetValue(instance, null) : null));
                        var newArray = MakeJSONArray(sourceArray, UseField ? tField.FieldType : tProp.PropertyType, jArray, Spacing, Wipe); // add Wipe param, copy custom names to inside new method
                        if (UseField)
                        {
                            tField.SetValue(instance, newArray);
                        }
                        else if (tProp.CanWrite)
                        {
                            tProp.SetValue(instance, newArray, null);
                        }
                        else throw new TargetException("Property is read-only!");
                    }
                    else if (jsonProperty.Value is JValue jValue)
                    {
                        SetJSONValue(jValue, jsonProperty, instance, UseField, tField, tProp);
                    }
                }
                catch (Exception E)
                {
                    Console.WriteLine(Spacing + "!!! Error on modifying property " + jsonProperty.Name);
                    Console.WriteLine(Spacing + "!!! " + E/*+"\n"+E.StackTrace*/);
                }
            }
           //Console.WriteLine(Spacing+"Going up");
            return instance;
        }

        private static void ShallowCopy(Type sharedType, object source, object target)
        {
            var fields = sharedType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            var props = sharedType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                try
                {
                    field.SetValue(target, field.GetValue(source));
                }
                catch { }
            }
            foreach (var prop in props)
            {
                try
                {
                    if (prop.CanRead && prop.CanWrite)
                        prop.SetValue(target, prop.GetValue(source), null);
                }
                catch { }
            }
        }

        static Type t_ilist = typeof(IList);
        private static IList MakeJSONArray(object originalArray, Type ArrayType, JArray Deserialize, string Spacing, bool Wipe)
        {
            IList newList;
            Type itemType;
            //if (ArrayType.IsSubclassOf(t_ilist))
            //{
            IList sourceList = Wipe ? null : originalArray as IList;

            if (ArrayType.IsGenericType) itemType = ArrayType.GetGenericArguments()[0];
            else itemType = ArrayType.GetElementType();

            int newCount = Deserialize.Count;
            newList = Activator.CreateInstance(ArrayType, newCount) as IList; // newCount here tells fixed arrays how many items to have. List<> arrays get starting capacity, but is empty.
            if (newCount != newList.Count) // Must be a List<>, which means it can be expanded with the following...
            {
                object def = itemType.IsClass ? null : Activator.CreateInstance(itemType); // Get default (Avoid creation if not needed)
                for (int i = 0; i < newCount; i++) newList.Add(def); // Populate empty list from 0 to length
            }

            for (int i = 0; i < newCount; i++) // Populate!
            {
                object item;
                //if (sourceList != null && i < sourceList.Count)
                //    item = sourceList[i];
                //else
                item = newList[i]; // Do not reference the original object! (Corruption risk)

                if (Deserialize[i] is JObject _jObject)
                {
                    if (item == null)
                    {
                        item = Activator.CreateInstance(itemType); // Create instance, because is needed
                        if (sourceList.Count != 0) // Copy current or last element
                        {
                            ShallowCopy(itemType, sourceList[Math.Min(i, sourceList.Count - 1)], item); // Helpful, trust me
                        }
                    }
                    item = ApplyValues(item, itemType, _jObject, Spacing);
                }
                else if (Deserialize[i] is JArray _jArray)
                {
                    item = MakeJSONArray(item, itemType, _jArray, Spacing, false);
                }
                else if (Deserialize[i] is JValue _jValue)
                {
                    try
                    {
                        item = _jValue.ToObject(itemType);
                    }
                    catch
                    {
                        string cache = _jValue.ToObject<string>();
                        string targetName = cache.Substring(cache.IndexOf('|') + 1);
                        item = GetValueFromString(targetName, cache, itemType);
                    }
                }
                newList[i] = item;
            }
            return newList;
            //}
            //else throw new Exception($"Trying to modify array to non-array type (Does not implement IList interface)");
        }

        private static void SetJSONObject(JObject jObject, ref object instance, string Spacing, bool Wipe, bool Instantiate, FieldInfo tField, PropertyInfo tProp, bool UseField)
        {
            if (UseField)
            {
                object original, rewrite;
                if (!Wipe)
                {
                    original = tField.GetValue(instance);
                    if (Instantiate)
                    {
                        if (tField.FieldType.IsSubclassOf(t_comp))
                        {
                            bool isActive = ((GameObject)typeof(Component).GetProperty("gameObject").GetValue(original, null)).activeInHierarchy;
                            var nObj = Component.Instantiate(original as Component);
                            nObj.gameObject.SetActive(isActive);
                            //Console.WriteLine(Spacing + m_tab + ">Instantiating");
                            var cacheSearchTransform = SearchTransform;
                            CreateGameObject(jObject, nObj.gameObject, Spacing + m_tab + m_tab);
                            SearchTransform = cacheSearchTransform;
                            //Console.WriteLine(LogAllComponents(nObj.transform, false, Spacing + m_tab));
                            rewrite = nObj;
                            goto doneField;
                        }
                        else
                        {
                            Console.WriteLine("Cannot Instantiate field that is not of type Component! (" + tField.Name + ")");
                        }
                    }
                    if (tField.FieldType.IsClass)
                    {
                        try
                        {
                            object newobj = Activator.CreateInstance(tField.FieldType);
                            ShallowCopy(tField.FieldType, original, newobj);
                            original = newobj;
                        }
                        catch { }
                    }
                    rewrite = ApplyValues(original, tField.FieldType, jObject, Spacing + m_tab);
                    doneField:;
                }
                else
                {
                    original = Activator.CreateInstance(tField.FieldType);
                    rewrite = ApplyValues(original, tField.FieldType, jObject, Spacing + m_tab);
                }
                try { tField.SetValue(instance, rewrite); } catch (Exception E) { Console.WriteLine(Spacing + m_tab + "!!!" + E.ToString()); }
            }
            else
            {
                object original, rewrite;
                if (!Wipe)
                {
                    original = tProp.GetValue(instance, null);
                    if (Instantiate)
                    {
                        if (tProp.PropertyType.IsSubclassOf(t_comp))
                        {
                            bool isActive = ((GameObject)typeof(Component).GetProperty("gameObject").GetValue(original, null)).activeInHierarchy;
                            var nObj = Component.Instantiate(original as Component);
                            nObj.gameObject.SetActive(isActive);
                            //Console.WriteLine(Spacing + m_tab + ">Instantiating");
                            var cacheSearchTransform = SearchTransform;
                            CreateGameObject(jObject, nObj.gameObject, Spacing + m_tab + m_tab);
                            SearchTransform = cacheSearchTransform;
                            //Console.WriteLine(LogAllComponents(nObj.transform, false, Spacing + m_tab));
                            rewrite = nObj;
                            goto doneProp;
                        }
                        else
                        {
                            Console.WriteLine("Cannot Instantiate property that is not of type Component! (" + tProp.Name + ")");
                        }
                    }
                    if (tProp.PropertyType.IsClass)
                    {
                        try
                        {
                            object newobj = Activator.CreateInstance(tProp.PropertyType);
                            ShallowCopy(tProp.PropertyType, original, newobj);
                            original = newobj;
                        }
                        catch { }
                    }
                    rewrite = ApplyValues(original, tProp.PropertyType, jObject, Spacing + m_tab);
                    doneProp:;
                }
                else
                {
                    original = Activator.CreateInstance(tProp.PropertyType);
                    rewrite = ApplyValues(original, tProp.PropertyType, jObject, Spacing + m_tab);
                }
                if (tProp.CanWrite)
                    try { tProp.SetValue(instance, rewrite, null); } catch (Exception E) { Console.WriteLine(Spacing + m_tab + "!!!" + E.ToString()); }
            }
        }

        static void SetJSONValue(JValue jValue, JProperty jsonProperty, object _instance, bool UseField, FieldInfo tField = null, PropertyInfo tProp = null)
        {
            try
            {
                if (UseField)
                {
                    tField.SetValue(_instance, jValue.ToObject(tField.FieldType));
                }
                else
                {
                    if (tProp.CanWrite) tProp.SetValue(_instance, jValue.ToObject(tProp.PropertyType), null);
                    else Console.WriteLine("Property is write-locked! (" + jsonProperty.Name + ")");
                }
                return;//continue;
            }
            catch { }
            string cache = jValue.ToObject<string>();
            string targetName = cache.Substring(cache.IndexOf('|') + 1);
            Type type = UseField ? tField.FieldType : tProp.PropertyType;
            object searchresult = GetValueFromString(targetName, cache, type);
            if (UseField)
            {
                tField.SetValue(_instance, searchresult);
            }
            else
            {
                tProp.SetValue(_instance, searchresult, null);
            }
        }

        static object GetValueFromString(string search, string searchFull, Type outType)
        {
            if (searchFull.StartsWith("Reference"))
            {
                if (GetReferenceFromBlockResource(search, out var result)) // Get value from a block in the game
                    return result;
            }
            else if (LoadedResources.TryGetValue(outType, out var dict) && dict.TryGetValue(search, out var result))
            {
                return result; // Get value from a value in the user database
            }
            else
            {
                try
                {
                    var recursive = SearchTransform.RecursiveFindWithProperties(searchFull);
                    if (recursive != null) return recursive; // Get value from this block
                }
                catch { }
                if (outType.IsSubclassOf(t_uobj))
                {
                    return GetObjectFromGameResources<UnityEngine.Object>(outType, search, true);
                }
            }
            return null;
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

        //IList CopyToNew(Type source, object from, int count)
        //{
        //    object result;
        //    if (source.IsArray)
        //    {
        //        result = System.Activator.CreateInstance(source, count)
        //    }
        //}
    }
}
