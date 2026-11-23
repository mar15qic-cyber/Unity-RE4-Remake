using System;
using System.Collections.Generic;
using UnityEngine;

public static partial class ExecutionGeometryBuilder
{
    private enum TorsoSurface { Ordinary, Jacket, Shirt, Waist }

    private static TorsoSurface IdentifyTorsoSurface(string name)
    {
        if (name.StartsWith("24_jacket_", StringComparison.Ordinal))
        {
            return TorsoSurface.Jacket;
        }
        if (name.StartsWith("24_shirts.part1", StringComparison.Ordinal))
        {
            return TorsoSurface.Shirt;
        }
        if (name.StartsWith("24_pants_", StringComparison.Ordinal))
        {
            return TorsoSurface.Waist;
        }
        return TorsoSurface.Ordinary;
    }

    private sealed partial class CutSource
    {
        // 横轴是朝半身内部的距离，纵轴是从衣物外表面向内的距离，单位为米。
        private static readonly Vector2[] jacketProfile =
        {
            Vector2.zero, new Vector2(-.005f, -.004f), new Vector2(-.005f, .001f),
            new Vector2(.009f, .009f), new Vector2(.014f, .013f), new Vector2(-.003f, .019f),
            new Vector2(-.002f, .023f), new Vector2(.038f, .036f)
        };
        private static readonly Vector2[] clothProfile =
        {
            Vector2.zero, new Vector2(-.007f, .002f), new Vector2(-.006f, .006f), new Vector2(.016f, .014f)
        };

        // 两侧撕口各有独立的胸口、腰部翻角。所有坐标按原模型绑定姿势的米数计算。
        private static float Flap(float y, int side)
        {
            float waist = 1.075f + side * .028f;
            float chest = 1.355f - side * .041f;
            float first = Mathf.Clamp01(1f - Mathf.Abs(y - waist) / .087f);
            float second = Mathf.Clamp01(1f - Mathf.Abs(y - chest) / .11f);
            return Mathf.Max(first, second);
        }

        private Vector3 ShellTear(Vertex v, int side)
        {
            Vector3 p = bodyMatrix.MultiplyPoint3x4(v.point);
            float near = Mathf.Clamp01(1f - Mathf.Abs(p.x) / .075f);
            float flap = Flap(p.y, side);
            float ripple = Mathf.Sin(p.y * 54f + side * 1.8f + p.z * 11f);
            float offset = .007f + flap * .022f + ripple * .005f;
            if (surface != TorsoSurface.Jacket)
            {
                offset *= .38f;
            }
            return bodyMatrix.inverse.MultiplyVector(Vector3.right * (side * offset * near * near));
        }

        private void AddLayeredTorso(List<(Vertex a, Vertex b)> edges, int side, List<Vertex> output, List<List<int>> groups)
        {
            var leather = new List<int>();
            var fabric = new List<int>();
            var tissue = new List<int>();
            if (surface == TorsoSurface.Jacket)
            {
                groups.Add(leather);
            }
            groups.Add(fabric);
            groups.Add(tissue);

            // 从真实边界逐段翻折，继承该段的权重；尖角不会固定在世界坐标。
            foreach (var edge in edges)
            {
                AddGarmentEdge(edge.a, edge.b, side, output, leather, fabric);
            }
            AddBodyVolume(edges, side, output, tissue);
        }

        private void AddGarmentEdge(Vertex start, Vertex end, int side, List<Vertex> output, List<int> leather, List<int> fabric)
        {
            var profile = clothProfile;
            if (surface == TorsoSurface.Jacket)
            {
                profile = jacketProfile;
            }
            for (int band = 0; band + 1 < profile.Length; band++)
            {
                int at = output.Count;
                var a = EdgePoint(start, side, band, profile[band]);
                var b = EdgePoint(end, side, band, profile[band]);
                var c = EdgePoint(end, side, band + 1, profile[band + 1]);
                var d = EdgePoint(start, side, band + 1, profile[band + 1]);
                Vector3 normal = Vector3.Cross(b.point - a.point, d.point - a.point).normalized;
                Vector3 outward = bodyMatrix.transpose.MultiplyVector(-Vector3.right * side).normalized;
                if (Vector3.Dot(normal, outward) < 0f)
                {
                    normal = -normal;
                }
                a.normal = normal;
                b.normal = normal;
                c.normal = normal;
                d.normal = normal;
                output.Add(a);
                output.Add(b);
                output.Add(c);
                output.Add(d);
                var materialFaces = fabric;
                if (surface == TorsoSurface.Jacket && band < 3)
                {
                    materialFaces = leather;
                }
                AddOrientedQuad(output, materialFaces, at, at + 1, at + 2, at + 3, normal);
            }
        }

