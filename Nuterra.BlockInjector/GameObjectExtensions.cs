using UnityEngine;

namespace Nuterra.BlockInjector
{
    public static class GameObjectExtensions
    {
        public static T EnsureComponent<T>(this GameObject obj) where T : Component
        {
            Component comp = obj.GetComponent<T>();
            if (comp == null) comp = obj.AddComponent<T>();
            else try
                {
                    comp.name.Insert(0, "_");
                }
                catch
                {
                    comp = obj.AddComponent<T>();
                }
            return comp as T;
        }

        public static Material SetTexturesToMaterial(this Material material, Texture2D Alpha = null, Texture2D MetallicGloss = null, Texture2D Emission = null, bool MakeCopy = false) => GameObjectJSON.SetTexturesToMaterial(MakeCopy, material, Alpha, MetallicGloss, Emission);

        public static GameObject FindChildGameObject(this GameObject root, string targetName)
        {
            Transform[] ts = root.transform.GetComponentsInChildren<Transform>();
            foreach (Transform t in ts)
            {
                if (t.gameObject.name == targetName)
                {
                    return t.gameObject;
                }
            }
            return null;
        }
    }
}