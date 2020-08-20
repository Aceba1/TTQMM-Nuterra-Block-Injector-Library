using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using System.Collections;

namespace Nuterra.BlockInjector
{
    public sealed class BlockPrefabBuilder
    {
        //public static Dictionary<string, GameObject> PrefabList = new Dictionary<string, GameObject>();
        //public static Dictionary<int, string> OverrideValidity = new Dictionary<int, string>();
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
        internal ModuleCustomBlock _mcb;
        public TankBlock TankBlock { get; private set; }
        private Damageable _damageable;
        private ModuleDamage _moduleDamage;
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

        public BlockPrefabBuilder(BlockTypes PrefabFromResource, bool RemoveRenderers = true) =>
            CreateFromRes((int)PrefabFromResource, RemoveRenderers);
        public BlockPrefabBuilder(int PrefabFromResource, bool RemoveRenderers = true) =>
            CreateFromRes(PrefabFromResource, RemoveRenderers);
        public BlockPrefabBuilder(string PrefabFromResource, bool RemoveRenderers = true) =>
            CreateFromRes(PrefabFromResource, RemoveRenderers);

        public BlockPrefabBuilder(BlockTypes PrefabFromResource, Vector3 Offset, bool RemoveRenderers = true) : this(PrefabFromResource, RemoveRenderers) =>
            OffsetChildren(Offset);
        public BlockPrefabBuilder(int PrefabFromResource, Vector3 Offset, bool RemoveRenderers = true) : this(PrefabFromResource, RemoveRenderers) =>
            OffsetChildren(Offset);
        public BlockPrefabBuilder(string PrefabFromResource, Vector3 Offset, bool RemoveRenderers = true) : this(PrefabFromResource, RemoveRenderers) =>
            OffsetChildren(Offset);

        public BlockPrefabBuilder OffsetChildren(Vector3 Offset)
        {
            ThrowIfFinished();
            for (int i = 0; i < Prefab.transform.childCount; i++)
            {
                Prefab.transform.GetChild(i).localPosition += Offset;
            }
            return this;
        }

        static string TrimForSafeSearch(string Value) => Value.Replace("(", "").Replace(")", "").Replace("_", "").Replace(" ", "").ToLower();

        private static Dictionary<string, GameObject> _gameBlocksNameDict;
        private static Dictionary<int, GameObject> _gameBlocksIDDict;
        public static bool GameBlocksByName(string ReferenceName, out GameObject Block)
        {
            if (_gameBlocksNameDict == null)
            {
                PopulateRefDictionaries();
            }
            return _gameBlocksNameDict.TryGetValue(TrimForSafeSearch(ReferenceName), out Block);
        }
        public static bool GameBlocksByID(int ReferenceID, out GameObject Block)
        {
            if (_gameBlocksIDDict == null)
            {
                PopulateRefDictionaries();
            }
            return _gameBlocksIDDict.TryGetValue(ReferenceID, out Block);
        }

        private static void PopulateRefDictionaries()
        {
            _gameBlocksIDDict = new Dictionary<int, GameObject>();
            _gameBlocksNameDict = new Dictionary<string, GameObject>();
            var gos = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in gos)
            {
                try
                {
                    if (go.GetComponent<TankBlock>())
                    {
                        _gameBlocksNameDict.Add(TrimForSafeSearch(go.name), go);
                        Visible v = go.GetComponent<Visible>();
                        if (v != null)
                        {
                            _gameBlocksIDDict.Add(v.ItemType, go);
                        }
                    }
                }
                catch { /*fail silently*/ }
            }
        }