        private Vertex EdgePoint(Vertex v, int side, int ring, Vector2 profile)
        {
            Vector3 p = bodyMatrix.MultiplyPoint3x4(v.point);
            Vector3 outward = bodyMatrix.inverse.transpose.MultiplyVector(v.normal);
            outward.x = 0f;
            if (outward.sqrMagnitude < .00001f)
            {
                outward = Vector3.forward * Mathf.Sign(p.z);
            }
            outward.Normalize();
            float wave = Mathf.Sin(p.y * 66f + p.z * 12f + side * 2f);
            float flap = Flap(p.y, side);
            float across = profile.x;
            float depth = profile.y;
            if (surface == TorsoSurface.Jacket)
            {
                if (ring == 1 || ring == 2)
                {
                    v.tearOffset += bodyMatrix.inverse.MultiplyVector(
                        -Vector3.right * (side * (.005f + .020f * flap)) + outward * (flap * .016f));
                }
                if (ring == 5 || ring == 6)
                {
                    v.tearOffset -= bodyMatrix.inverse.MultiplyVector(Vector3.right *
                        (side * (.009f + .012f * (1f - flap) + .006f * wave)));
                }
            }
            v.point += bodyMatrix.inverse.MultiplyVector(Vector3.right * (side * across) - outward * depth);
            v.uv = new Vector2((p.y + p.z) * 8f, (across + depth) * 13f);
            v.uv2 = v.uv;
            return v;
        }

