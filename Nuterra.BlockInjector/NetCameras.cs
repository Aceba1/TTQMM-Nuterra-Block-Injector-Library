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
            if (Lookup == null) Lookup = new Dictionary<NetPlayer, NetCamera>();
            GameObject newbody = new GameObject("CameraObject");
            GameObject.DontDestroyOnLoad(newbody);
            NetCamera newcam = newbody.AddComponent<NetCamera>();
            newcam.HasBody = Player != ManNetwork.inst.MyPlayer;
            newcam.player = Player;
            if (newcam.HasBody)
            {
                if (MeshBody == null)
                {
                    MeshBody = GameObjectJSON.MeshFromData(Properties.Resources.mpcamdrone_body);
                    MeshBarrel = GameObjectJSON.MeshFromData(Properties.Resources.mpcamdrone_barrel);
                    DroneMat = GameObjectJSON.MaterialFromShader();
                    DroneMat.mainTexture = GameObjectJSON.ImageFromFile(Properties.Resources.mpcamdrone_tex);
                }
                
                newbody.AddComponent<MeshFilter>().sharedMesh = MeshBody;
                newcam.color = newbody.AddComponent<MeshRenderer>();
                newcam.color.sharedMaterial = DroneMat;
                var barrel = new GameObject("Barrel");
                newcam.T_Barrel = barrel.transform;
                newcam.T_Barrel.parent = newbody.transform;
                barrel.AddComponent<MeshFilter>().sharedMesh = MeshBarrel;
                barrel.AddComponent<MeshRenderer>().sharedMaterial = DroneMat;
                newbody.transform.position = Vector3.down * 1000f;
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
                    NetHandler.BroadcastMessageToAllExcept(MsgCamDrone, new CamDroneMessage() { position = Camera.current ? Camera.current.transform.position : Vector3.down * 1000f, rotation = Camera.current ? Camera.current.transform.rotation : Quaternion.identity } , true);
                    return;
                }
                NetHandler.BroadcastMessageToServer(MsgCamDrone, new CamDroneMessage() { position = Camera.current ? Camera.current.transform.position : Vector3.down * 1000f, rotation = Camera.current ? Camera.current.transform.rotation : Quaternion.identity });
                return;
            }
            color.material.SetColor("_Color", player.Colour);
        }

        internal void UpdateFromNet(CamDroneMessage msg)
        {
            var pastpos = transform.position;
            transform.position = msg.position;
            transform.rotation = Quaternion.Euler(0, msg.rotation.eulerAngles.y, 0) * Quaternion.LookRotation(pastpos - msg.position - Vector3.up * 5f, Vector3.forward);
            T_Barrel.rotation = msg.rotation;
        }

        public static void OnUpdateDrone(CamDroneMessage msg, NetworkMessage sender)
        {
            if (Lookup.TryGetValue(sender.GetSender(), out NetCamera cam))
                cam.UpdateFromNet(msg);
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
