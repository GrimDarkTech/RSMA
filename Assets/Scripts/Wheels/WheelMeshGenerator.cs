using System;
using UnityEditor;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class WheelMeshGenerator : MonoBehaviour
{
    [Header("Main Settings")]
    [Min(0.0001f)] public float mainRadius = 0.5f;
    [Min(0.0001f)] public float mainWidth = 0.2f;

    [Header("Outer (Chamfer) Settings")]
    [Tooltip("Radius of the outer flat part")]
    [Min(0.0001f)] public float outerRadius = 0.45f;
    [Tooltip("Full width of the outer flat part")]
    [Min(0.0001f)] public float outerWidth = 0.15f;

    [Header("Resolution")]
    [Range(3, 128)] public int segments = 32;

    private bool isGenerated = false;

    [ContextMenu("Generate Wheel Mesh")]
    public void GenerateMesh()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        Mesh mesh = new Mesh();
        mesh.name = "ProceduralWheel";

        // Подготовка данных
        int verticesPerRing = segments;
        // Нам нужно 4 кольца: 2 внешних (узких) и 2 внутренних (широких для основной части)
        // Итого: 4 кольца + 2 центральные точки для торцов
        Vector3[] vertices = new Vector3[verticesPerRing * 4 + 2];
        int[] triangles = new int[segments * 6 * 3 + segments * 2 * 3]; // Бока (3 секции) + Торцы

        float halfMainWidth = mainWidth / 2f;
        float halfOuterWidth = outerWidth / 2f;


        for (int i = 0; i < segments; i++)
        {
            float angle = i * 2 * Mathf.PI / segments;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            // Умножаем каждую компоненту на соответствующий scale
            // В данном случае: X - ширина, Y и Z - радиус (высота и глубина)

            // 0: Левое внешнее кольцо
            vertices[i] = new Vector3(-halfOuterWidth, cos * outerRadius, sin * outerRadius);

            // 1: Левое основное кольцо
            vertices[i + segments] = new Vector3(-halfMainWidth, cos * mainRadius, sin * mainRadius);

            // 2: Правое основное кольцо
            vertices[i + segments * 2] = new Vector3(halfMainWidth, cos * mainRadius, sin * mainRadius);

            // 3: Правое внешнее кольцо
            vertices[i + segments * 3] = new Vector3(halfOuterWidth, cos * outerRadius, sin * outerRadius);
        }

        // Центры для торцов (последние два индекса)
        int leftCenterIndex = vertices.Length - 2;
        int rightCenterIndex = vertices.Length - 1;
        vertices[leftCenterIndex] = new Vector3(-halfOuterWidth, 0, 0);
        vertices[rightCenterIndex] = new Vector3(halfOuterWidth, 0, 0);

        // Генерация треугольников
        int tri = 0;
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;

            // Секция 1: Левая фаска (между внешним 0 и основным 1)
            tri = AddQuad(triangles, tri, i, next, next + segments, i + segments);

            // Секция 2: Центральный цилиндр (между 1 и 2)
            tri = AddQuad(triangles, tri, i + segments, next + segments, next + segments * 2, i + segments * 2);

            // Секция 3: Правая фаска (между основным 2 и внешним 3)
            tri = AddQuad(triangles, tri, i + segments * 2, next + segments * 2, next + segments * 3, i + segments * 3);

            // Торцы (Cap)
            // Левый
            triangles[tri++] = leftCenterIndex;
            triangles[tri++] = i;
            triangles[tri++] = next;

            // Правый
            triangles[tri++] = rightCenterIndex;
            triangles[tri++] = next + segments * 3;
            triangles[tri++] = i + segments * 3;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.mesh = mesh;

        // Обновляем MeshCollider если он есть
        MeshCollider collider = GetComponent<MeshCollider>();
        if (collider != null)
        {
            collider.sharedMesh = null;
            collider.sharedMesh = mesh;
        }

        isGenerated = true;
    }

    private int AddQuad(int[] tris, int ti, int v00, int v10, int v11, int v01)
    {
        tris[ti] = v00;
        tris[ti + 1] = v10;
        tris[ti + 2] = v11;
        tris[ti + 3] = v00;
        tris[ti + 4] = v11;
        tris[ti + 5] = v01;
        return ti + 6;
    }

    private void Start()
    {
        if (!isGenerated) 
        {
            GenerateMesh();
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(WheelMeshGenerator))]
public class WheelMeshEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        WheelMeshGenerator gen = (WheelMeshGenerator)target;

        if (GUILayout.Button("Generate / Update Mesh"))
        {
            gen.GenerateMesh();
        }
    }
}
#endif