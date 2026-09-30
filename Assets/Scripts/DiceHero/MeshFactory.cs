using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    /// <summary>Procedural meshes used by the prototype.</summary>
    public static class MeshFactory
    {
        /// <summary>Unit cube (size 1, centred) with rounded edges of the given radius.</summary>
        public static Mesh RoundedCube(float radius = 0.14f, int edgeSegments = 4)
        {
            float h = 0.5f - radius;
            // Grid coordinates along one face axis: evenly spaced angles through each rounded band.
            var coords = new List<float>();
            for (int i = 0; i <= edgeSegments; i++)
            {
                float a = 45f * (1f - i / (float)edgeSegments) * Mathf.Deg2Rad;
                coords.Add(-h - radius * Mathf.Tan(a));
            }
            coords.Add(0f);
            for (int i = edgeSegments; i >= 0; i--)
            {
                float a = 45f * (1f - i / (float)edgeSegments) * Mathf.Deg2Rad;
                coords.Add(h + radius * Mathf.Tan(a));
            }

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            Vector3[] faceN = { Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            int n = coords.Count;
            foreach (var fn in faceN)
            {
                Vector3 u = Mathf.Abs(fn.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(fn, u);
                int start = verts.Count;
                for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = fn * 0.5f + u * coords[i] + v * coords[j];
                    Vector3 inner = new Vector3(Mathf.Clamp(p.x, -h, h), Mathf.Clamp(p.y, -h, h), Mathf.Clamp(p.z, -h, h));
                    Vector3 nrm = (p - inner).normalized;
                    verts.Add(inner + nrm * radius);
                    normals.Add(nrm);
                }
                for (int j = 0; j < n - 1; j++)
                for (int i = 0; i < n - 1; i++)
                {
                    int a = start + j * n + i, b = a + 1, c = a + n, d = c + 1;
                    AddQuad(tris, verts, fn, a, b, d, c);
                }
            }

            var mesh = new Mesh { name = "RoundedCube" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Adds a quad, choosing the winding so it faces along 'facing' (Unity front faces are clockwise).
        static void AddQuad(List<int> tris, List<Vector3> verts, Vector3 facing, int a, int b, int c, int d)
        {
            Vector3 cross = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (Vector3.Dot(cross, facing) >= 0f) { tris.AddRange(new[] { a, b, c, a, c, d }); }
            else { tris.AddRange(new[] { a, c, b, a, d, c }); }
        }

        /// <summary>Checkerboard of flat tiles (two submeshes for the two colours), top surface at y = 0.</summary>
        public static Mesh CheckerBoard(int sizeX, int sizeZ, float tile, float gap, float thickness)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var trisA = new List<int>();
            var trisB = new List<int>();
            float ox = -sizeX * tile * 0.5f, oz = -sizeZ * tile * 0.5f;
            for (int z = 0; z < sizeZ; z++)
            for (int x = 0; x < sizeX; x++)
            {
                var tris = ((x + z) & 1) == 0 ? trisA : trisB;
                Vector3 min = new Vector3(ox + x * tile + gap * 0.5f, -thickness, oz + z * tile + gap * 0.5f);
                Vector3 max = new Vector3(ox + (x + 1) * tile - gap * 0.5f, 0f, oz + (z + 1) * tile - gap * 0.5f);
                AddBox(verts, normals, tris, min, max, skipBottom: true);
            }
            var mesh = new Mesh { name = "Board", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(trisA, 0);
            mesh.SetTriangles(trisB, 1);
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddBox(List<Vector3> verts, List<Vector3> normals, List<int> tris, Vector3 min, Vector3 max, bool skipBottom)
        {
            Vector3 c = (min + max) * 0.5f, e = (max - min) * 0.5f;
            Vector3[] faceN = { Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            foreach (var fn in faceN)
            {
                if (skipBottom && fn == Vector3.down) continue;
                Vector3 u = Mathf.Abs(fn.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(fn, u);
                Vector3 fc = c + Vector3.Scale(fn, e);
                Vector3 ue = Vector3.Scale(u, e), ve = Vector3.Scale(v, e);
                int s = verts.Count;
                verts.Add(fc - ue - ve); verts.Add(fc + ue - ve); verts.Add(fc + ue + ve); verts.Add(fc - ue + ve);
                for (int k = 0; k < 4; k++) normals.Add(fn);
                AddQuad(tris, verts, fn, s, s + 1, s + 2, s + 3);
            }
        }
    }
}
