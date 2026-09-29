using System.Collections.Generic;
using UnityEngine;

// Bounded world-space stains: spray settles into the floor, underneath corpses.
public class BloodEffects : MonoBehaviour
{
    static readonly Queue<BloodEffects> stains = new Queue<BloodEffects>();
    const int Limit = 80;
    public static int Count => stains.Count;
    Mesh mesh;
    Material material;
    Vector3[] centers, velocities, vertices;
    float[] sizes;
    Color[] colors;
    float age;
    bool lethal;
    int fragments;
    public int FragmentCount => fragments;
    public static void Clear()
    {
        while (stains.Count > 0) { var stain = stains.Dequeue(); if (stain != null) { stain.gameObject.SetActive(false); Destroy(stain.gameObject); } }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRegistry() => stains.Clear();
    public static void Spawn(Vector3 position, Vector2 direction, bool killed, bool heavy = false)
    {
        while (stains.Count >= Limit) { var old = stains.Dequeue(); if (old != null) { old.gameObject.SetActive(false); Destroy(old.gameObject); } }
        var stain = new GameObject(killed ? "Blood - fatal spray and pool" : "Blood - impact spray").AddComponent<BloodEffects>();
        stain.transform.position = position; stain.Build(direction, killed, heavy); stains.Enqueue(stain);
    }
    void Build(Vector2 direction, bool killed, bool heavy)
    {
        lethal = killed;
        fragments = killed ? (heavy ? 16 : 9) : 0;
        int count = killed ? (heavy ? 112 : 86) : 32;
        centers = new Vector3[count]; velocities = new Vector3[count]; sizes = new float[count];
        vertices = new Vector3[count * 8]; colors = new Color[count * 8]; var triangles = new int[count * 18];
        for (int i = 0; i < count; i++)
        {
            bool pool = killed && i < 18;
            bool chunk = i >= count - fragments;
            centers[i] = pool ? (Vector3)Random.insideUnitCircle * 0.7f : Vector3.zero;
            velocities[i] = pool ? Vector3.zero : (Vector3)(Quaternion.Euler(0, 0, Random.Range(-85f, 85f)) * direction) * Random.Range(2f, heavy ? 13 : killed ? 9 : 5);
            sizes[i] = pool ? Random.Range(0.23f, 0.48f) : chunk ? Random.Range(0.07f, 0.15f) : Random.Range(0.025f, 0.09f);
            Color color = new Color(Random.Range(0.24f, 0.55f), 0.005f, Random.Range(0.012f, 0.035f), 0.95f);
            if (chunk) color = i % 4 == 0 ? new Color(0.75f, 0.57f, 0.40f) : new Color(0.65f, 0.065f, 0.095f);
            for (int k = 0; k < 8; k++) colors[i * 8 + k] = color;
            int v = i * 8, t = i * 18;
            for (int k = 0; k < 6; k++) { triangles[t + k * 3] = v; triangles[t + k * 3 + 1] = v + k + 1; triangles[t + k * 3 + 2] = v + k + 2; }
        }
        mesh = new Mesh { name = "Pixel blood stain" }; mesh.MarkDynamic();
        mesh.vertices = vertices; mesh.colors = colors; mesh.triangles = triangles;
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = gameObject.AddComponent<MeshRenderer>(); renderer.sortingOrder = -4;
        material = new Material(Shader.Find("Sprites/Default")) { mainTexture = Texture2D.whiteTexture };
        renderer.sharedMaterial = material; Draw();
    }
    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.04f); age += dt;
        for (int i = 0; i < centers.Length; i++)
        {
            Vector3 next = centers[i] + velocities[i] * dt;
            if (CombatBallistics.WallBetween(transform.position + centers[i], transform.position + next)) velocities[i] = Vector3.zero;
            else centers[i] = next;
            velocities[i] *= Mathf.Exp(-5 * dt);
        }
        Draw(); if (age > 1.1f) enabled = false;
    }
    void Draw()
    {
        for (int i = 0; i < centers.Length; i++)
        {
            float s = sizes[i] * (lethal && i < 18 ? Mathf.Lerp(0.3f, 1, Mathf.Clamp01(age * 3)) : 1);
            Vector3 p = centers[i]; int v = i * 8;
            if (i >= centers.Length - fragments)
                p.y += Mathf.Abs(Mathf.Sin(Mathf.Clamp01(age / 0.75f) * Mathf.PI * 2)) * Mathf.Max(0, 1 - age / 0.75f) * 0.7f;
            for (int k = 0; k < 8; k++) { float a = k * Mathf.PI / 4; vertices[v + k] = p + new Vector3(Mathf.Cos(a) * s, Mathf.Sin(a) * s * 0.7f); }
        }
        mesh.vertices = vertices; mesh.RecalculateBounds();
    }
    void OnDestroy() { if (mesh != null) Destroy(mesh); if (material != null) Destroy(material); }
}
