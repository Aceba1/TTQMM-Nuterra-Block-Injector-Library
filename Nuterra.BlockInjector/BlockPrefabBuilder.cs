using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using System.Collections;

namespace Nuterra.BlockInjector
{
    public sealed class BlockPrefabBuilder
    {
        public static Dictionary<string, GameObject> PrefabList = new Dictionary<string, GameObject>();
        public static Dictionary<int, string> OverrideValidity = new Dictionary<int, string>();
        internal static Dictionary<int, uint> ReparseVersion = new Dictionary<int, uint>();
        internal class RegisterTimer : MonoBehaviour
        {
            public BlockPrefabBuilder prefabToRegister;
            public CustomBlock customBlock;
            public void CallBlockPrefabBuilder(float time, BlockPrefabBuilder PrefabToRegister, CustomBlock CustomBlock)
            {
                prefabToRegister = PrefabToRegister;
                customBlock = CustomBlock;
                Invoke("RunBlock", time);
            }

            bool Passed;

            private void RunBlock()
            {
                Passed = BlockLoader.Register(customBlock);
                Singleton.DoOnceAfterStart(FinishBlock);
            }

            private void FinishBlock()
            {
                customBlock.Prefab.SetActive(false);
                if (Passed)
                    BlockLoader.FixBlockUnlockTable(customBlock);
                UnityEngine.GameObject.Destroy(this.gameObject);
            }
        }

        private bool _finished = false;
        private Visible _visible;
        private ModuleCustomBlock _mcb;
        public TankBlock TankBlock { get; private set; }
        private Damageable _damageable;
        private ModuleDamage _moduleDamage;
        private AutoSpriteRenderer _spriteRenderer;
        private CustomBlock _customBlock;
//        private UnityEngine.Networking.NetworkIdentity _netid;
//        private NetBlock _netblock;

        public BlockPrefabBuilder()
        {
            Initialize(new GameObject("newBlock"), true);
        }

        public BlockPrefabBuilder(GameObject prefab, bool MakeCopy = false)
        {
            if (MakeCopy)
                Initialize(GameObject.Instantiate(prefab), false);
            else
                Initialize(prefab, false);
        }

        public BlockPrefabBuilder(string PrefabFromResource, bool RemoveRenderers = true)
        {
            CreateFromRes(PrefabFromResource, RemoveRenderers);
        }

        public BlockPrefabBuilder(string PrefabFromResource, Vector3 Offset, bool RemoveRenderers = true)
        {
            CreateFromRes(PrefabFromResource, RemoveRenderers);
            for (int i = 0; i < Prefab.transform.childCount; i++)
            {
                Prefab.transform.GetChild(i).localPosition += Offset;
            }
        }

        private static Dictionary<string, GameObject> _gameBlocks;
        internal static Dictionary<string, GameObject> GameBlocks
        {
            get
            {
                if (_gameBlocks == null)
                {
                    _gameBlocks = new Dictionary<string, GameObject>();
                    var gos = Resources.FindObjectsOfTypeAll<GameObject>();
                    foreach (var go in gos)
                    {
                        try
                        {
                            if (go.GetComponent<TankBlock>())
                            {
                                _gameBlocks.Add(go.name.Replace("_", "").Replace(" ", "").ToLower(), go);
                            }
                        }
                        catch { /*fail silently*/ }
                    }
                }
                return _gameBlocks;
            }
        }

        internal void CreateFromRes(string PrefabFromResource, bool RemoveRenderers)
        {
            GameObject original = null;
            string NewSearch = PrefabFromResource.Replace("(", "").Replace(")", "").Replace("_", "").Replace(" ", "").ToLower();
            try
            {
                if (!GameBlocks.TryGetValue(NewSearch, out original))
                {
                    PrefabList.TryGetValue(PrefabFromResource, out original);
                }
            }
            catch { /*report in below condition*/ }
            if (original == null)
            {
                string errStr = $"No prefab named '{PrefabFromResource}' => '{NewSearch}' could be found... (Make sure block exists before this one!)";
                Console.WriteLine(errStr);
                throw new Exception(errStr);
            }
            var copy = UnityEngine.Object.Instantiate(original);
            Initialize(copy, false);
            if (RemoveRenderers)
            {
                RemoveChildrenWithComponent(true, null, typeof(MeshRenderer), typeof(MeshFilter), typeof(Collider));
            }
            RemoveChildrenWithComponent(true, null, typeof(ColliderSwapper), typeof(TTNetworkTransform));
        }

