using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Harmony;

/// <summary>
/// A simple class with two event hooks
/// </summary>
public class TechPhysicsReset : TechComponent
{
    public void Subscribe(Action preEvent, Action postEvent)
    {
        PreResetPhysicsEvent.Subscribe(preEvent);
        PostResetPhysicsEvent.Subscribe(postEvent);
    }
    public void Unsubscribe(Action preEvent, Action postEvent)
    {
        PreResetPhysicsEvent.Unsubscribe(preEvent);
        PostResetPhysicsEvent.Unsubscribe(postEvent);
    }

    public EventNoParams PreResetPhysicsEvent;
    public EventNoParams PostResetPhysicsEvent;

    [HarmonyPatch(typeof(Tank), "ResetPhysics")]
    private static class TankPhysicsHookPatch
    {
        private static void Prefix(Tank __instance)
        {
            __instance.GetComponent<TechPhysicsReset>().PreResetPhysicsEvent.Send();
        }
        private static void Postfix(Tank __instance)
        {
            __instance.GetComponent<TechPhysicsReset>().PostResetPhysicsEvent.Send();
        }
    }
}