using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Nuterra.BlockInjector
{
    internal sealed class FirstPersonCamera : Module
    {
        public static KeyCode key = KeyCode.R;

        public static FirstPersonCamera inst { get; private set; }
        
        private bool IsActive;
        private Camera camera;
        const float SMOOTH = 25f;
        float Smooth => Mathf.Clamp01(SMOOTH * Time.deltaTime);
        const float MIN = 0.1f;
        private float originalMIN = -1f;
        private Vector3 _mouseStart = Vector3.zero;
        private bool _mouseDragging => Input.GetMouseButton(1);
        private bool _mouseStartDragging => Input.GetMouseButtonDown(1);
        private Quaternion _rotation;
        private Quaternion _rotationStart;

        public Tank Tank => Singleton.playerTank;

        private Tank _Tank;

        public int CurrentModule = -1;

        public List<ModuleFirstPerson> Module = new List<ModuleFirstPerson>();

        private bool GetModule()
        {
            _Tank = Tank;
            if (_Tank != null)
                _Tank.GetComponentsInChildren<ModuleFirstPerson>(false, Module);
            return Module.Count != 0 && Module.Count > CurrentModule;
        }

        public void Manual_Awake()
        {
            inst = this;
            ManGameMode.inst.ModeSwitchEvent.Subscribe(OnGameModeChange);
        }

        private static void OnGameModeChange()
        {
            if (inst.camera != null)
            {
                inst.DisableFPVState();
            }
        }

        public void Awake()
        {
            _rotation = Quaternion.identity;
            changeAroundX = 0f;
            changeAroundY = 0f;
            CreateLineMaterial();
        }

        public void DisableFPVState()
        {
            IsActive = false;
            if (camera)
            {
                Singleton.cameraTrans.parent = null;
                //camera.fieldOfView = originalFOV;
                camera.nearClipPlane = originalMIN;
                camera = null;
            }
            //Console.WriteLine("Camera: Switched to TankCamera");
            TankCamera.inst.FreezeCamera(false);
            TankCamera.inst.Enable();
            TankCamera.inst.SetCameraShake(.5f, 1f, 2f);
            CurrentModule = -1;
        }

        public void EnableFPVState()
        {
            IsActive = true;
            Awake();
            camera = Singleton.camera;
            if (originalMIN == -1f)
            {
                //originalFOV = camera.fieldOfView;
                originalMIN = camera.nearClipPlane;
            }
            //camera.fieldOfView = FOV;
            camera.nearClipPlane = MIN;
            TankCamera.inst.FreezeCamera(true);
            if (CurrentModule <= -2)
            {
                CurrentModule = Module.Count - 1;
            }
            Singleton.cameraTrans.parent = Module[CurrentModule].transform;
            Singleton.cameraTrans.localPosition = Vector3.zero;
            //Console.WriteLine("Camera: Switched to FirstPersonCamera " + CurrentModule.ToString());
        }

        private void Update()
        {
            try
            {
                if (ManGameMode.inst.LockPlayerControls)
                {
                    if (IsActive)
                        DisableFPVState();
                    return;
                }
                if (Input.GetKeyDown(key))
                {
                    if (Input.GetKey(KeyCode.LeftControl))
                    {
                        Awake();
                    }
                    else
                    {
                        bool flag = GetModule();
                        CurrentModule += Input.GetKey(KeyCode.LeftShift) ? -1 : 1;
                        CurrentModule = ((CurrentModule + 1) % (Module.Count + 1)) - 1;
                        if (CurrentModule == -1)
                        {
                            if (IsActive)
                                DisableFPVState();
                            return;
                        }
                        if (flag)
                        {
                            EnableFPVState();
                        }
                        else if (IsActive)
                        {
                            //Console.WriteLine("Could not find camera module");
                            DisableFPVState();
                        }
                        else
                        {
                            CurrentModule = -1;
                        }
                    }
                }

                if (!IsActive)
                    return;

                if (Module.Count != 0 || !Module[CurrentModule] || Tank != _Tank || Module[CurrentModule].thisBlock != _Tank)
                {
                    IsActive = GetModule();
                    if (!IsActive)
                    {
                        DisableFPVState();
                        return;
                    }
                }

                if (_mouseStartDragging)
                    BeginSpinControl();

                try
                {
                    EnsureCameraState();
                    UpdateLocalRotation();
                    UpdateCamera();
                }
                catch (Exception E)
                {
                    Console.WriteLine("Something bad happened while updating the FPV camera!");
                    Console.WriteLine(E);
                    DisableFPVState();
                }
            }
            catch (Exception E)
            {
                Console.WriteLine("Unknown error in FPV script: " + E);
            }
        }

        private void EnsureCameraState()
        {
            //camera.fieldOfView = FOV;
            camera.nearClipPlane = MIN;
            var DOF = Singleton.Manager<CameraManager>.inst.DOF;
            if (DOF != null)
            {
                var settings = DOF.settings;
                settings.focusDistance = 2f;
                settings.aperture = 2f;
                settings.focalLength = 2f;
                // Why, 2, you may ask? Because Unity documents aren't of help and I have no idea what any of this means. That is why.
                DOF.settings = settings;
            }
            TankCamera.inst.FreezeCamera(true);
        }

        float changeAroundX, changeAroundY;
        float m_changeAroundX, m_changeAroundY;

        private void UpdateLocalRotation()
        {
            if (_mouseDragging)
            {
                Vector3 mouseDelta = Input.mousePosition - _mouseStart;

                mouseDelta = mouseDelta / Screen.width;
                m_changeAroundY = mouseDelta.x * 200f * Globals.inst.m_RuntimeCameraSpinSensHorizontal;
                m_changeAroundX = mouseDelta.y * 200f * Globals.inst.m_RuntimeCameraSpinSensVertical;

                m_changeAroundY += _rotationStart.eulerAngles.y;
                m_changeAroundX += _rotationStart.eulerAngles.x;

                if (m_changeAroundX > 180f)
                {
                    m_changeAroundX -= 360f;
                }

                m_changeAroundX = Mathf.Clamp(m_changeAroundX, -80, 80);
            }
        }

        private void UpdateCamera()
        {
            var module = Module[CurrentModule];
            float diffY = m_changeAroundX - changeAroundX;
            float smooth = Smooth;
            changeAroundX += diffY * smooth;
            float diffX = (m_changeAroundY - changeAroundY + 180) % 360 - 180;
            changeAroundY += diffX * smooth;
            _rotation = Quaternion.Euler(changeAroundX, changeAroundY, 0);
            Singleton.cameraTrans.parent = module.transform;
            Singleton.cameraTrans.localPosition = Vector3.zero;
            Singleton.cameraTrans.rotation = (module.AdaptToMainRot ? Tank.control.FirstController.block.transform.rotation : module.transform.rotation) * _rotation;
            TankCamera.inst.FreezeCamera(true);
        }

        internal void BeginSpinControl()
        {
            _mouseStart = Input.mousePosition;
            _rotationStart = Quaternion.Euler(m_changeAroundX, m_changeAroundY, 0);
        }

        static void DrawBox(float xMin, float xMax, float yMin, float yMax)
        {
            GL.Vertex3(xMin, yMin, 0); // bottom left
            GL.Vertex3(xMax, yMin, 0); // bottom right
            GL.Vertex3(xMax, yMax, 0); // top right

            GL.Vertex3(xMax, yMax, 0); // top right
            GL.Vertex3(xMin, yMin, 0); // bottom left
            GL.Vertex3(xMin, yMax, 0); // top left
        }
        static void DrawTri(float xA, float yA, float xB, float yB, float xC, float yC)
        {
            GL.Vertex3(xA, yA, 0);
            GL.Vertex3(xB, yB, 0);
            GL.Vertex3(xC, yC, 0);
        }

        public float arrowWidthRatio = 0.012f;
        public float arrowLengthRatio = 0.02f;

        static Material lineMaterial;
        static void CreateLineMaterial()
        {
            if (!lineMaterial)
            {
                // Unity has a built-in shader that is useful for drawing
                // simple colored things.
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                lineMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                // Turn on alpha blending
                lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                // Turn backface culling off
                lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                // Turn off depth writes
                lineMaterial.SetInt("_ZWrite", 0);
            }
        }

        public void OnRenderObject()
        {
            if (!IsActive) return;

            // Apply the line material
            lineMaterial.SetPass(0);

            GL.PushMatrix();
            GL.LoadPixelMatrix();

            float fov = Camera.main.fieldOfView;
            float X = Screen.width, Y = Screen.height,
                arrowWidth = arrowWidthRatio * Y, arrowLength = arrowLengthRatio * Y, arrowLength2 = arrowLength + arrowLength,
                centerX = (((changeAroundY + 180) % 360) - 180) / fov * -Y + 0.5f * X, centerY = (changeAroundX / fov + 0.5f) * Y;

            // Draw lines
            GL.Begin(GL.TRIANGLES);
            GL.Color(new Color(0f, 0f, 0f, 0.4f));

            //Bottom arrow
            DrawTri(centerX, arrowLength2,
                centerX + arrowWidth, arrowLength,
                centerX - arrowWidth, arrowLength);
            DrawBox(centerX - arrowWidth, centerX + arrowWidth,
                0, arrowLength);

            //Top Arrow
            DrawTri(centerX, Y - arrowLength2,
                centerX - arrowWidth, Y - arrowLength,
                centerX + arrowWidth, Y - arrowLength);
            DrawBox(centerX - arrowWidth, centerX + arrowWidth,
                Y - arrowLength, Y);

            //Left Arrow
            DrawTri(arrowLength2, centerY,
                arrowLength, centerY + arrowWidth,
                arrowLength, centerY - arrowWidth);
            DrawBox(0, arrowLength,
                centerY - arrowWidth, centerY + arrowWidth);

            //Right Arrow
            DrawTri(X - arrowLength2, centerY,
                X - arrowLength, centerY - arrowWidth,
                X - arrowLength, centerY + arrowWidth);
            DrawBox(X, X - arrowLength,
                centerY - arrowWidth, centerY + arrowWidth);

            GL.End();

            GL.PopMatrix();
        }
    }

    public class ModuleFirstPerson : MonoBehaviour
    {
        private GameObject _anchor;
        public bool AdaptToMainRot = false;
        public TankBlock thisBlock;

        public void Awake()
        {
            thisBlock = gameObject.GetComponent<TankBlock>();
        }

        public GameObject FirstPersonAnchor
        {
            get
            {
                if (!_anchor)
                {
                    Transform[] ts = transform.GetComponentsInChildren<Transform>();
                    foreach (Transform t in ts)
                    {
                        if (t.gameObject.name == "FirstPersonAnchor")
                        {
                            _anchor = t.gameObject;
                            return _anchor;
                        }
                    }
                    _anchor = null;
                }
                return _anchor;
            }
        }
    }
}
