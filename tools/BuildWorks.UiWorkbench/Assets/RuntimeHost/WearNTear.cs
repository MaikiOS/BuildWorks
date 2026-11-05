using UnityEngine;

// Fixture scalars only. No native support/update/ownership behaviour is simulated.
public sealed class WearNTear : MonoBehaviour
{
    public bool m_supports = true, m_noSupportWear = true, m_forceCorrectCOMCalculation;
    public Vector3 m_comOffset;
    public float FixtureMaximum = 100, FixtureMinimum = 10, FixtureHorizontalLoss = .2f, FixtureVerticalLoss = .125f;
    private static int s_rayMask = ~0;
    private static Collider[] s_tempColliders = new Collider[128];
    private void GetMaterialProperties(out float maximum, out float minimum, out float horizontal, out float vertical)
    { maximum = FixtureMaximum; minimum = FixtureMinimum; horizontal = FixtureHorizontalLoss; vertical = FixtureVerticalLoss; }
}
