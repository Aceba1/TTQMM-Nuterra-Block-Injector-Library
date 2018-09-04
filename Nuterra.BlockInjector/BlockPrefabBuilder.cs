using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

namespace Nuterra.BlockInjector
{
    public sealed class BlockPrefabBuilder
    {
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

            private void RunBlock()
            {
                BlockLoader.Register(customBlock);
                Singleton.DoOnceAfterStart(FinishBlock);
            }

            private void FinishBlock()
            {
                customBlock.Prefab.SetActive(false);
                BlockLoader.FixBlockUnlockTable(customBlock);
                UnityEngine.GameObject.Destroy(this.gameObject);
            }
        }

        private bool _finished = false;
        private TankBlock _block;
        private Visible _visible;
        private Damageable _damageable;
        private ModuleDamage _moduleDamage;
        private AutoSpriteRenderer _spriteRenderer;
        private GameObject _renderObject;
        private CustomBlock _customBlock;
        private UnityEngine.Networking.NetworkIdentity _netid;

        public BlockPrefabBuilder()
        {
            Initialize(new GameObject("newBlock"), true);
        }

        public BlockPrefabBuilder(GameObject prefab, bool MakeCopy = false)
        {
            if (MakeCopy)
                Initialize(GameObject.Instantiate(prefab), true);
            else
                Initialize(prefab, true);
        }