        // 截面是有正反面、侧壁和封端的独立体块。布边与体块由同一批交点插值蒙皮。
        private void AddBodyVolume(List<(Vertex a, Vertex b)> edges, int side, List<Vertex> output, List<int> faces)
        {
            const int samples = 13;
            const int ringSize = samples * 2;
            float low = float.PositiveInfinity;
            float high = float.NegativeInfinity;
            var heights = new List<Vector2>(edges.Count);
            foreach (var edge in edges)
            {
                float a = bodyMatrix.MultiplyPoint3x4(edge.a.point).y;
                float b = bodyMatrix.MultiplyPoint3x4(edge.b.point).y;
                heights.Add(new Vector2(a, b));
                low = Mathf.Min(low, a, b);
                high = Mathf.Max(high, a, b);
            }
            int rows = Mathf.Clamp(Mathf.CeilToInt((high - low) / .008f), 4, 180);
            int firstVertex = output.Count;
            int previous = -1;
            for (int row = 0; row <= rows; row++)
            {
                float y = Mathf.Lerp(low, high, Mathf.Clamp(row / (float)rows, .0002f, .9998f));
                float near = float.PositiveInfinity;
                float far = float.NegativeInfinity;
                Vertex first = default;
                Vertex last = default;
                for (int i = 0; i < edges.Count; i++)
                {
                    Vector2 h = heights[i];
                    if ((h.x > y) == (h.y > y))
                    {
                        continue;
                    }
                    var v = Vertex.Blend(edges[i].a, edges[i].b, (y - h.x) / (h.y - h.x));
                    float z = bodyMatrix.MultiplyPoint3x4(v.point).z;
                    if (z < near)
                    {
                        near = z;
                        first = v;
                    }
                    if (z > far)
                    {
                        far = z;
                        last = v;
                    }
                }
                if (far - near < .0005f)
                {
                    if (previous >= 0)
                    {
                        CloseVolumeRow(output, faces, previous, samples, Vector3.up);
                    }
                    previous = -1;
                    continue;
                }
                int current = output.Count;
                for (int k = 0; k < ringSize; k++)
                {
                    int index = k;
                    if (k >= samples)
                    {
                        index = ringSize - 1 - k;
                    }
                    float u = index / (float)(samples - 1);
                    float margin = Mathf.Min(.18f, .024f / (far - near));
                    if (surface == TorsoSurface.Shirt)
                    {
                        margin = Mathf.Min(.10f, .009f / (far - near));
                    }
                    float zFraction = Mathf.Lerp(margin, 1f - margin, u);
                    var v = Vertex.Blend(first, last, zFraction);
                    float arch = Mathf.Sin(u * Mathf.PI);
                    float ridge = Mathf.Sin(y * 48f + u * 10f + side) * .006f
                        + Mathf.Sin(y * 94f - u * 18f) * .0025f;
                    float inset = .024f + .019f * arch + ridge * arch + .006f * Flap(y, side);
                    if (surface == TorsoSurface.Shirt)
                    {
                        // 胸前夹克敞开，以衬衫轮廓包住可见躯干，避免露出对侧衣片。
                        inset = .010f + .016f * arch + ridge * arch;
                    }
                    else if (surface == TorsoSurface.Waist)
                    {
                        inset += .012f;
                    }
                    if (k >= samples)
                    {
                        inset += .019f + arch * .024f;
                    }
                    v.point += bodyMatrix.inverse.MultiplyVector(Vector3.right * (side * inset));
                    v.tearOffset = Vector3.zero;
                    v.normal = Vector3.zero;
                    Vector3 tangent = bodyMatrix.inverse.MultiplyVector(Vector3.forward).normalized;
                    v.tangent = new Vector4(tangent.x, tangent.y, tangent.z, 1f);
                    v.uv = new Vector2(zFraction * 1.24f + .042f * Mathf.Sin(y * 12f), y * 2.4f);
                    v.uv2 = v.uv;
                    output.Add(v);
                }
                if (previous < 0)
                {
                    CloseVolumeRow(output, faces, current, samples, Vector3.down);
                }
                else
                {
                    for (int k = 0; k < ringSize; k++)
                    {
                        int next = (k + 1) % ringSize;
                        Vector3 outward = Vector3.right * -side;
                        if (k == samples - 1)
                        {
                            outward = Vector3.forward;
                        }
                        else if (k == ringSize - 1)
                        {
                            outward = Vector3.back;
                        }
                        else if (k >= samples)
                        {
                            outward = -outward;
                        }
                        AddOrientedQuad(output, faces, previous + k, current + k, current + next, previous + next,
                            bodyMatrix.transpose.MultiplyVector(outward));
                    }
                }
                previous = current;
            }
            if (previous >= 0)
            {
                CloseVolumeRow(output, faces, previous, samples, Vector3.up);
            }
            var normals = new Vector3[output.Count - firstVertex];
            for (int t = 0; t < faces.Count; t += 3)
            {
                int a = faces[t];
                int b = faces[t + 1];
                int c = faces[t + 2];
                Vector3 area = Vector3.Cross(output[b].point - output[a].point, output[c].point - output[a].point);
                normals[a - firstVertex] += area;
                normals[b - firstVertex] += area;
                normals[c - firstVertex] += area;
            }
            for (int i = 0; i < normals.Length; i++)
            {
                var v = output[firstVertex + i];
                v.normal = normals[i].normalized;
                output[firstVertex + i] = v;
            }
        }

        private void CloseVolumeRow(List<Vertex> output, List<int> faces, int start, int count, Vector3 direction)
        {
            Vector3 normal = bodyMatrix.transpose.MultiplyVector(direction);
            for (int i = 0; i < count - 1; i++)
            {
                int back = start + 2 * count - 1 - i;
                AddOrientedQuad(output, faces, start + i, start + i + 1, back - 1, back, normal);
            }
        }

        private static void AddOrientedQuad(List<Vertex> output, List<int> faces, int a, int b, int c, int d, Vector3 normal)
        {
            if (Vector3.Dot(Vector3.Cross(output[b].point - output[a].point, output[d].point - output[a].point), normal) < 0f)
            {
                (b, d) = (d, b);
            }
            faces.Add(a);
            faces.Add(b);
            faces.Add(c);
            faces.Add(a);
            faces.Add(c);
            faces.Add(d);
        }
    }
}