        const BindingFlags b = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
        static readonly PropertyInfo visible = typeof(TankBlock).GetProperty("visible", b);
        static readonly FieldInfo m_VisibleComponent = typeof(Visible).GetField("m_VisibleComponent", b);
        static readonly FieldInfo FilledCellsGravityScaleFactors = typeof(TankBlock).GetField("FilledCellsGravityScaleFactors", b);
        private void Initialize(GameObject prefab, bool clearGridInfo)
        {
            _customBlock = new CustomBlock();
            _customBlock.Prefab = prefab;
            _customBlock.Prefab.SetActive(false);
            GameObject.DontDestroyOnLoad(_customBlock.Prefab);

            _customBlock.Prefab.tag = "TankBlock";
            _customBlock.Prefab.layer = Globals.inst.layerTank;

            _damageable = _customBlock.Prefab.EnsureComponent<Damageable>();
            _moduleDamage = _customBlock.Prefab.EnsureComponent<ModuleDamage>();
            _spriteRenderer = _customBlock.Prefab.EnsureComponent<AutoSpriteRenderer>();

            TankBlock = _customBlock.Prefab.EnsureComponent<TankBlock>();

            _visible = _customBlock.Prefab.EnsureComponent<Visible>();

//            _netid = _customBlock.Prefab.EnsureComponent<UnityEngine.Networking.NetworkIdentity>();
//            _netblock = _customBlock.Prefab.EnsureComponent<NetBlock>();

            visible.SetValue(TankBlock, _visible, null);
            m_VisibleComponent.SetValue(_visible, TankBlock as Component);
            if (clearGridInfo)
            {
                TankBlock.attachPoints = new Vector3[] { };
                TankBlock.filledCells = new IntVector3[] { new Vector3(0, 0, 0) };
                FilledCellsGravityScaleFactors.SetValue(TankBlock, new float[] { 1f });
            }

            _mcb = _customBlock.Prefab.EnsureComponent<ModuleCustomBlock>();

            Transform transform = _customBlock.Prefab.transform;
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        public GameObject Prefab { get => _customBlock.Prefab; }

        public BlockPrefabBuilder RegisterLater(float Time = 5f)
        {
            ThrowIfFinished();
            string name = _customBlock.Name;

            while (PrefabList.ContainsKey(name)) name += "+";
            _customBlock.Prefab.name = name;
            PrefabList.Add(name, _customBlock.Prefab);
            OverrideValidity.Add(_customBlock.BlockID, _customBlock.Prefab.name);
            //_customBlock.Prefab.transform.position = Vector3.down * 1000f;
            new GameObject().AddComponent<RegisterTimer>().CallBlockPrefabBuilder(Time, this, _customBlock);
            _finished = true;
            return this;
        }

        public string LogAllComponents(Transform SearchIn = null, string Indenting = "")
        {
            string result = "";
            Transform _search = TankBlock.transform;
            if (SearchIn != null)
            {
                _search = SearchIn;
            }
            Component[] c = _search.GetComponents<Component>();
            foreach (Component comp in c)
            {
                result += "\n" + Indenting + comp.name + " : " + comp.GetType().Name;
            }
            for (int i = _search.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = _search.transform.GetChild(i);
                result += LogAllComponents(child, Indenting + " ");
            }
            return result;
        }

        public BlockPrefabBuilder RemoveChildrenWithComponent(bool RemoveJustComponent = false, Transform SearchIn = null, params Type[] typesToRemove)
        {
            Transform _search = TankBlock.transform;
            if (SearchIn != null)
            {
                _search = SearchIn;
            }
            for (int i1 = 0; i1 < typesToRemove.Length; i1++)
            {
                foreach (Component c in _search.GetComponents(typesToRemove[i1]))
                {
                    if (c != null)
                    {
                        if (RemoveJustComponent)
                        {
                            Component.DestroyImmediate(c);
                        }
                        else
                        {
                            GameObject.DestroyImmediate(_search.gameObject);
                            return this;
                        }
                    }
                }
            }
            for (int i = _search.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = _search.transform.GetChild(i);
                RemoveChildrenWithComponent(RemoveJustComponent, child, typesToRemove);
            }
            return this;
        }

        public BlockPrefabBuilder RemoveChildrenWithComponent<T>(bool RemoveJustComponent = false, Transform SearchIn = null) where T : Component
        {
            Transform _search = TankBlock.transform;
            if (SearchIn != null)
            {
                _search = SearchIn;
            }
            for (int i = _search.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = _search.transform.GetChild(i);
                Component c = child.GetComponent<T>();
                if (c == null)
                {
                    RemoveChildrenWithComponent<T>(RemoveJustComponent, child);
                }
                else
                {
                    if (RemoveJustComponent)
                    {
                        RemoveChildrenWithComponent<T>(RemoveJustComponent, child);
                        Component.DestroyImmediate(c);
                    }
                    else
                        GameObject.DestroyImmediate(child.gameObject);
                }
            }
            return this;
        }

        public BlockPrefabBuilder SetGrade(int Grade = 0)
        {
            ThrowIfFinished();
            _customBlock.Grade = Grade;
            return this;
        }

        public BlockPrefabBuilder SetHP(int HealthPoints)
        {
            ThrowIfFinished();
            _moduleDamage.maxHealth = HealthPoints;
            _damageable.SetMaxHealth((float)HealthPoints);
            return this;
        }

        public BlockPrefabBuilder SetDetachFragility(float Fragility)
        {
            ThrowIfFinished();
            _moduleDamage.m_DamageDetachFragility = Fragility;
            return this;
        }

        static FieldInfo m_DamageableType = typeof(Damageable).GetField("m_DamageableType", b);

        public BlockPrefabBuilder SetDamageableType(ManDamage.DamageableType type)
        {
            ThrowIfFinished();
            m_DamageableType.SetValue(_damageable, type);
            return this;
        }

        public BlockPrefabBuilder SetName(string blockName)
        {
            ThrowIfFinished();
            _customBlock.Name = blockName;
            _customBlock.Prefab.name = blockName;
            return this;
        }

        public BlockPrefabBuilder SetBlockID(int id)
        {
            ThrowIfFinished();
            _customBlock.BlockID = id;
            _visible.m_ItemType = new ItemTypeInfo(ObjectTypes.Block, id);
            if (ReparseVersion.TryGetValue(id, out uint reparse))
            {
                if (!BlockLoader.AcceptOverwrite) throw new ArgumentException("A block with the same ID (" + id.ToString() + ") has already been defined!");
                ReparseVersion[id] = ++reparse; _mcb.reparse_version_cache = reparse;
            }
            else
            {
                ReparseVersion[id] = 0; _mcb.reparse_version_cache = 0;
            }
            return this;
        }

        [Obsolete("Hex code is unnecessary, please supply only an ID")]
        public BlockPrefabBuilder SetBlockID(int id, string Net128HashHex)
        {
            SetBlockID(id);
            //if (Net128HashHex != "")
            //    typeof(UnityEngine.Networking.NetworkIdentity).GetField("m_AssetId", b).SetValue(_netid, UnityEngine.Networking.NetworkHash128.Parse(Net128HashHex
            return this;
        }

        public BlockPrefabBuilder SetDescription(string description)
        {
            ThrowIfFinished();
            _customBlock.Description = description;
            return this;
        }

        public BlockPrefabBuilder SetPrice(int price)
        {
            ThrowIfFinished();
            _customBlock.Price = price;
            return this;
        }

        public BlockPrefabBuilder SetFaction(FactionSubTypes faction)
        {
            ThrowIfFinished();
            _customBlock.Faction = faction;
            return this;
        }

        static FieldInfo m_BlockRarity = typeof(TankBlock).GetField("m_BlockRarity", b);

        public BlockPrefabBuilder SetRarity(BlockRarity rarity)
        {
            ThrowIfFinished();
            _customBlock.Rarity = rarity;
            m_BlockRarity.SetValue(TankBlock, rarity);
            return this;
        }

        static FieldInfo m_BlockCategory = typeof(TankBlock).GetField("m_BlockCategory", b);

        public BlockPrefabBuilder SetCategory(BlockCategories category)
        {
            ThrowIfFinished();
            _customBlock.Category = category;
            m_BlockCategory.SetValue(TankBlock, category);
            //_block.m_BlockCategory = category;
            return this;
        }

        /// <summary>
        /// Define both the cells and APs of a block
        /// </summary>
        /// <param name="cells">Cells used to define the used space in a tech's grid</param>
        /// <param name="aps">The Attach Points of a block. Half a unit away from the center of a cell in one direction (0f, -0.5f, 0f)</param>
        /// <param name="IgnoreFaults">Do not throw exception when given invalid arguments</param>
        /// <returns></returns>
        public BlockPrefabBuilder SetSizeManual(IntVector3[] cells, Vector3[] aps, bool IgnoreFaults = false)
        {
            SetSizeManual(cells, IgnoreFaults);
            SetAPsManual(aps, IgnoreFaults);
            return this;
        }

        /// <summary>
        /// Define only the cells, without touching APs
        /// </summary>
        /// <param name="cells">Cells used to define the used space in a tech's grid</param>
        /// <param name="IgnoreFaults">Do not throw exception when given invalid arguments</param>
        /// <returns></returns>
        public BlockPrefabBuilder SetSizeManual(IntVector3[] cells, bool IgnoreFaults = false)
        {
            ThrowIfFinished();
            var gravityScale = new float[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                if (!IgnoreFaults && (cell.x < 0f) || (cell.y < 0f) || (cell.z < 0f))
                {
                    throw new Exception("There is a cell out of range! (" + cell.x.ToString() + ", " + cell.y.ToString() + ", " + cell.z.ToString() + ")\nMake sure that cells do not go below 0 in any axis. Check your APs as well");
                }
                gravityScale[i] = 1f;
            }
            TankBlock.filledCells = cells;
            FilledCellsGravityScaleFactors.SetValue(TankBlock, gravityScale);
            return this;
        }

        /// <summary>
        /// Define only the APs of a block
        /// </summary>
        /// <param name="aps">The Attach Points of a block. Half a unit away from the center of a cell in one direction (0f, -0.5f, 0f)</param>
        /// <param name="IgnoreFaults">Do not throw exception when given invalid arguments</param>
        /// <returns></returns>
        public BlockPrefabBuilder SetAPsManual(Vector3[] aps, bool IgnoreFaults = false)
        {
            ThrowIfFinished();
            if (!IgnoreFaults)
            {
                for (int i = 0; i < aps.Length; i++)
                {
                    if ((aps[i].x % 0.5f != 0f) || (aps[i].y % 0.5 != 0) || (aps[i].z % 0.5 != 0))
                    {
                        throw new Exception("AP #" + i.ToString() + " is not in the center of a face! (" + aps[i].x.ToString() + ", " + aps[i].y.ToString() + ", " + aps[i].z.ToString() + ")");
                    }
                    int facecheck = 0;
                    if (Mathf.RoundToInt(aps[i].x * 10f + 50f) % 10 == 5) facecheck++;
                    if (Mathf.RoundToInt(aps[i].y * 10f + 50f) % 10 == 5) facecheck++;
                    if (Mathf.RoundToInt(aps[i].z * 10f + 50f) % 10 == 5) facecheck++;
                    if (facecheck != 1)
                    {
                        throw new Exception("AP #" + i.ToString() + " is not in the center of a face! (" + aps[i].x.ToString() + ", " + aps[i].y.ToString() + ", " + aps[i].z.ToString() + ")");
                    }
                }
            }
            TankBlock.attachPoints = aps;
            return this;
        }

        /// <summary>
        /// Make a 3D rect of grid cells set to the defined size
        /// </summary>
        /// <param name="size">The extents of a block</param>
        /// <returns></returns>
        public BlockPrefabBuilder SetSize(IntVector3 size)
        {
            ThrowIfFinished();
            List<IntVector3> cells = new List<IntVector3>();
            for (int x = 0; x < Math.Abs(size.x); x++)
            {
                for (int y = 0; y < Math.Abs(size.y); y++)
                {
                    for (int z = 0; z < Math.Abs(size.z); z++)
                    {
                        cells.Add(new Vector3(Math.Sign(size.x) * x, Math.Sign(size.y) * y, Math.Sign(size.z) * z));
                    }
                }
            }

            var gravityScale = new float[cells.Count];
            for (int i = 0; i < cells.Count; i++)
            {
                gravityScale[i] = 1f;
            }
            FilledCellsGravityScaleFactors.SetValue(TankBlock, gravityScale);
            TankBlock.filledCells = cells.ToArray();
            return this;
        }

        /// <summary>
        /// Make a 3D rect of grid cells set to the defined size, and generate APs
        /// </summary>
        /// <param name="size">the extents of a block</param>
        /// <param name="points"></param>
        /// <returns></returns>
        public BlockPrefabBuilder SetSize(IntVector3 size, AttachmentPoints points = AttachmentPoints.Bottom)
        {
            ThrowIfFinished();
            List<IntVector3> cells = new List<IntVector3>();
            List<Vector3> aps = new List<Vector3>();
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    for (int z = 0; z < size.z; z++)
                    {
                        cells.Add(new Vector3(x, y, z));
                        if (y == 0)
                        {
                            aps.Add(new Vector3(x, -0.5f, z));
                        }
                        if (points == AttachmentPoints.All)
                        {
                            if (x == 0)
                            {
                                aps.Add(new Vector3(-0.5f, y, z));
                            }
                            if (x == size.x - 1)
                            {
                                aps.Add(new Vector3(x + 0.5f, y, z));
                            }
                            if (y == size.y - 1)
                            {
                                aps.Add(new Vector3(x, y + 0.5f, z));
                            }
                            if (z == 0)
                            {
                                aps.Add(new Vector3(x, y, -0.5f));
                            }
                            if (z == size.z - 1)
                            {
                                aps.Add(new Vector3(x, y, z + 0.5f));
                            }
                        }
                    }
                }
            }

