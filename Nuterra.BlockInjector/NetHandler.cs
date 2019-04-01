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
                try
                {
                    Console.WriteLine($"Received client message {netMsg.msgType}");
                    NetMessage reader = new NetMessage();
                    netMsg.ReadMessage(reader);
                    ClientReceive(reader);
                }
                catch (Exception E)
                {
                    Console.WriteLine($"Error on parsing client message for {typeof(NetMessage).FullName}: {E.Message}\n{E.StackTrace}");
                }

            }

            public override void OnHostReceive(NetworkMessage netMsg)
            {
                try
                {
                    Console.WriteLine($"Received server message {netMsg.msgType}");
                    NetMessage reader = new NetMessage();
                    netMsg.ReadMessage(reader);
                    HostReceive(reader);
                }
                catch (Exception E)
                {
                    Console.WriteLine($"Error on parsing client message for {typeof(NetMessage).FullName}: {E.Message}\n{E.StackTrace}");
                }
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
                Console.WriteLine($"Sent {MessageID} to all");
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
                Console.WriteLine($"Sent {MessageID} to all-except");
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
                Console.WriteLine($"Sent {MessageID} to client");
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
                Console.WriteLine($"Sent {MessageID} to server");
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

                static void Prefix(NetPlayer __instance)
                {
                    try
                    {
                        OnClientLeft?.Invoke(__instance.netId);
                        if (__instance.isServer || __instance.isLocalPlayer)
                        {
                            IsHost = false;
                        }
                    }
                    catch (Exception E)
                    {
                        Console.WriteLine($"Could not run Recycle patch! {E.Message}\n{E.StackTrace}");
                    }
                }
            }

            [HarmonyPatch(typeof(NetPlayer), "OnStartClient")]
            static class OnStartClient
            {
                static void Postfix(NetPlayer __instance)
                {
                    try
                    {
                        foreach (var item in Subscriptions)
                        {
                            try
                            {
                                if (item.Value.CanReceiveAsClient)
                                {
                                    Singleton.Manager<ManNetwork>.inst.SubscribeToClientMessage(__instance.netId, item.Key, new ManNetwork.MessageHandler(item.Value.OnClientReceive));
                                    Console.WriteLine($"Added client subscription {item.Value}");
                                }
                            }
                            catch (Exception E)
                            {
                                Console.WriteLine($"Exception on Client Subscription: {E.Message}\n{E.StackTrace}");
                            }
                        }
                        OnClientJoined?.Invoke(__instance.netId);
                    }
                    catch (Exception E)
                    {
                        Console.WriteLine($"Could not run Client patch! {E.Message}\n{E.StackTrace}");
                    }
                }
            }

            [HarmonyPatch(typeof(NetPlayer), "OnStartServer")]
            static class OnStartServer
            {
                static void Postfix(NetPlayer __instance)
                {
                    try
                    {
                        if (!IsHost)
                        {
                            CurrentID = __instance.netId;
                            IsHost = true;
                            foreach (var item in Subscriptions)
                            {
                                try
                                {
                                    if (item.Value.CanReceiveAsHost)
                                    {
                                        Singleton.Manager<ManNetwork>.inst.SubscribeToServerMessage(__instance.netId, item.Key, new ManNetwork.MessageHandler(item.Value.OnHostReceive));
                                        Console.WriteLine($"Added server subscription {item.Value}");
                                    }
                                }
                                catch (Exception E)
                                {
                                    Console.WriteLine($"Exception on Server Subscription: {E.Message}\n{E.StackTrace}");
                                }
                            }
                        }
                    }
                    catch (Exception E)
                    {
                        Console.WriteLine($"Could not run Server patch! {E.Message}\n{E.StackTrace}");
                    }
                }
            }
        }
    }
}