        public BlockPrefabBuilder(string PrefabFromResource, bool RemovePrefabRenderers = true)
        {
            GameObject original = null;
            var gos = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in gos)
            {
                if (go.name.StartsWith(PrefabFromResource))
                {
                    original = go;
                    break;
                }
            }
            if (original == null)
            {
                throw new Exception("Nothing starting with \"" + PrefabFromResource + "\" could be found. Decompile the resources asset to see what there is");
            }
            var copy = GameObject.Instantiate(original);
            Initialize(copy, false);
            if (RemovePrefabRenderers)
                RemoveChildrenWithComponent<MeshRenderer>();
        }

        private void Initialize(GameObject prefab, bool clearGridInfo)
        {
            _customBlock = new CustomBlock();
            _customBlock.Prefab = prefab;
            _customBlock.Prefab.SetActive(false);
            GameObject.DontDestroyOnLoad(_customBlock.Prefab);

            _customBlock.Prefab.tag = "TankBlock";
            _customBlock.Prefab.layer = Globals.inst.layerTank;

            _visible = _customBlock.Prefab.EnsureComponent<Visible>();
            _damageable = _customBlock.Prefab.EnsureComponent<Damageable>();
            _moduleDamage = _customBlock.Prefab.EnsureComponent<ModuleDamage>();
            _spriteRenderer = _customBlock.Prefab.EnsureComponent<AutoSpriteRenderer>();

            _block = _customBlock.Prefab.EnsureComponent<TankBlock>();
            
            _netid = _customBlock.Prefab.EnsureComponent<UnityEngine.Networking.NetworkIdentity>();
            BindingFlags b = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
            typeof(TankBlock).GetProperty("visible", b).SetValue(_block, _visible, null);
            typeof(Visible).GetField("m_VisibleComponent", b).SetValue(_visible, _block as Component);
            if (clearGridInfo)
            {
                _block.attachPoints = new Vector3[] { };
                _block.filledCells = new IntVector3[] { new Vector3(0, 0, 0) };
            }
        }
        public BlockPrefabBuilder RegisterLater(float Time = 5f)
        {
            ThrowIfFinished();
            _customBlock.Prefab.transform.position = Vector3.down * 1000f;
            new GameObject().AddComponent<RegisterTimer>().CallBlockPrefabBuilder(Time, this, _customBlock);
            _finished = true;
            return this;
        }

        public BlockPrefabBuilder RemoveChildrenWithComponent<T>(Transform SearchIn = null) where T : Component
        {
            Transform _search = _block.transform;
            if (SearchIn != null)
            {
                _search = SearchIn;
            }
            for(int i = _search.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = _search.transform.GetChild(i);
                if (child.GetComponent<T>() == null)
                {
                    RemoveChildrenWithComponent<T>(_search);
                }
                else
                {
                    GameObject.DestroyImmediate(child.gameObject);
                }
            }
            return this;
        }

        public BlockPrefabBuilder SetGrade(int Grade = 0)
        {
            _customBlock.Grade = Grade;
            return this;
        }

        public BlockPrefabBuilder SetWeight(float Weight)
        {
            _block.m_DefaultMass = Weight;
            return this;
        }

        public BlockPrefabBuilder SetHP(int HealthPoints)
        {
            _moduleDamage.maxHealth = HealthPoints;
            return this;
        }
        public TankBlock TankBlock
        {
            get => _block;
        }
        public BlockPrefabBuilder SetName(string blockName)
        {
            ThrowIfFinished();
            _customBlock.Name = blockName;
            _customBlock.Prefab.name = blockName;
            return this;
        }

        public BlockPrefabBuilder SetBlockID(int id, string Net128HashHex)
        {
            ThrowIfFinished();
            _customBlock.BlockID = id;
            _visible.m_ItemType = new ItemTypeInfo(ObjectTypes.Block, id);
            typeof(UnityEngine.Networking.NetworkIdentity).GetField("m_AssetId", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_netid, UnityEngine.Networking.NetworkHash128.Parse(Net128HashHex));
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

        public BlockPrefabBuilder SetCategory(BlockCategories category)
        {
            ThrowIfFinished();
            _customBlock.Category = category;
            typeof(TankBlock).GetField("m_BlockCategory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(_block, category);
            //_block.m_BlockCategory = category;
            return this;
        }

        public BlockPrefabBuilder SetSizeManual(IntVector3[] cells, Vector3[] aps, bool IgnoreFaults = false)
        {
            ThrowIfFinished();
            if (!IgnoreFaults)
            {
                foreach (IntVector3 cell in cells)
                {
                    if ((cell.x < 0f) || (cell.y < 0f) || (cell.z < 0f))
                    {
                        throw new Exception("There is a cell out of range! (" + cell.x.ToString() + ", " + cell.y.ToString() + ", " + cell.z.ToString() + ")\nMake sure that cells do not go below 0 in any axis. Check your APs as well");
                    }
                }
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
            _block.filledCells = cells;
            _block.attachPoints = aps;
            return this;
        }

        public BlockPrefabBuilder SetSize(Vector3I size, AttachmentPoints points = AttachmentPoints.Bottom)
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
            _block.filledCells = cells.ToArray();
            _block.attachPoints = aps.ToArray();
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
            _block.m_DefaultMass = mass;
            return this;
        }

        public BlockPrefabBuilder SetModel(Mesh Mesh, bool CreateBoxCollider, Material Material = null)
        {
            ThrowIfFinished();
            GameObject model = new GameObject("");
            model.SetActive(false);
            model.AddComponent<MeshFilter>().sharedMesh = Mesh;
            Material _Material = Material;
            if (_Material == null) _Material = GameObjectJSON.MaterialFromShader();
            model.AddComponent<MeshRenderer>().material = _Material;
            if (CreateBoxCollider)
            {
                Mesh.RecalculateBounds();
                var bc = model.AddComponent<BoxCollider>();
                bc.size = Mesh.bounds.size * 0.75f;
                bc.center = Mesh.bounds.center;
            }
            SetModel(model);
            return this;
        }

        public BlockPrefabBuilder SetModel(Mesh Mesh, Mesh ColliderMesh = null, bool ConvexCollider = true, Material Material = null)
        {
            ThrowIfFinished();
            GameObject model = new GameObject("");
            model.AddComponent<MeshFilter>().sharedMesh = Mesh;
            Material _Material = Material;
            if (_Material == null) _Material = GameObjectJSON.MaterialFromShader();
            model.AddComponent<MeshRenderer>().material = _Material;
            if (ColliderMesh != null)
            {
                var mc = model.AddComponent<MeshCollider>();
                mc.convex = ConvexCollider;
                mc.sharedMesh = ColliderMesh;
            }
            SetModel(model);
            return this;
        }

        public BlockPrefabBuilder SetModel(GameObject renderObject, bool MakeCopy = false)
        {
            ThrowIfFinished();
            if (_renderObject)
            {
                GameObject.DestroyImmediate(_renderObject);
            }
            if (MakeCopy)
                _renderObject = GameObject.Instantiate(renderObject);
            else
                _renderObject = renderObject;
            _renderObject.transform.parent = _customBlock.Prefab.transform;
            _renderObject.name = $"RenderObject";
            _renderObject.layer = Globals.inst.layerTank;

            foreach (GameObject child in _renderObject.EnumerateHierarchy(false, false))
            {
                child.layer = _renderObject.layer;
            }

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

        public BlockPrefabBuilder AddComponent<TBehaviour>() where TBehaviour : MonoBehaviour
        {
#warning TODO: Make extension method
            return AddComponent<TBehaviour>(null);
        }

        private void ThrowIfFinished()
        {
            if (_finished)
            {
                throw new InvalidOperationException("Build() already called");
            }
        }

        public CustomBlock Build()
        {
            _finished = true;
            return _customBlock;
        }
    }
}