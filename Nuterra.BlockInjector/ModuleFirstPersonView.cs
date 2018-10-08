using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Nuterra
{
    internal sealed class FirstPersonCamera : CameraManager.Camera
    {
        public static KeyCode key = KeyCode.R;

        public static FirstPersonCamera inst { get; private set; }

        private Vector3 _mouseStart = Vector3.zero;
        private bool _mouseDragging = false;
        private Quaternion _rotation;
        private Quaternion _rotationStart;

        public Tank Tank => Singleton.playerTank;

        private Tank _Tank;

        public ModuleFirstPerson Module;

        private bool GetModule()
        {
            _Tank = Tank;
            if (_Tank != null)
                Module = _Tank.GetComponentInChildren<ModuleFirstPerson>();
            return Module != null;
        }

        private void Awake()
        {
            inst = this;
            ManGameMode.inst.ModeSwitchEvent.Subscribe(OnGameModeChange);
        }

        private static void OnGameModeChange()
        {
            if (CameraManager.inst.IsCurrent<FirstPersonCamera>())
            {
                CameraManager.inst.Switch<TankCamera>();
            }
        }

        public override void Enable()
        {
            _rotation = Quaternion.identity;
        }

        private void Update()
        {
            if (Input.GetKeyDown(key))
            {
                if (CameraManager.inst.IsCurrent<FirstPersonCamera>())
                {
                    CameraManager.inst.Switch<TankCamera>();
                    return;
                }
                if (GetModule())
                {
                    CameraManager.inst.Switch<FirstPersonCamera>();
                }
            }

            if (!CameraManager.inst.IsCurrent<FirstPersonCamera>())
            {
                return;
            }

            if (!Tank) return;
            if (Tank != _Tank)
                GetModule();
            if (!Module) return;
            if (!Module.FirstPersonAnchor) return;

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
            var thing = Tank.GetComponent<ModuleFirstPerson>();
            Singleton.cameraTrans.position = thing.FirstPersonAnchor.transform.position;
            Singleton.cameraTrans.rotation = thing.transform.rotation * _rotation;
        }

        internal void BeginSpinControl()
        {
            _mouseDragging = true;
            _mouseStart = Input.mousePosition;
            _rotationStart = _rotation;
        }

        internal void EndSpinControl()
        {
            _mouseDragging = false;
        }
        internal sealed class ModuleFirstPerson : MonoBehaviour
        {
            private GameObject _anchor;

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
