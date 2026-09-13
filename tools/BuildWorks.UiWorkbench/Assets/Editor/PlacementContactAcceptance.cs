using System;
using System.Collections.Generic;
using OstrixMods.BuildWorks;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class PlacementContactAcceptance
{
    internal static string Run()
    {
        var owned = new List<GameObject>();
        GameObject Cube(string name, Vector3 position, Vector3 scale)
        {
            GameObject result = GameObject.CreatePrimitive(PrimitiveType.Cube);
            result.name = name;
            result.layer = 0;
            result.transform.SetPositionAndRotation(position, Quaternion.identity);
            result.transform.localScale = scale;
            owned.Add(result);
            return result;
        }
        bool Contact(GameObject ghost, PlacementContactSides sides, out RaycastHit hit,
            Vector3? eye = null, float maximumDistance = 50f)
        {
            Physics.SyncTransforms();
            Require(BuildWorksPlacementValidation.TryGetBounds(ghost, out Bounds bounds), "Missing scaled bounds");
            return BuildWorksPlacementValidation.TryFindContact(ghost, bounds, 1,
                eye ?? new Vector3(0, 3, -4), maximumDistance, sides, out hit);
        }
        try
        {
            // This fixture occupies a private distant area; unrelated Workbench
            // geometry must never serve as support for a missing-contact test.
            Vector3 origin = new Vector3(2000, 0, 2000);
            GameObject ghost = Cube("ContactGhost", origin + Vector3.up * .5f, Vector3.one);
            Vector3 eye = origin + new Vector3(0, 3, -4);
            GameObject floor = Cube("ContactFloor", origin - Vector3.up * .5f, new Vector3(8,1,8));
            Require(Contact(ghost, PlacementContactSides.Below, out RaycastHit hit, eye) &&
                hit.collider.gameObject == floor && Vector3.Dot(hit.normal, Vector3.up) > .999f &&
                Mathf.Abs(hit.point.y) < .0001f, "Floor contact/normal is not under the frozen part");
            Require(!Contact(ghost, PlacementContactSides.Below, out _, origin + Vector3.up * 100, 5),
                "Contact bypasses native eye distance");
            ghost.transform.position = origin + Vector3.up * 3;
            Require(!Contact(ghost, PlacementContactSides.Below, out _, eye),
                "Unsupported part guesses the distant ground as contact");

            ghost.transform.position = origin + Vector3.up * .5f;
            GameObject ceiling = Cube("ContactCeiling", origin + Vector3.up * 1.5f, new Vector3(8,1,8));
            Require(Contact(ghost, PlacementContactSides.Above, out hit, eye) &&
                hit.collider.gameObject == ceiling && Vector3.Dot(hit.normal, Vector3.down) > .999f,
                "Ceiling contact does not retain the actual underside normal");
            Require(Contact(ghost, PlacementContactSides.NonFloor, out hit, eye) &&
                hit.collider.gameObject == ceiling, "Native notOnFloor incorrectly excludes a ceiling");
            ceiling.SetActive(false);
            GameObject wall = Cube("ContactWall", origin + new Vector3(1, .5f, 0), new Vector3(1,1,8));
            Require(Contact(ghost, PlacementContactSides.NonFloor, out hit, eye) &&
                hit.collider.gameObject == wall && Mathf.Abs(hit.normal.y) < .0001f,
                "Wall-only placement resolves to a floor/ceiling instead");
            wall.SetActive(false);
            floor.transform.position += Vector3.up * .2f;
            Require(!Contact(ghost, PlacementContactSides.Below, out _, eye),
                "A surface deep inside the bounds was accepted as a boundary contact");
            floor.transform.position -= Vector3.up * .2f;

            floor.AddComponent<Rigidbody>().isKinematic = true;
            Require(!Contact(ghost, PlacementContactSides.Below, out _, eye),
                "A rigidbody was accepted as native static support");
            Object.DestroyImmediate(floor.GetComponent<Rigidbody>());
            floor.SetActive(false);
            Require(!Contact(ghost, PlacementContactSides.All, out _, eye),
                "The ghost's own collider was accepted as support");
            floor.SetActive(true);

            ghost.transform.localScale = Vector3.one * .01f;
            ghost.transform.position = origin + Vector3.up * .005f;
            Require(Contact(ghost, PlacementContactSides.Below, out hit, eye) &&
                hit.collider.gameObject == floor, "One-percent furniture lost its contact");
            Require(BuildWorksPlacementValidation.TryGetBounds(ghost, out Bounds tiny) &&
                Mathf.Abs(tiny.size.y - .01f) < .00001f, "One-percent bounds were clamped to full size");
            ghost.transform.position += Vector3.up * .01f;
            Require(!Contact(ghost, PlacementContactSides.Below, out _, eye),
                "One-percent contact tolerance reaches an unrelated lower surface");

            ghost.transform.localScale = new Vector3(2, .5f, 1);
            ghost.transform.SetPositionAndRotation(origin + Vector3.up, Quaternion.Euler(0,90,0));
            ghost.SetActive(false);
            Require(BuildWorksPlacementValidation.TryGetBounds(ghost, out Bounds inactive) &&
                Vector3.Distance(inactive.size, new Vector3(1,.5f,2)) < .001f &&
                Vector3.Distance(inactive.center, ghost.transform.position) < .001f,
                "Inactive retry ghost bounds ignore its current rotation/base scale");
            ghost.SetActive(true);

            // Keep an actual sloping surface normal; the native notOnTiltingSurface
            // branch will decide whether that normal is legal for a particular Piece.
            floor.transform.rotation = Quaternion.Euler(0,0,40);
            floor.transform.position = origin - Vector3.up * .5f;
            ghost.transform.localScale = Vector3.one;
            ghost.transform.SetPositionAndRotation(origin + Vector3.up * (.5f / Mathf.Cos(40 * Mathf.Deg2Rad)),
                Quaternion.identity);
            Require(Contact(ghost, PlacementContactSides.Below, out hit, eye) &&
                hit.normal.y > .7f && hit.normal.y < .8f,
                "Sloping support normal was replaced with Vector3.up");
            return "Actual Physics contact: floor/ceiling/wall normals; no distant support; native eye range; " +
                "rigidbody and ghost exclusions; 1% scale; inactive rotated bounds; slope normal passed. " +
                "Host Piece flags, water-volume queries, station checks and costs require the native integration contract separately.";
        }
        finally
        {
            foreach (GameObject item in owned)
                if (item) { item.SetActive(false); Object.DestroyImmediate(item); }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Placement contact: " + message);
    }
}
