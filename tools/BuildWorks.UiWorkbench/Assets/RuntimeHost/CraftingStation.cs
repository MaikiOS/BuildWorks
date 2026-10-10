using UnityEngine;
public class CraftingStation : MonoBehaviour
{
    public bool m_upgrader;
    public string m_name = "$workbench";
    public Sprite m_icon;
    public static bool Available;
    public static bool HaveBuildStationInRange(string name, Vector3 position) => Available;
}