        internal void CreateFromRes(int PrefabID, bool RemoveRenderers)
        {
            if (!GameBlocksByID(PrefabID, out GameObject original))
            {
                string errStr = $"No prefab with ID '{PrefabID}' could be found...";
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

        internal void CreateFromRes(string PrefabName, bool RemoveRenderers)
        {
            if (!GameBlocksByName(PrefabName, out GameObject original))
            {
                string errStr = $"No prefab named '{PrefabName}' could be found...";
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
        static readonly PropertyInfo TankBlock_visible = typeof(TankBlock).GetProperty("visible", b);
        static readonly PropertyInfo Visible_block = typeof(Visible).GetProperty("block", b);
        static readonly FieldInfo TankBlock_FilledCellsGravityScaleFactors = typeof(TankBlock).GetField("FilledCellsGravityScaleFactors", b);
        private void Initialize(GameObject prefab, bool clearGridInfo)
        {
            _customBlock = new CustomBlock();
            //_customBlock.RuntimeID = BlockLoader.GetNextAvailableID();
            _customBlock.Prefab = prefab;
            _customBlock.Prefab.SetActive(false);
            GameObject.DontDestroyOnLoad(_customBlock.Prefab);

            _customBlock.Prefab.tag = "TankBlock";
            _customBlock.Prefab.layer = Globals.inst.layerTank;

            _damageable = _customBlock.Prefab.EnsureComponent<Damageable>();
            _moduleDamage = _customBlock.Prefab.EnsureComponent<ModuleDamage>();

            TankBlock = _customBlock.Prefab.EnsureComponent<TankBlock>();

            _visible = _customBlock.Prefab.EnsureComponent<Visible>();

            //            _netid = _customBlock.Prefab.EnsureComponent<UnityEngine.Networking.NetworkIdentity>();
            //            _netblock = _customBlock.Prefab.EnsureComponent<NetBlock>();

            TankBlock_visible.SetValue(TankBlock, _visible, null);
            Visible_block.SetValue(_visible, TankBlock);
            if (clearGridInfo)
            {
                TankBlock.attachPoints = new Vector3[] { };
                TankBlock.filledCells = new IntVector3[] { new Vector3(0, 0, 0) };
                TankBlock_FilledCellsGravityScaleFactors.SetValue(TankBlock, new float[] { 1f });
            }

            _mcb = _customBlock.Prefab.EnsureComponent<ModuleCustomBlock>();

            Transform transform = _customBlock.Prefab.transform;
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        public GameObject Prefab { get => _customBlock.Prefab; }

        public int RuntimeID
        {
            get => _customBlock.RuntimeID;
            //get
            //{
            //    if (string.IsNullOrEmpty(_customBlock.BlockID)) throw new ArgumentException("Tried to get RuntimeID before SetBlockID() was called!");
            //    return _customBlock.RuntimeID;
            //}
        }

        /// <summary>
        /// NOTE: Only for use if the block has already been registered!
        /// </summary>
        /// <returns></returns>
        public BlockPrefabBuilder OverlapExistingRegister()
        {
            ThrowIfFinished();
            if (!BlockLoader.AcceptOverwrite || !BlockLoader.CustomBlocks.ContainsKey(_customBlock.BlockID))//!BlockLoader.NameIDToRuntimeIDTable.TryGetValue(_customBlock.BlockID, out _))
            {
                throw new InvalidOperationException($"OverlapExistingRegister : Block {_customBlock.Name} ({_customBlock.RuntimeID}) has not been registered before! Use RegisterLater()");
            }
            OptimizeCellsForAP();
            RegisterRecipe();
            BlockLoader.Register(_customBlock);
            _gameBlocksNameDict[TrimForSafeSearch(_customBlock.Name)] = _customBlock.Prefab;
            _gameBlocksIDDict[_customBlock.BlockID] = _customBlock.Prefab;
            //_gameBlocksNameDict[_customBlock.BlockID] = _customBlock.Prefab;
            Prefab.SetActive(false);
            _finished = true;
            return this;
        }

        public BlockPrefabBuilder RegisterLater(float Time = 5f)
        {
            //if (BlockLoader.NameIDToRuntimeIDTable.TryGetValue(_customBlock.BlockID, out int overlap))
            if (BlockLoader.CustomBlocks.TryGetValue(_customBlock.BlockID, out CustomBlock _overlap))
            {
                int overlap = _overlap.RuntimeID;
                if (!BlockLoader.AcceptOverwrite)
                {
                    throw new Exception($"Block {_customBlock.Name} ({_customBlock.BlockID}) overlaps with predefined block #{overlap} ({_customBlock.BlockID})!");
                }
                Console.WriteLine($"RegisterLater : Block {_customBlock.Name} ({_customBlock.BlockID}) overlaps with predefined block #{overlap} ({_customBlock.BlockID})! Invoking OverlapExistingRegister()");
                return OverlapExistingRegister();
            }
            ThrowIfFinished();
            //string name = _customBlock.Name;
            //while (PrefabList.ContainsKey(name)) name += "+";
            //_customBlock.Prefab.name = name;
            _customBlock.Prefab.name = BlockLoader.NameIDProtocolStart + _customBlock.BlockID;

            if (_gameBlocksNameDict == null)
            {
                PopulateRefDictionaries();
            }
            _gameBlocksNameDict[TrimForSafeSearch(_customBlock.Name)] = _customBlock.Prefab;
            _gameBlocksIDDict[_customBlock.BlockID] = _customBlock.Prefab;
            //_gameBlocksNameDict[_customBlock.RuntimeID] = _customBlock.Prefab;

            _customBlock.Prefab.transform.position = Vector3.down * 1000f;
            OptimizeCellsForAP();
            RegisterRecipe();
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

            if (_mcb.FilePath == null)
                _mcb.FilePath = Assembly.GetCallingAssembly().Location;

            return this;
        }

        public BlockPrefabBuilder SetBlockID(int id)
        {
            //return SetBlockID(id.ToString());
            ThrowIfFinished();
            _customBlock.BlockID = id;
            _customBlock.RuntimeID = id;
            if (ReparseVersion.TryGetValue(id, out uint reparse))
            {
                if (!BlockLoader.AcceptOverwrite) throw new ArgumentException("A block with the same ID (" + id.ToString() + ") has already been defined!");
                ReparseVersion[id] = ++reparse; _mcb.reparse_version_cache = reparse;
            }
            else
            {
                ReparseVersion[_customBlock.RuntimeID] = 0; _mcb.reparse_version_cache = 0;
            }
            _visible.m_ItemType = new ItemTypeInfo(ObjectTypes.Block, _customBlock.RuntimeID);
            return this;
        }

        //public BlockPrefabBuilder SetBlockID(string id)
        //{
        //    ThrowIfFinished();
        //    if (string.IsNullOrEmpty(id)) throw new ArgumentException("The ID parameter cannot be null or empty!");
        //    if (!string.IsNullOrEmpty(_customBlock.BlockID)) throw new ArgumentException("This block's ID has already been defined");
        //    if (BlockLoader.NameIDToRuntimeIDTable.TryGetValue(id, out int existentID))
        //    {
        //        if (!BlockLoader.AcceptOverwrite) throw new ArgumentException("A block with the same ID (" + id + ") has already been defined! #" + existentID); //Else, this is going to be an overwriten block
        //        ReparseVersion[existentID] += 1; _mcb.reparse_version_cache = ReparseVersion[existentID];
        //        _customBlock.RuntimeID = existentID;
        //        BlockLoader.CapInjectedID--; // The ID reserved for this block can be used
        //    }
        //    else
        //    {
        //        ReparseVersion[_customBlock.RuntimeID] = 0; _mcb.reparse_version_cache = 0;
        //    }
        //    _customBlock.BlockID = id;
        //    _visible.m_ItemType = new ItemTypeInfo(ObjectTypes.Block, _customBlock.RuntimeID);
        //    return this;
        //}

        [Obsolete("Hex code is unnecessary, please supply only an ID")]
        public BlockPrefabBuilder SetBlockID(int id, string Net128HashHex)
        {
            return SetBlockID(id);
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
            if (rarity < BlockRarity.Common || rarity > BlockRarity.Rare)
            {
                Console.WriteLine(_customBlock.Name + " " + _customBlock.BlockID + " : Tried setting an invalid BlockRarity of " + (int)rarity + "!");
                rarity = BlockRarity.Common;
            }
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
        /// <param name="IgnoreFaults">Unused property</param>
        /// <returns></returns>
        public BlockPrefabBuilder SetSizeManual(IntVector3[] cells, bool IgnoreFaults = false)
        {
            ThrowIfFinished();
            var gravityScale = new float[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                gravityScale[i] = 1f;
            }
            TankBlock.filledCells = cells;
            TankBlock_FilledCellsGravityScaleFactors.SetValue(TankBlock, gravityScale);
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

        public BlockPrefabBuilder SetSizeFromStringMap(string[][] ZYXCells)
        {
            List<IntVector3> cells = new List<IntVector3>();
            for (int z = 0; z < ZYXCells.Length; z++)
            {
                string[] YXslice = ZYXCells[z];
                if (YXslice == null) continue;

                for (int y = 0, ry = YXslice.Length - 1; ry >= 0; y++, ry--)
                {
                    string Xline = YXslice[ry];
                    if (Xline == null) continue;

                    for (int x = 0; x < Xline.Length; x++)
                    {
                        char cell = Xline[x];
                        if (cell != ' ') cells.Add(new IntVector3(x, y, z));
                    }
                }
            }
            var gravityScale = new float[cells.Count];
            for (int i = 0; i < cells.Count; i++)
            {
                gravityScale[i] = 1f;
            }
            TankBlock_FilledCellsGravityScaleFactors.SetValue(TankBlock, gravityScale);
            TankBlock.filledCells = cells.ToArray();
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
            int xm = Math.Abs(size.x),
                ym = Math.Abs(size.y),
                zm = Math.Abs(size.z);
            for (int x = 0; x < xm; x++)
            {
                bool OnFace1 = x == 0 || x == xm - 1;
                for (int y = 0; y < ym; y++)
                {
                    bool OnFace2 = OnFace1 || (y == 0 || y == ym - 1);
                    for (int z = 0; z < zm; z++)
                    {
                        bool OnFace = OnFace2 || (z == 0 || z == zm - 1);
                        if (OnFace)
                            cells.Add(new Vector3(Math.Sign(size.x) * x, Math.Sign(size.y) * y, Math.Sign(size.z) * z));
                    }
                }
            }

            var gravityScale = new float[cells.Count];
            for (int i = 0; i < cells.Count; i++)
            {
                gravityScale[i] = 1f;
            }
            TankBlock_FilledCellsGravityScaleFactors.SetValue(TankBlock, gravityScale);
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
            TankBlock_FilledCellsGravityScaleFactors.SetValue(TankBlock, gravityScale);
            TankBlock.filledCells = cells.ToArray();
            TankBlock.attachPoints = aps.ToArray();
            return this;
        }

        public enum AttachmentPoints
        {
            Bottom,
            All,
        }

        public enum EmissionMode : byte
        {
            None,
            Active,
            ActiveAtNight,
            ActiveWhenAnchored
        }

        public BlockPrefabBuilder SetCustomEmissionMode(EmissionMode Mode)
        {
            ThrowIfFinished();
            _mcb.EmissionMode = Mode;
            return this;
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
            for (int i = 0; i < Prefab.transform.childCount; i++)
            {
                transform = Prefab.transform.GetChild(i);
                if (transform.name.Length < 5 && transform.name.EndsWith("col")) // "[a-z]col"
                    transform.localPosition = CenterOfMass;
            }
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
                model.AddComponent<MeshRenderer>().material = Material == null ? GameObjectJSON.MaterialFromShader(Color.white) : Material;
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

        public BlockPrefabBuilder SetDeathExplosionReference(int ReferenceID)
        {
            ThrowIfFinished();
            if (GameBlocksByID(ReferenceID, out GameObject refBlock))
                _moduleDamage.deathExplosion = refBlock.GetComponent<ModuleDamage>().deathExplosion;
            else
                Console.WriteLine($"Cound not find block '{ReferenceID}' for explosion effect");
            return this;
        }

        public BlockPrefabBuilder SetDeathExplosionReference(string ReferenceName)
        {
            ThrowIfFinished();
            if (GameBlocksByName(ReferenceName, out GameObject refBlock))
                _moduleDamage.deathExplosion = refBlock.GetComponent<ModuleDamage>().deathExplosion;
            else
                Console.WriteLine($"Cound not find block '{ReferenceName}' for explosion effect");
            return this;
        }

        CustomRecipe.RecipeInput[] _recipeQueue;
        string _recipeTable;

        public BlockPrefabBuilder SetRecipe(params ChunkTypes[] RecipeItems)
        {
            ThrowIfFinished();
            _recipeQueue = CustomRecipe.RecipeInput.FromChunkTypesArray(RecipeItems);
            return this;
        }

        public BlockPrefabBuilder SetRecipe(Dictionary<ChunkTypes, int> ItemCountRecipe)
        {
            ThrowIfFinished();
            _recipeQueue = new CustomRecipe.RecipeInput[ItemCountRecipe.Count];
            int iter = 0;
            foreach (var pair in ItemCountRecipe)
            {
                _recipeQueue[iter++] = new CustomRecipe.RecipeInput((int)pair.Key, pair.Value);
            }
            return this;
        }

        public BlockPrefabBuilder SetRecipe(params CustomRecipe.RecipeInput[] input)
        {
            ThrowIfFinished();
            _recipeQueue = input;
            return this;
        }

        public BlockPrefabBuilder SetCustomRecipeTable(string customRecipeTable)
        {
            ThrowIfFinished();
            _recipeTable = customRecipeTable;
            return this;
        }

        public BlockPrefabBuilder SetDropFromCrates(bool drop)
        {
            ThrowIfFinished();
            _customBlock.DropFromCrates = drop;
            return this;
        }

        public BlockPrefabBuilder SetPairedBlock(int block)
        {
            ThrowIfFinished();
            _customBlock.PairedBlock = block;
            return this;
        }


        private void OptimizeCellsForAP()
        {
            List<int> croppedCells = new List<int>();
            for (int i = 0; i < TankBlock.attachPoints.Length; i++) // Go through every AP on the block
            {
                IntVector3 ScaledAP = TankBlock.attachPoints[i] * 2f;
                IntVector3 FlooredAP = ScaledAP.PadHalfDown();
                IntVector3 RoofedAP = FlooredAP + ScaledAP.AxisUnit();
                for (int cell = 0; cell < TankBlock.filledCells.Length; cell++) // Find if the AP is on a cell. Start at 0, because cells near 255 can accidentally be pushed beyond that cap
                {
                    if (TankBlock.filledCells[cell] == FlooredAP ||
                        TankBlock.filledCells[cell] == RoofedAP)
                    {
                        if (!croppedCells.Contains(cell))
                            croppedCells.Add(cell); // Cell will cause deadlock, move to front
                        break;
                    }
                }
            }
            if (croppedCells.Count != 0) // If there are any cells that would cause a deadlock...
            {
                List<IntVector3> NewCellOrder = new List<IntVector3>(TankBlock.filledCells); // Turn cells to list for reorganizing
                croppedCells.Sort(); // Sort indexes by ascending to avoid corruption
                for (int i = croppedCells.Count - 1; i >= 0; i--) // Remove from top-down to avoid corruption
                {
                    //Console.WriteLine("Removing cell " + croppedCells[i] + " from block " + _customBlock.Name + " (" + NewCellOrder.Count + " cells)");
                    NewCellOrder.RemoveAt(croppedCells[i]);
                }
                List<IntVector3> InjectedCells = new List<IntVector3>(); // Make insert list
                foreach (var cell in croppedCells)
                {
                    InjectedCells.Add(TankBlock.filledCells[cell]);
                }
                NewCellOrder.InsertRange(0, InjectedCells);
                TankBlock.filledCells = NewCellOrder.ToArray();
            }
        }

        private void RegisterRecipe()
        {
            if (_recipeQueue != null)
            {
                CustomRecipe.RegisterRecipe(_recipeQueue, _customBlock.RuntimeID, _recipeTable ?? CustomRecipe.FabricatorFromFactionType(_customBlock.Faction));
            }
        }
    }
}