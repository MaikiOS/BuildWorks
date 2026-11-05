using System;

namespace ReservationChecks
{
    internal static class ContainerCallbackChecks
    {
        private sealed class OtherContainer : Container { internal OtherContainer() : base(2) { } }
        private sealed class OtherWear : WearNTear { }
        internal static void Run()
        {
            int checks = 0, invoked = 0;
            void Check(bool value) { checks++; if (!value) throw new Exception("Container callback guard failed."); }
            var container = new Container(2);
            var wear = container.NativeCallbacksForTest();
            Check(BlueprintConstructionContainerGuard.CallbacksOnly(container, wear));
            wear.m_onDestroyed = null;
            Check(!BlueprintConstructionContainerGuard.CallbacksOnly(container, wear));
            wear = container.NativeCallbacksForTest(); wear.m_onDestroyed += () => invoked++;
            Check(!BlueprintConstructionContainerGuard.CallbacksOnly(container, wear));
            var other = new Container(2);
            Check(!BlueprintConstructionContainerGuard.CallbacksOnly(container, other.NativeCallbacksForTest()));
            wear = container.NativeCallbacksForTest(); container.Inventory.m_onChanged = null;
            Check(!BlueprintConstructionContainerGuard.CallbacksOnly(container, wear));
            wear = container.NativeCallbacksForTest(); container.Inventory.m_onChanged += () => invoked++;
            Check(!BlueprintConstructionContainerGuard.CallbacksOnly(container, wear));
            wear = container.NativeCallbacksForTest(); container.m_rootObjectOverride = new ZNetView();
            Check(!BlueprintConstructionContainerGuard.CallbacksOnly(container, wear));
            container.m_rootObjectOverride = null;
            other.NativeCallbacksForTest(); container.Inventory.m_onChanged = other.Inventory.m_onChanged;
            Check(!BlueprintConstructionContainerGuard.CallbacksOnly(container, wear));
            wear = container.NativeCallbacksForTest();
            Check(!BlueprintConstructionContainerGuard.CallbacksOnly(container, new OtherWear { m_onDestroyed = wear.m_onDestroyed }));
            var derived = new OtherContainer();
            Check(!BlueprintConstructionContainerGuard.CallbacksOnly(derived, derived.NativeCallbacksForTest()));
            Check(BlueprintConstructionContainerGuard.CallbacksOnly(container, container.NativeCallbacksForTest()));
            wear = container.NativeCallbacksForTest();
            Check(BlueprintConstructionContainerGuard.ViewsMatch(container, wear, container.View));
            Check(BlueprintConstructionContainerGuard.ViewsMatch(null, wear, container.View));
            Check(!BlueprintConstructionContainerGuard.ViewsMatch(container, wear, other.View));
            Check(!BlueprintConstructionContainerGuard.ViewsMatch(container, new WearNTear(other.View), container.View));
            container.ReplaceView();
            Check(!BlueprintConstructionContainerGuard.ViewsMatch(container, wear, container.View));
            Check(invoked == 0);
            Console.WriteLine("PASS " + checks + " actual container callback predicates: exact single native method/target, override/subclass/foreign/extra refusal; host substituted, no callback invoked, no empty/removal permission proved.");
        }
    }
}
