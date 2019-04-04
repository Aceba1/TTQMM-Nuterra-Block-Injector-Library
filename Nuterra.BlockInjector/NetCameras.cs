using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Nuterra.BlockInjector;
using UnityEngine.Networking;

namespace Nuterra
{
    class NetCamera : MonoBehaviour
    {
        static Dictionary<NetPlayer, NetCamera> Lookup;
        internal const TTMsgType MsgCamDrone = (TTMsgType)7342;
        public NetPlayer player { get; private set; }
        bool HasBody;
        internal static Mesh MeshBody, MeshBarrel;
        internal static Material DroneMat;
        MeshRenderer color;
        Transform T_Barrel;
        public static void CreateForPlayer(NetPlayer Player)
        {
            GameObject newbody = new GameObject("CameraObject");
            NetCamera newcam = newbody.AddComponent<NetCamera>();
            newcam.HasBody = Player != ManNetwork.inst.MyPlayer;
            if (newcam.HasBody)
            {
                if (MeshBody == null)
                {
                    MeshBody = GameObjectJSON.MeshFromData(Properties.Resources.mpcamdrone_body);
                    MeshBarrel = GameObjectJSON.MeshFromData(Properties.Resources.mpcamdrone_barrel);
                    DroneMat = GameObjectJSON.MaterialFromShader();
                    var tex = new Texture2D(2, 2);
                    tex.SetPixel(0, 0, Color.gray); tex.SetPixel(1, 0, Color.white); tex.SetPixel(0, 1, Color.black); tex.SetPixel(1, 1, Color.black);
                    tex.Apply();
                    DroneMat.mainTexture = tex;
                }
                
                newbody.AddComponent<MeshFilter>().sharedMesh = MeshBody;
                newcam.color = newbody.AddComponent<MeshRenderer>();
                newcam.color.sharedMaterial = DroneMat;
                var barrel = new GameObject("Barrel");
                newcam.T_Barrel = barrel.transform;
                newcam.T_Barrel.parent = newbody.transform;
                barrel.AddComponent<MeshFilter>().sharedMesh = MeshBarrel;
                barrel.AddComponent<MeshRenderer>().sharedMaterial = DroneMat;
            }
            else
            {
                newbody.transform.parent = Singleton.cameraTrans;
                newbody.transform.localPosition = Vector3.zero;
                newbody.transform.localRotation = Quaternion.identity;
            }
        }



        private void Update()
        {
            if (!player.isClient)
            {
                Lookup.Remove(player);
                GameObject.Destroy(gameObject);
                return;
            }
            if (!HasBody)
            {
                if (ManNetwork.IsHost)
                {
                    NetHandler.BroadcastMessageToAllExcept(MsgCamDrone, new CamDroneMessage() { position = transform.position, rotation = transform.rotation }, true);
                    return;
                }
                NetHandler.BroadcastMessageToServer(MsgCamDrone, new CamDroneMessage() { position = transform.position, rotation = transform.rotation });
                return;
            }
            color.material.SetColor("_Color", player.Colour);

        }

        internal void UpdateFromNet(CamDroneMessage msg)
        {
            transform.LookAt(msg.position - Vector3.up * 6f, Vector3.forward);
            transform.rotation *= Quaternion.Euler(0, msg.rotation.eulerAngles.y, 0);
            transform.position = msg.position;
            T_Barrel.localRotation = Quaternion.Euler(msg.rotation.eulerAngles.x, 0, 0);
        }

        public static void OnUpdateDrone(CamDroneMessage msg, NetworkMessage sender)
        {
            Lookup[sender.GetSender()].UpdateFromNet(msg);
        }

        internal static void OnServerUpdateDrone(CamDroneMessage msg, NetworkMessage sender)
        {
            NetHandler.BroadcastMessageToAllExcept(MsgCamDrone, msg, true, sender.conn.connectionId);
            OnUpdateDrone(msg, sender);
        }

        internal class CamDroneMessage : UnityEngine.Networking.MessageBase
        {
            public override void Deserialize(NetworkReader reader)
            {
                position = reader.ReadVector3();
                rotation = reader.ReadQuaternion();
            }
            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(position);
                writer.Write(rotation);
            }
            public Vector3 position;
            public Quaternion rotation;
        }
    }
}