            var gravityScale = new float[cells.Count];
            for (int i = 0; i < cells.Count; i++)
            {
                gravityScale[i] = 1f;
            }
            FilledCellsGravityScaleFactors.SetValue(TankBlock, gravityScale);
            TankBlock.filledCells = cells.ToArray();
            TankBlock.attachPoints = aps.ToArray();
            return this;
        }

        public enum AttachmentPoints
        {
            Bottom,
            All,
        }

        public BlockPrefabBuilder SetMass(float mass)
        {
            ThrowIfFinished();
            if (mass <= 0f) throw new ArgumentOutOfRangeException(nameof(mass), "Cannot be lower or equal to zero");
            TankBlock.m_DefaultMass = mass;
            return this;
        }

        public BlockPrefabBuilder SetCenterOfMass(Vector3 CenterOfMass)
        {
            ThrowIfFinished();
            Transform transform = Prefab.transform.Find("CentreOfMass");
            if (transform == null)
            {
                transform = new GameObject("CentreOfMass").transform;
                transform.parent = Prefab.transform;
            }
            transform.localPosition = CenterOfMass;
            transform = Prefab.transform.Find("aCol");
            if (transform != null) transform.localPosition = CenterOfMass;
            _mcb.HasInjectedCenterOfMass = true;
            _mcb.InjectedCenterOfMass = CenterOfMass;
            return this;
        }

