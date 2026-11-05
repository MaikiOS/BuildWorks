using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal static class BlueprintConstructionContainerGuard
    {
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo RootOverride = typeof(Container).GetField("m_rootObjectOverride", InstanceFields);
        private static readonly FieldInfo Changed = typeof(Inventory).GetField("m_onChanged", InstanceFields);
        private static readonly MethodInfo DestroyedMethod = typeof(Container).GetMethod("OnDestroyed", InstanceFields, null, Type.EmptyTypes, null);
        private static readonly MethodInfo ChangedMethod = typeof(Container).GetMethod("OnContainerChanged", InstanceFields, null, Type.EmptyTypes, null);

        internal static bool CallbacksOnly(Container container, WearNTear wear)
        {
            if (!container || container.GetType() != typeof(Container) || !wear || wear.GetType() != typeof(WearNTear) ||
                RootOverride == null || RootOverride.DeclaringType != typeof(Container) || RootOverride.FieldType != typeof(ZNetView) ||
                !ReferenceEquals(RootOverride.GetValue(container), null) || Changed == null ||
                Changed.DeclaringType != typeof(Inventory) || Changed.FieldType != typeof(Action)) return false;
            var inventory = container.GetInventory();
            return inventory != null && inventory.GetType() == typeof(Inventory) &&
                One(wear.m_onDestroyed, container, DestroyedMethod) && One(Changed.GetValue(inventory) as Delegate, container, ChangedMethod);
        }

        private static bool One(Delegate callbacks, object target, MethodInfo method)
        {
            var list = callbacks?.GetInvocationList();
            return method != null && method.DeclaringType == typeof(Container) && !method.IsStatic && method.ReturnType == typeof(void) &&
                list?.Length == 1 && ReferenceEquals(list[0].Target, target) && list[0].Method == method;
        }

        internal static bool ViewsMatch(Container container, WearNTear wear, ZNetView view) => wear && view &&
            ReferenceEquals(typeof(WearNTear).GetField("m_nview", InstanceFields)?.GetValue(wear), view) &&
            (ReferenceEquals(container, null) || container && ReferenceEquals(typeof(Container).GetField("m_nview", InstanceFields)?.GetValue(container), view));

        internal static bool NativeRoot(GameObject instance, WearNTear wear)
        {
            if (!instance || !wear || wear.gameObject != instance ||
                !ViewsMatch(instance.GetComponent<Container>(), wear, instance.GetComponent<ZNetView>()) ||
                !CallbacksOnly(instance.GetComponent<Container>(), wear)) return false;
            var components = instance.GetComponentsInChildren<MonoBehaviour>(true);
            // Unknown behaviours may run OnDestroy even when the inventory is empty.
            return components.Length == 4 && components.All(component => component && component.gameObject == instance &&
                (component.GetType() == typeof(Container) || component.GetType() == typeof(Piece) ||
                 component.GetType() == typeof(WearNTear) || component.GetType() == typeof(ZNetView)));
        }
    }
}
