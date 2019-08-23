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
        private const float FOV = 75f;
        private float originalFOV = 0f;
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
        }

        public void DisableFPVState()
        {
            IsActive = false;
            if (camera)
            {
                Singleton.cameraTrans.parent = null;
                camera.fieldOfView = originalFOV;
                camera = null;
            }
            Console.WriteLine("Camera: Switched to TankCamera");
            TankCamera.inst.FreezeCamera(false);
            TankCamera.inst.Enable();
            CurrentModule = -1;
        }

        public void EnableFPVState()
        {
            IsActive = true;
            Awake();
            camera = Camera.current;
            if (originalFOV == 0f)
            {
                originalFOV = camera.fieldOfView;
            }
            camera.fieldOfView = FOV;
            TankCamera.inst.FreezeCamera(true);
            if (CurrentModule <= -2)
            {
                CurrentModule = Module.Count - 1;
            }
            Singleton.cameraTrans.parent = Module[CurrentModule].transform;
            Singleton.cameraTrans.localPosition = Vector3.zero;
            Console.WriteLine("Camera: Switched to FirstPersonCamera " + CurrentModule.ToString());
        }

        private void Update()
        {
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
                    if (IsActive && CurrentModule >= Module.Count || CurrentModule == -1)
                    {
                        DisableFPVState();
                        return;
                    }
                    if (flag)
                    {
                        EnableFPVState();
                    }
                    else if (IsActive)
                    {
                        Console.WriteLine("Could not find camera module");
                        DisableFPVState();
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
                UpdateLocalRotation();
                UpdateCamera();
            }
            catch
            {
                Console.WriteLine("Something bad happened while updating the fpv camera!");
                DisableFPVState();
            }
        }

        private void UpdateLocalRotation()
        {
            if (_mouseDragging)
            {
                Vector3 mouseDelta = Input.mousePosition - _mouseStart;

                mouseDelta = mouseDelta / Screen.width;
                float changeAroundY = mouseDelta.x * 300f * Globals.inst.m_RuntimeCameraSpinSensHorizontal;
                float changeAroundX = mouseDelta.y * 300f * Globals.inst.m_RuntimeCameraSpinSensVertical;

                changeAroundY += _rotationStart.eulerAngles.y;
                changeAroundX += _rotationStart.eulerAngles.x;

                if (changeAroundX > 180f)
                {
                    changeAroundX -= 360f;
                }

                float before = changeAroundX;
                changeAroundX = Mathf.Clamp(changeAroundX, -80, 80);
                Quaternion newRotation = Quaternion.Euler(changeAroundX, changeAroundY, 0);
                _rotation = newRotation;
            }
        }

        private void UpdateCamera()
        {
            var module = Module[CurrentModule];
            Singleton.cameraTrans.parent = module.transform;
            Singleton.cameraTrans.localPosition = Vector3.zero;
            Singleton.cameraTrans.rotation = (module.AdaptToMainRot ? Tank.control.FirstController.block.transform.rotation : module.transform.rotation) * _rotation;
        }

        internal void BeginSpinControl()
        {
            _mouseStart = Input.mousePosition;
            _rotationStart = _rotation;
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