        public BlockPrefabBuilder SetModel(Mesh Mesh, bool CreateBoxCollider, Material Material = null)
        {
            return SetModel(Mesh, CreateBoxCollider, Material, new PhysicMaterial());
        }
        
        public BlockPrefabBuilder SetModel(Mesh Mesh, bool CreateBoxCollider, Material Material = null, PhysicMaterial PhysicMaterial = null)
        {
            ThrowIfFinished();
            GameObject model = new GameObject("m_MeshRenderer");
            if (Mesh != null)
            {
                model.AddComponent<MeshFilter>().sharedMesh = Mesh;
                model.AddComponent<MeshRenderer>().material = Material == null ? GameObjectJSON.MaterialFromShader() : Material;
            }
            if (CreateBoxCollider)
            {
                var bc = model.AddComponent<BoxCollider>();
                if (Mesh != null)
                {
                    Mesh.RecalculateBounds();
                    bc.size = Mesh.bounds.size - Vector3.one * 0.2f;
                    bc.center = Mesh.bounds.center;
                }
                bc.sharedMaterial = PhysicMaterial;
            }
            SetModel(model);
            return this;
        }

        public BlockPrefabBuilder SetModel(Mesh Mesh, Mesh ColliderMesh = null, bool ConvexCollider = true, Material Material = null)
        {
            return SetModel(Mesh, ColliderMesh, ConvexCollider, Material, new PhysicMaterial());
        }

