using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static partial class ExecutionGeometryBuilder
{
    private const string Generated = "Assets/RE4ChapterOneVillage/ExecutionArt";
    private const string BankPath = "Assets/Resources/Combat/ExecutionGeometry.asset";
    private const string LeonPath = "Assets/Models/角色/里昂/LeonR4R.fbx";
    private const string VillagerPath = "Assets/RE4ChapterOneAnimationLoop/Generated/MainGanadoMale.prefab";
    [MenuItem("工具/战斗/生成处决分件")]
    public static void Build()
    {
        GameObject leon = AssetDatabase.LoadAssetAtPath<GameObject>(LeonPath);
        GameObject male = AssetDatabase.LoadAssetAtPath<GameObject>(VillagerPath);
        Material interior = AssetDatabase.LoadAssetAtPath<Material>(Generated + "/Interior.mat");
        GameObject mark = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/BloodMark.prefab");
        if (leon == null || male == null || interior == null || mark == null)
        {
            throw new InvalidOperationException("处决模型、断面材质或血迹预制件缺失。");
        }

        var bank = AssetDatabase.LoadAssetAtPath<ExecutionGeometry>(BankPath);
        bool create = bank == null;
        if (create)
        {
            bank = ScriptableObject.CreateInstance<ExecutionGeometry>();
        }

        var generated = new List<Mesh>();
        try
        {
            var leather = AssetDatabase.LoadAssetAtPath<Material>(Generated + "/JacketEdge.mat");
            var lining = AssetDatabase.LoadAssetAtPath<Material>(Generated + "/JacketLining.mat");
            var cloth = AssetDatabase.LoadAssetAtPath<Material>(Generated + "/TornCloth.mat");
            if (leather == null || lining == null || cloth == null)
            {
                throw new InvalidOperationException("里昂的皮革翻边、夹克内衬或衣料材质缺失。");
            }
            var player = BuildSections(leon, Vector3.right, Vector3.zero, true, "Leon", generated, leather, lining, cloth, interior);
            var bones = male.GetComponentsInChildren<Transform>(true);
            Transform neck = Array.Find(bones, bone => bone.name == "Neck_1");
            Transform left = Array.Find(bones, bone => bone.name == "L_UpperArm");
            Transform right = Array.Find(bones, bone => bone.name == "R_UpperArm");
            Vector3 headCut = male.transform.InverseTransformPoint(neck.position) + Vector3.up * .018f;
            Vector3 armLeft = male.transform.InverseTransformPoint(left.position) + Vector3.left * .055f;
            Vector3 armRight = male.transform.InverseTransformPoint(right.position) + Vector3.right * .055f;
            var head = BuildSections(male, Vector3.up, headCut, false, "Head", generated);
            var leftArm = BuildSections(male, Vector3.left, armLeft, false, "LeftArm", generated);
            var rightArm = BuildSections(male, Vector3.right, armRight, false, "RightArm", generated);
            AppendVillagerCuts("MainGanadoFemale", generated, ref head, ref leftArm, ref rightArm);
            AppendVillagerCuts("MainGanadoArmed", generated, ref head, ref leftArm, ref rightArm);
            if (player.Length == 0 || head.Length == 0 || leftArm.Length == 0 || rightArm.Length == 0)
            {
                throw new InvalidOperationException("未生成完整的处决部位。");
            }

            var old = Array.Empty<Object>();
            if (create)
            {
                AssetDatabase.CreateAsset(bank, BankPath);
            }
            else
            {
                old = AssetDatabase.LoadAllAssetRepresentationsAtPath(BankPath);
            }

            foreach (Mesh mesh in generated)
            {
                AssetDatabase.AddObjectToAsset(mesh, bank);
            }

            bank.leon = player;
            bank.head = head;
            bank.leftArm = leftArm;
            bank.rightArm = rightArm;
            bank.interior = interior;
            bank.bloodMark = mark;
            EditorUtility.SetDirty(bank);
            foreach (Object previous in old)
            {
                if (previous is Mesh)
                {
                    Object.DestroyImmediate(previous, true);
                }
            }

            AssetDatabase.SaveAssetIfDirty(bank);
            AssetDatabase.ImportAsset(BankPath);
        }
        finally
        {
            foreach (Mesh mesh in generated)
            {
                if (mesh != null && !AssetDatabase.Contains(mesh))
                {
                    Object.DestroyImmediate(mesh);
                }
            }
        }
    }

    private static ExecutionGeometry.Section[] BuildSections(GameObject actor, Vector3 normal, Vector3 origin, bool animate, string label, List<Mesh> generated,
        Material leather = null, Material lining = null, Material cloth = null, Material interior = null, string cutJoint = null)
    {
        var sections = new List<ExecutionGeometry.Section>();
        foreach (var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!renderer.enabled && !animate)
            {
                continue;
            }

            var source = renderer.sharedMesh;
            var matrix = actor.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            var surface = TorsoSurface.Ordinary;
            if (animate)
            {
                surface = IdentifyTorsoSurface(renderer.name);
            }
            Vector3 cutOrigin = origin;
            if (cutJoint != null)
            {
                if (!TryGetVillagerCut(renderer, matrix, cutJoint, normal, out cutOrigin, out bool detachWhole))
                {
                    continue;
                }
                if (detachWhole)
                {
                    sections.Add(new ExecutionGeometry.Section { original = source, separated = source });
                    continue;
                }
            }
            var geometry = new CutSource(source, matrix, normal, cutOrigin, surface);
            if (!animate && geometry.maximum <= 0f)
            {
                continue;
            }

            var section = new ExecutionGeometry.Section
            {
                original = source
            };
            if (!animate && geometry.minimum >= 0f)
            {
                section.separated = source;
            }
            else
            {
                if (surface == TorsoSurface.Jacket)
                {
                    section.cutMaterials = new[] { leather, lining, interior };
                }
                else if (surface != TorsoSurface.Ordinary)
                {
                    section.cutMaterials = new[] { cloth, interior };
                }
                bool cap = CanCap(renderer.name, animate);
                section.retained = geometry.Make(false, animate, cap, label, generated);
                section.separated = geometry.Make(true, animate, cap, label, generated);
            }

            if (animate)
            {
                section.retainedJoints = WeightedJoints(section.retained);
                section.separatedJoints = WeightedJoints(section.separated);
            }
            sections.Add(section);
        }

        return sections.ToArray();
    }

    private static int[] WeightedJoints(Mesh mesh)
    {
        if (mesh == null)
        {
            return Array.Empty<int>();
        }
        var joints = new HashSet<int>();
        foreach (var weight in mesh.GetAllBoneWeights())
        {
            joints.Add(weight.boneIndex);
        }
        return joints.OrderBy(index => index).ToArray();
    }

    private static bool CanCap(string name, bool player)
    {
        if (player)
        {
            return name.StartsWith("24_jacket_") || name.StartsWith("24_shirts.part1") || name.StartsWith("24_pants_") || name.StartsWith("24_head_");
        }

        return name.EndsWith("__Body_Mat") || name.EndsWith("__Neck_Mat") || name.EndsWith("__Face_Mat") || name.EndsWith("__Jacket_Mat") || name.EndsWith("__R_Arm_Mat") || name.EndsWith("__L_Arm_Mat");
    }

    private struct Vertex
    {
        public Vector3 point;
        public Vector3 normal;
        public Vector3 tearOffset;
        public Vector4 tangent;
        public Vector2 uv;
        public Vector2 uv2;
        public Color color;
        public BoneWeight skin;
        internal static Vertex Blend(Vertex a, Vertex b, float t)
        {
            if (t <= .00001f)
            {
                return a;
            }

            if (t >= .99999f)
            {
                return b;
            }

            return new Vertex
            {
                point = Vector3.Lerp(a.point, b.point, t),
                normal = Vector3.Slerp(a.normal, b.normal, t),
                tearOffset = Vector3.Lerp(a.tearOffset, b.tearOffset, t),
                tangent = Vector4.Lerp(a.tangent, b.tangent, t),
                uv = Vector2.Lerp(a.uv, b.uv, t),
                uv2 = Vector2.Lerp(a.uv2, b.uv2, t),
                color = Color.Lerp(a.color, b.color, t),
                skin = MixSkin(a.skin, b.skin, t)
            };
        }

        private static BoneWeight MixSkin(BoneWeight a, BoneWeight b, float t)
        {
            var weights = new Dictionary<int, float>(8);
            void Add(int bone, float weight)
            {
                if (weight > 0f)
                {
                    weights.TryGetValue(bone, out float prior);
                    weights[bone] = prior + weight;
                }
            }

            void Collect(BoneWeight w, float amount)
            {
                Add(w.boneIndex0, w.weight0 * amount);
                Add(w.boneIndex1, w.weight1 * amount);
                Add(w.boneIndex2, w.weight2 * amount);
                Add(w.boneIndex3, w.weight3 * amount);
            }

            Collect(a, 1f - t);
            Collect(b, t);
            var values = weights.OrderByDescending(pair => pair.Value).Take(4).ToArray();
            var result = new BoneWeight();
            float sum = values.Sum(pair => pair.Value);
            if (values.Length > 0)
            {
                result.boneIndex0 = values[0].Key;
                result.weight0 = values[0].Value / sum;
            }

            if (values.Length > 1)
            {
                result.boneIndex1 = values[1].Key;
                result.weight1 = values[1].Value / sum;
            }

            if (values.Length > 2)
            {
                result.boneIndex2 = values[2].Key;
                result.weight2 = values[2].Value / sum;
            }

            if (values.Length > 3)
            {
                result.boneIndex3 = values[3].Key;
                result.weight3 = values[3].Value / sum;
            }

            return result;
        }
    }

    private sealed partial class CutSource
    {
        private readonly Mesh source;
        private readonly Matrix4x4 bodyMatrix;
        private readonly Vector3 axis;
        private readonly Vector3 up;
        private readonly Vertex[] vertices;
        private readonly float[] distance;
        public readonly float minimum;
        public readonly float maximum;
        private readonly TorsoSurface surface;
        public CutSource(Mesh mesh, Matrix4x4 matrix, Vector3 normal, Vector3 origin, TorsoSurface surface)
        {
            this.surface = surface;
            source = mesh;
            bodyMatrix = matrix;
            axis = normal;
            up = Vector3.up;
            if (Mathf.Abs(normal.y) > .9f)
            {
                up = Vector3.right;
            }

            var points = mesh.vertices;
            var normals = mesh.normals;
            var tangents = mesh.tangents;
            var uv = mesh.uv;
            var uv2 = mesh.uv2;
            var colors = mesh.colors;
            var skin = mesh.boneWeights;
            vertices = new Vertex[points.Length];
            distance = new float[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var v = new Vertex
                {
                    point = points[i],
                    normal = normals[i],
                    skin = skin[i],
                    color = Color.white
                };
                if (tangents.Length > i)
                {
                    v.tangent = tangents[i];
                }

                if (uv.Length > i)
                {
                    v.uv = uv[i];
                }

                if (uv2.Length > i)
                {
                    v.uv2 = uv2[i];
                }

                if (colors.Length > i)
                {
                    v.color = colors[i];
                }

                vertices[i] = v;
                distance[i] = Vector3.Dot(matrix.MultiplyPoint3x4(points[i]) - origin, normal);
            }

            minimum = distance.Min();
            maximum = distance.Max();
        }

        public Mesh Make(bool positive, bool animate, bool cap, string label, List<Mesh> generated)
        {
            int sign = -1;
            if (positive)
            {
                sign = 1;
            }

            var output = new List<Vertex>();
            var lookup = new Dictionary<(int, int), int>();
            var groups = new List<List<int>>();
            var segments = new List<(Vertex a, Vertex b)>();
            int Add(int a, int b)
            {
                if (a > b)
                {
                    (a, b) = (b, a);
                }

                if (lookup.TryGetValue((a, b), out int index))
                {
                    return index;
                }

                var v = vertices[a];
                if (a != b)
                {
                    v = Vertex.Blend(v, vertices[b], distance[a] / (distance[a] - distance[b]));
                }

                if (surface != TorsoSurface.Ordinary)
                {
                    v.tearOffset = ShellTear(v, sign);
                }
                index = output.Count;
                lookup.Add((a, b), index);
                output.Add(v);
                return index;
            }

            var poly = new int[4];
            for (int group = 0; group < source.subMeshCount; group++)
            {
                var indices = source.GetTriangles(group);
                var faces = new List<int>();
                groups.Add(faces);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    int count = 0;
                    int start = -1;
                    int finish = -1;
                    for (int j = 0; j < 3; j++)
                    {
                        int a = indices[i + j];
                        int b = indices[i + (j + 1) % 3];
                        bool keep = sign * distance[a] >= 0f;
                        if (keep)
                        {
                            poly[count++] = Add(a, a);
                        }

                        if (keep != (sign * distance[b] >= 0f))
                        {
                            int cross = Add(a, b);
                            poly[count++] = cross;
                            if (start < 0)
                            {
                                start = cross;
                            }
                            else
                            {
                                finish = cross;
                            }
                        }
                    }

                    for (int j = 2; j < count; j++)
                    {
                        faces.Add(poly[0]);
                        faces.Add(poly[j - 1]);
                        faces.Add(poly[j]);
                    }

                    if (cap && finish >= 0)
                    {
                        segments.Add((output[start], output[finish]));
                    }
                }
            }

            if (output.Count == 0)
            {
                return null;
            }

            if (segments.Count > 0 && surface != TorsoSurface.Ordinary)
            {
                AddLayeredTorso(segments, sign, output, groups);
            }
            else if (segments.Count > 0)
            {
                var interior = new List<int>();
                AddInterior(segments, sign, output, interior);
                if (interior.Count > 0)
                {
                    groups.Add(interior);
                }
            }

            string side = "A";
            if (positive)
            {
                side = "B";
            }

            var mesh = new Mesh
            {
                name = label + "_" + source.name + "_" + side,
                indexFormat = IndexFormat.UInt32
            };
            generated.Add(mesh);
            mesh.SetVertices(output.Select(v => v.point).ToList());
            mesh.SetNormals(output.Select(v => v.normal).ToList());
            mesh.SetTangents(output.Select(v => v.tangent).ToList());
            mesh.SetUVs(0, output.Select(v => v.uv).ToList());
            mesh.SetUVs(1, output.Select(v => v.uv2).ToList());
            mesh.SetColors(output.Select(v => v.color).ToList());
            mesh.bindposes = source.bindposes;
            mesh.boneWeights = output.Select(v => v.skin).ToArray();
            mesh.subMeshCount = groups.Count;
            for (int i = 0; i < groups.Count; i++)
            {
                mesh.SetTriangles(groups[i], i, false);
            }

            if (animate)
            {
                AddOpeningFrames(mesh, output, sign);
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        // 在切平面上逐行取外壳的前后交点，补出有厚度、能随骨骼变形的内侧表面。
        private void AddInterior(List<(Vertex a, Vertex b)> segments, int sign, List<Vertex> output, List<int> faces)
        {
            float Height(Vertex v) => Vector3.Dot(bodyMatrix.MultiplyPoint3x4(v.point), up);
            float low = segments.Min(s => Mathf.Min(Height(s.a), Height(s.b)));
            float high = segments.Max(s => Mathf.Max(Height(s.a), Height(s.b)));
            int rows = Mathf.Clamp(Mathf.CeilToInt((high - low) / .007f), 4, 240);
            int previous = -1;
            for (int row = 0; row <= rows; row++)
            {
                float y = Mathf.Lerp(low, high, Mathf.Clamp(row / (float)rows, .0001f, .9999f));
                float front = float.NegativeInfinity;
                float back = float.PositiveInfinity;
                Vertex first = default;
                Vertex last = default;
                foreach (var edge in segments)
                {
                    float a = Height(edge.a);
                    float b = Height(edge.b);
                    if ((a > y) == (b > y))
                    {
                        continue;
                    }

                    float t = (y - a) / (b - a);
                    float z = bodyMatrix.MultiplyPoint3x4(Vector3.Lerp(edge.a.point, edge.b.point, t)).z;
                    if (z < back)
                    {
                        back = z;
                        first = Vertex.Blend(edge.a, edge.b, t);
                    }

                    if (z > front)
                    {
                        front = z;
                        last = Vertex.Blend(edge.a, edge.b, t);
                    }
                }

                if (front <= back)
                {
                    previous = -1;
                    continue;
                }

                int current = output.Count;
                for (int x = 0; x < 5; x++)
                {
                    float u = x * .25f;
                    var v = Vertex.Blend(first, last, u);
                    float relief = Mathf.Sin(u * Mathf.PI) * (.0035f + .0015f * Mathf.Sin(y * 100f));
                    v.point += bodyMatrix.inverse.MultiplyVector(axis * (sign * relief));
                    v.normal = bodyMatrix.transpose.MultiplyVector(-axis * sign).normalized;
                    Vector3 tangent = bodyMatrix.inverse.MultiplyVector(Vector3.forward).normalized;
                    v.tangent = new Vector4(tangent.x, tangent.y, tangent.z, 1f);
                    v.uv = new Vector2(u, y * 3.5f);
                    v.uv2 = v.uv;
                    v.color = Color.white;
                    output.Add(v);
                }

                if (previous >= 0)
                {
                    for (int x = 0; x < 4; x++)
                    {
                        AddFace(previous + x, current + x, current + x + 1);
                        AddFace(previous + x, current + x + 1, previous + x + 1);
                    }
                }

                previous = current;
            }

            void AddFace(int a, int b, int c)
            {
                Vector3 cross = Vector3.Cross(output[b].point - output[a].point, output[c].point - output[a].point);
                if (Vector3.Dot(cross, output[a].normal) < 0f)
                {
                    (b, c) = (c, b);
                }

                faces.Add(a);
                faces.Add(b);
                faces.Add(c);
            }
        }

        private void AddOpeningFrames(Mesh mesh, List<Vertex> points, int sign)
        {
            // 锯齿从髋部向上推进，只有切线附近的衣物和断面需要局部张开。
            foreach (float frame in new[]
            {
                32f,
                62f,
                82f,
                100f
            }

            )
            {
                var delta = new Vector3[points.Count];
                float progress = frame * .01f;
                for (int i = 0; i < delta.Length; i++)
                {
                    Vector3 p = bodyMatrix.MultiplyPoint3x4(points[i].point);
                    float reach = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-.06f, .12f, Mathf.Lerp(.85f, 2.06f, progress) - p.y));
                    float lip = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Abs(p.x) / .12f);
                    float wide = Mathf.Lerp(.012f, .032f, progress) * reach * lip;
                    Vector3 offset = Vector3.right * (sign * wide);
                    delta[i] = bodyMatrix.inverse.MultiplyVector(offset) + points[i].tearOffset * (progress * reach);
                }

                mesh.AddBlendShapeFrame("SawOpening", frame, delta, null, null);
            }
        }
    }
}
