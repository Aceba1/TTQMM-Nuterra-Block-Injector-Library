using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using UnityEngine.Networking;

namespace Nuterra
{
    public static class NetHandler
    {
        private static NetworkInstanceId CurrentID;
        public static bool IsHost { get; private set; } = false;
        
        public static Action<NetworkInstanceId> OnClientJoined;
        public static Action<NetworkInstanceId> OnClientLeft;

        internal abstract class NetAction
        {
            public bool CanReceiveAsHost = false, CanReceiveAsClient = false;

            public abstract void OnClientReceive(NetworkMessage netMsg);
            public abstract void OnHostReceive(NetworkMessage netMsg);
        }

        internal class ActionWrapper<NetMessage> : NetAction where NetMessage : MessageBase, new()
        {
            public Action<NetMessage> ClientReceive;
            public Action<NetMessage> HostReceive;

            public override void OnClientReceive(NetworkMessage netMsg)
            {
                NetMessage reader = new NetMessage();
                netMsg.ReadMessage(reader);
                ClientReceive(reader);
            }

            public override void OnHostReceive(NetworkMessage netMsg)
            {
                NetMessage reader = new NetMessage();
                netMsg.ReadMessage(reader);
                HostReceive(reader);
            }
        }

        private static Dictionary<TTMsgType, NetAction> Subscriptions = new Dictionary<TTMsgType, NetAction>();

        public static void Subscribe<CustomMessageBase>(TTMsgType MessageID, Action<CustomMessageBase> AsClientReceiveMessage, Action<CustomMessageBase> AsHostReceiveMessage = null)
            where CustomMessageBase : MessageBase, new()
        {
            Subscriptions.Add(MessageID, new ActionWrapper<CustomMessageBase>()
            {
                CanReceiveAsClient = AsClientReceiveMessage != null,
                ClientReceive = AsClientReceiveMessage,
                CanReceiveAsHost = AsHostReceiveMessage != null,
                HostReceive = AsHostReceiveMessage,
            });
        }

        /* 
        public class WaterChangeMessage : MessageBase
        {
            public WaterChangeMessage() { }
            public WaterChangeMessage(float Height)
            {
                this.Height = Height;
            }
            public override void Deserialize(NetworkReader reader)
            {
                this.Height = reader.ReadSingle();
            }

            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(this.Height);
            }

            public float Height;
        } 
        */

        public static void BroadcastMessageToAll<NetMessage>(TTMsgType MessageID, NetMessage Message) where NetMessage : MessageBase
        {
            try
            {
                Singleton.Manager<ManNetwork>.inst.SendToAllClients(MessageID, Message, CurrentID);
            }
            catch (Exception E)
            {
                Console.WriteLine($"Could not send message {typeof(NetMessage).ToString()} to all clients : {E.Message}, \n{E.StackTrace}");
            }
        }

        public static void BroadcastMessageToAllExcept<NetMessage>(TTMsgType MessageID, NetMessage Message, bool SkipBroadcaster, int ClientConnectionToIgnore = -1) where NetMessage : MessageBase
        {
            try
            {
                Singleton.Manager<ManNetwork>.inst.SendToAllExceptClient(ClientConnectionToIgnore, MessageID, Message, CurrentID, SkipBroadcaster);
            }
            catch (Exception E)
            {
                Console.WriteLine($"Could not send message {typeof(NetMessage).ToString()} to specific clients : {E.Message}, \n{E.StackTrace}");
            }
        }

        public static void BroadcastMessageToClient<NetMessage>(TTMsgType MessageID, NetMessage Message, int ClientConnection) where NetMessage : MessageBase
        {
            try
            {
                Singleton.Manager<ManNetwork>.inst.SendToClient(ClientConnection, MessageID, Message, CurrentID);
            }
            catch (Exception E)
            {
                Console.WriteLine($"Could not send message {typeof(NetMessage).ToString()} to client on connection {ClientConnection} : {E.Message}, \n{E.StackTrace}");
            }
        }

        public static void BroadcastMessageToServer<NetMessage>(TTMsgType MessageID, NetMessage Message) where NetMessage : MessageBase
        {
            try
            {
                Singleton.Manager<ManNetwork>.inst.SendToServer(MessageID, Message, CurrentID);
            }
            catch (Exception E)
            {
                Console.WriteLine($"Could not send message {typeof(NetMessage).ToString()} to server : {E.Message}, \n{E.StackTrace}");
            }
        }

        public static class Patches
        {
            //[HarmonyPatch(typeof(ManLooseBlocks), "RegisterMessageHandlers")]
            //static class CreateWaterHooks
            //{
            //    static void Postfix
            //}

            [HarmonyPatch(typeof(NetPlayer), "OnRecycle")]
            static class OnRecycle
            {
                static void Postfix(NetPlayer __instance)
                {
                    if (__instance.isServer || __instance.isLocalPlayer)
                    {
                        IsHost = false;
                    }
                    OnClientLeft(__instance.netId);
                }
            }

            [HarmonyPatch(typeof(NetPlayer), "OnStartClient")]
            static class OnStartClient
            {
                static void Postfix(NetPlayer __instance)
                {
                    foreach(var item in Subscriptions)
                    {
                        if (item.Value.CanReceiveAsClient)
                        {
                            Singleton.Manager<ManNetwork>.inst.SubscribeToClientMessage(__instance.netId, item.Key, new ManNetwork.MessageHandler(item.Value.OnClientReceive));
                        }
                    }
                    OnClientJoined(__instance.netId);
                }
            }

            [HarmonyPatch(typeof(NetPlayer), "OnStartServer")]
            static class OnStartServer
            {
                static void Postfix(NetPlayer __instance)
                {
                    if (!IsHost)
                    {
                        foreach (var item in Subscriptions)
                        {
                            if (item.Value.CanReceiveAsHost)
                            {
                                Singleton.Manager<ManNetwork>.inst.SubscribeToServerMessage(__instance.netId, item.Key, new ManNetwork.MessageHandler(item.Value.OnHostReceive));
                            }
                        }
                        CurrentID = __instance.netId;
                        IsHost = true;
                    }
                }
            }
        }
    }
}