        public BlockPrefabBuilder SetModel(Mesh Mesh, Mesh ColliderMesh = null, bool ConvexCollider = true, Material Material = null, PhysicMaterial PhysicMaterial = null)
        {
            ThrowIfFinished();
            GameObject model = new GameObject("m_MeshRenderer");
            if (Mesh != null)
            {
                model.AddComponent<MeshFilter>().sharedMesh = Mesh;
                model.AddComponent<MeshRenderer>().sharedMaterial = Material == null ? GameObjectJSON.MaterialFromShader() : Material;
            }
            if (ColliderMesh != null)
            {
                var mc = model.AddComponent<MeshCollider>();
                mc.convex = ConvexCollider;
                mc.sharedMesh = ColliderMesh;
                mc.sharedMaterial = PhysicMaterial;
            }
            SetModel(model);
            return this;
        }

        public BlockPrefabBuilder SetModel(GameObject renderObject, bool MakeCopy = false)
        {
            ThrowIfFinished();
            GameObject _renderObject;
            if (MakeCopy)
                _renderObject = GameObject.Instantiate(renderObject);
            else
                _renderObject = renderObject;
            _renderObject.transform.parent = _customBlock.Prefab.transform;
            _renderObject.transform.localPosition = Vector3.zero;
            _renderObject.transform.localRotation = Quaternion.identity;
            _renderObject.layer = Globals.inst.layerTank;

            //foreach (GameObject child in _renderObject.EnumerateHierarchy(false, false))
            //{
            //    child.layer = _renderObject.layer;
            //}

            return this;
        }

