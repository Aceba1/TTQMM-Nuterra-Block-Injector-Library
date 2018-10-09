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
            FirstPersonCamera.inst.IsActive = false;
            FirstPersonCamera.inst.CurrentModule = -1;
            if (FirstPersonCamera.inst.camera != null)
            {
                TankCamera.inst.enabled = true;
            }
        }

        public void Awake()
        {
            _rotation = Quaternion.identity;
        }

        private void Update()
        {
            if (Input.GetKeyDown(key))
            {
                bool flag = GetModule();
                CurrentModule += Input.GetKey(KeyCode.LeftShift) ? -1 : 1;
                if (IsActive && CurrentModule >= Module.Count || CurrentModule == -1)
                {
                    IsActive = false;
                    Console.WriteLine("Camera: Switched to TankCamera");
                    TankCamera.inst.enabled = true;
                    CurrentModule = -1;
                    return;
                }
                if (flag)
                {
                    IsActive = true;
                    Awake();
                    camera = Camera.main;
                    TankCamera.inst.enabled = false;
                    if (CurrentModule <= -2)
                    {
                        CurrentModule = Module.Count - 1;
                    }
                    Console.WriteLine("Camera: Switched to FirstPersonCamera " + CurrentModule.ToString());
                }
                else
                {
                    Console.WriteLine("Could not find camera module");
                    TankCamera.inst.enabled = true;
                    CurrentModule = -1;
                }
            }

            if (!IsActive)
                return;

            if (Module.Count != 0 || !Module[CurrentModule] || Tank != _Tank || Module[CurrentModule].thisBlock != _Tank)
            {
                IsActive = GetModule();
                if (!IsActive)
                {
                    CurrentModule = -1;
                    TankCamera.inst.enabled = true;
                    return;
                }
            }

            if (_mouseStartDragging)
                BeginSpinControl();

            UpdateLocalRotation();
            UpdateCamera();
        }

        private void UpdateLocalRotation()
        {
            if (_mouseDragging)
            {
                Vector3 mouseDelta = Input.mousePosition - _mouseStart;

                mouseDelta = mouseDelta / Screen.width;
                float changeAroundY = mouseDelta.x * TankCamera.inst.spinSensitivity * 400f * Globals.inst.m_CurrentCamSpinSensHorizontal;
                float changeAroundX = mouseDelta.y * TankCamera.inst.spinSensitivity * 400f * Globals.inst.m_CurrentCamSpinSensVertical;

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
            Singleton.cameraTrans.position = module.FirstPersonAnchor.transform.position;
            Singleton.cameraTrans.rotation = (module.AdaptToMainRot ? Tank.control.FirstController.block.transform.rotation : module.transform.rotation) * _rotation;
        }

        internal void BeginSpinControl()
        {
            _mouseStart = Input.mousePosition;
            _rotationStart = _rotation;
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
}
