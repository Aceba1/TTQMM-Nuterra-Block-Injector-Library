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
        public static NetworkInstanceId ThisNetID => ManNetwork.inst.MyPlayer.netId;
        internal abstract class NetAction
        {
            public bool CanReceiveAsHost = false, CanReceiveAsClient = false;

            public abstract void OnClientReceive(NetworkMessage netMsg);
            public abstract void OnHostReceive(NetworkMessage netMsg);
        }

        internal class ActionWrapper<NetMessage> : NetAction where NetMessage : MessageBase, new()
        {
            public Action<NetMessage, NetworkMessage> ClientReceive;
            public Action<NetMessage, NetworkMessage> HostReceive;

            public override void OnClientReceive(NetworkMessage netMsg)
            {
                try
                {
                    Console.WriteLine($"Received client message {netMsg.msgType}");
                    NetMessage reader = new NetMessage();
                    netMsg.ReadMessage(reader);
                    ClientReceive(reader, netMsg);
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
                    HostReceive(reader, netMsg);
                }
                catch (Exception E)
                {
                    Console.WriteLine($"Error on parsing client message for {typeof(NetMessage).FullName}: {E.Message}\n{E.StackTrace}");
                }
            }
        }

        private static Dictionary<TTMsgType, NetAction> Subscriptions = new Dictionary<TTMsgType, NetAction>();

        public static void Subscribe<CustomMessageBase>(TTMsgType MessageID, Action<CustomMessageBase, NetworkMessage> AsClientReceiveMessage, Action<CustomMessageBase, NetworkMessage> AsHostReceiveMessage = null)
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

        public static void BroadcastMessageToAll<NetMessage>(TTMsgType MessageID, NetMessage Message) where NetMessage : MessageBase
        {
            try
            {
                Singleton.Manager<ManNetwork>.inst.SendToAllClients(MessageID, Message, ThisNetID);
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
                Singleton.Manager<ManNetwork>.inst.SendToAllExceptClient(ClientConnectionToIgnore, MessageID, Message, ThisNetID, SkipBroadcaster);
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
                Singleton.Manager<ManNetwork>.inst.SendToClient(ClientConnection, MessageID, Message, ThisNetID);
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
                Singleton.Manager<ManNetwork>.inst.SendToServer(MessageID, Message, ThisNetID);
                Console.WriteLine($"Sent {MessageID} to server");
            }
            catch (Exception E)
            {
                Console.WriteLine($"Could not send message {typeof(NetMessage).ToString()} to server : {E.Message}, \n{E.StackTrace}");
            }
        }

        internal static class Patches
        {
            internal static void INIT()
            {
                var g = new UnityEngine.GameObject();
                UnityEngine.GameObject.DontDestroyOnLoad(g);
                ManNetwork.inst.OnPlayerAdded.Subscribe(g.AddComponent<UnityBugWorkaround>().ActivateMe);
            }

            internal class UnityBugWorkaround : UnityEngine.MonoBehaviour
            {
                public void ActivateMe(NetPlayer obj)
                {
                    gameObject.SetActive(true);
                }

                private void Update()
                {
                    if (ManNetwork.inst.MyPlayer != null)
                    {
                        PlayerAdded(ManNetwork.inst.MyPlayer);
                        gameObject.SetActive(false);
                    }
                }
            }

            private static void PlayerAdded(NetPlayer obj)
            {
                foreach (var item in Subscriptions)
                {
                    try
                    {
                        if (item.Value.CanReceiveAsClient)
                        {
                            Singleton.Manager<ManNetwork>.inst.SubscribeToClientMessage(obj.netId, item.Key, new ManNetwork.MessageHandler(item.Value.OnClientReceive));
                            Console.WriteLine($"Added client subscription {item.Value}");
                        }
                    }
                    catch (Exception E)
                    {
                        Console.WriteLine($"Exception on Client Subscription: {E.Message}\n{E.StackTrace}");
                    }
                }
                if (obj.IsHostPlayer || obj.isServer)
                {
                    foreach (var item in Subscriptions)
                    {
                        try
                        {
                            if (item.Value.CanReceiveAsHost)
                            {
                                Singleton.Manager<ManNetwork>.inst.SubscribeToServerMessage(item.Key, new ManNetwork.MessageHandler(item.Value.OnHostReceive));
                                Console.WriteLine($"Added server subscription {item.Value}");
                            }
                        }
                        catch (Exception E)
                        {
                            Console.WriteLine($"Exception on Server Subscription: {E.Message}\n{E.StackTrace}");
                        }
                    }
                }
                Console.WriteLine($"Player {obj.netId} has joined: Name-{obj.name}, Server-{obj.isServer}, Host-{obj.IsHostPlayer}, Local-{obj.isLocalPlayer}, Client-{obj.isClient}");
            }
        }
    }
}