        public BlockPrefabBuilder SetIcon(Texture2D displaySprite)
        {
            ThrowIfFinished();
            _customBlock.DisplaySprite = GameObjectJSON.SpriteFromImage(displaySprite);
            return this;
        }

        public BlockPrefabBuilder SetIcon(Sprite displaySprite)
        {
            ThrowIfFinished();
            _customBlock.DisplaySprite = displaySprite;
            return this;
        }

        public BlockPrefabBuilder AddComponent<TBehaviour>(Action<TBehaviour> preparer) where TBehaviour : MonoBehaviour
        {
            ThrowIfFinished();
            var component = _customBlock.Prefab.AddComponent<TBehaviour>();
            preparer?.Invoke(component);
            return this;
        }

        public BlockPrefabBuilder AddComponent<TBehaviour>(out TBehaviour NewComponent) where TBehaviour : MonoBehaviour
        {
            ThrowIfFinished();
            NewComponent = _customBlock.Prefab.AddComponent<TBehaviour>();
            return this;
        }

        public BlockPrefabBuilder AddComponent<TBehaviour>() where TBehaviour : MonoBehaviour
        {
            return AddComponent<TBehaviour>(null);
        }

        private void ThrowIfFinished()
        {
            if (_finished)
            {
                throw new InvalidOperationException("Build() already called");
            }
        }

        internal CustomBlock Build()
        {
            _finished = true;
            return _customBlock;
        }
    }
}