using System;
using System.Collections.Generic;
using System.Linq;
using Unity_StarRail_CRP_Sample.MathUtils;
using UnityEngine;

namespace Unity_StarRail_CRP_Sample.Editor
{
    public enum WriteChannel
    {
        Normal,
        Tangent,
        UV3
    }

    [ExecuteAlways]
    public class CharacterNormalSmoothTool : MonoBehaviour
    {
        public WriteChannel writeChannel = WriteChannel.Tangent;

        [NonSerialized]
        public bool rebuild = false;

        public void Start()
        {
            rebuild = true;
            SmoothNormals();
        }

        private void SmoothNormals()
        {
            var skinnedMeshes = GetComponentsInChildren<SkinnedMeshRenderer>();
            SmoothNormals(skinnedMeshes, writeChannel, ref rebuild);
        }

        private void SmoothNormals(SkinnedMeshRenderer[] skinnedMeshes, 
            WriteChannel channel, ref bool reBuild)
        {
            if (skinnedMeshes != null)
            {
                if (!reBuild)
                {
                    return;
                }

                SmoothNormals(skinnedMeshes, channel);

                reBuild = false;
            }
        }
        
        public static void SmoothNormals(SkinnedMeshRenderer[] skinnedMeshes, WriteChannel channel)
        {
            Debug.Log("Smoothing normals");

            for (int i = 0; i < skinnedMeshes.Length; i++)
            {
                Mesh mesh = skinnedMeshes[i].sharedMesh;

                var packNormals = new Vector2[mesh.vertices.Length];
                var smoothNormal3 = new Vector3[mesh.vertices.Length];
                var smoothNormals = new Vector4[mesh.vertices.Length];

                Dictionary<Vector3, DVector3> smoothNormalsDict = new Dictionary<Vector3, DVector3>();

                // Group vertices by position
                for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
                {
                    var topology = mesh.GetTopology(subMeshIndex);
                    var indices = mesh.GetIndices(subMeshIndex);
                    
                    int primitiveVertexCount = GetPrimitiveVertexCount(topology);
                    
                    for (int index = 0; index < indices.Length; index += primitiveVertexCount)
                    {
                        Vector3 p0 = mesh.vertices[indices[index]];
                        Vector3 p1 = mesh.vertices[indices[index + 2]];
                        Vector3 p2 = mesh.vertices[indices[index + 1]];

                        DVector3[] v1 = new DVector3[3];
                        DVector3[] v2 = new DVector3[3];
                        
                        v1[0] = SafeNormalize(p1 - p0);
                        v2[0] = SafeNormalize(p2 - p0);
                        
                        v1[1] = SafeNormalize(p0 - p2);
                        v2[1] = SafeNormalize(p1 - p2);
                        
                        v1[2] = SafeNormalize(p2 - p1);
                        v2[2] = SafeNormalize(p0 - p1);
                            
                        DVector3 primitiveNormal = SafeCrossNormalized(v2[0], v1[0]);

                        for (int j = 0; j < primitiveVertexCount; j++)
                        {
                            Vector3 vertex = mesh.vertices[indices[index + j]];
                            DVector3 weightNormal = primitiveNormal * GetNormalWeight(v1[j], v2[j]);

                            if (smoothNormalsDict.TryGetValue(vertex, out DVector3 smoothNormal))
                            {
                                smoothNormal += weightNormal;
                                smoothNormalsDict[vertex] = smoothNormal;
                            }
                            else
                            {
                                smoothNormalsDict.Add(vertex, weightNormal);
                            }
                        }
                    }
                }

                // Calculate smooth normals
                for (int vertexIndex = 0; vertexIndex < mesh.vertices.Length; vertexIndex++)
                {
                    DVector3 smoothNormal = SafeNormalize(smoothNormalsDict[mesh.vertices[vertexIndex]]);
                    smoothNormal3[vertexIndex] = new Vector3((float)smoothNormal.x, (float)smoothNormal.y, (float)smoothNormal.z);
                    smoothNormals[vertexIndex] = new Vector4((float)smoothNormal.x, (float)smoothNormal.y, (float)smoothNormal.z, 0.0f);
                }

                // Write smooth normals
                if (channel == WriteChannel.Normal)
                {
                    mesh.normals = smoothNormal3;
                }
                else if (channel == WriteChannel.Tangent)
                {
                    mesh.tangents = smoothNormals;
                }
                else if (channel == WriteChannel.UV3)
                {
                    // Turn smooth normals from Object space to Tangent space
                    for (int index = 0; index < mesh.vertices.Length; index++)
                    {
                        Vector3 normalOS = mesh.normals[index];
                        Vector4 tangentOS = mesh.tangents[index];
                        Vector4 bitangentOS = GetBitangentOS(normalOS, tangentOS, skinnedMeshes[i].transform);
                        tangentOS.w = 0.0f;

                        Matrix4x4 tbn = Matrix4x4.identity;
                        tbn.SetRow(0, tangentOS.normalized);
                        tbn.SetRow(1, bitangentOS);
                        tbn.SetRow(2, (Vector4)normalOS.normalized);

                        Vector4 smoothNormalTS = tbn * smoothNormals[index];
                        packNormals[index] = PackNormalOctQuadEncode(smoothNormalTS.normalized);
                    }

                    mesh.uv3 = packNormals;
                }
            }
            
            Debug.Log("Smooth normals completed");
        }
        
        private void Awake()
        {
            SmoothNormals();
        }

        private static float GetOddNegativeScale(Transform trans)
        {
            float scale = Vector3.Dot(trans.localScale, Vector3.one);
            return scale >= 0.0f ? 1.0f : -1.0f;
        }

        private static Vector4 GetBitangentOS(Vector3 normalOS, Vector4 tangentOS, Transform trans)
        {
            Vector3 bitangnet = Vector3.Cross(normalOS.normalized, ((Vector3)tangentOS).normalized) 
                                * (tangentOS.w * GetOddNegativeScale(trans));
            
            bitangnet.Normalize();
            return new Vector4(bitangnet.x, bitangnet.y, bitangnet.z, 0.0f);
        }

        private static Vector2 PackNormalOctQuadEncode(Vector4 n)
        {
            return PackNormalOctQuadEncode((Vector3)n);
        }
        
        private static Vector2 PackNormalOctQuadEncode(Vector3 n)
        {
            float nDot1 = Mathf.Abs(n.x) + Mathf.Abs(n.y) + Mathf.Abs(n.z);
            n /= Mathf.Max(nDot1, 1e-6f);
            float tx = Mathf.Clamp01(-n.z);
            Vector2 t = new Vector2(tx, tx);
            Vector2 res = new Vector2(n.x, n.y);
            return res + (res is { x: >= 0.0f, y: >= 0.0f } ? t : -t);
        }

        private static int GetPrimitiveVertexCount(MeshTopology topology)
        {
            switch (topology)
            {
                case MeshTopology.Triangles:
                    return 3;
                case MeshTopology.Quads:
                    return 4;
                default:
                    throw new ArgumentException("Unsupported topology");
            }
        }

        private static double GetNormalWeight(DVector3 v1, DVector3 v2)
        {
            return Math.Acos(DVector3.Dot(v1, v2));
        }

        private static DVector3 SafeCrossNormalized(Vector3 v1, Vector3 v2)
        {
            DVector3 v1d = new DVector3(v1.x, v1.y, v1.z);
            DVector3 v2d = new DVector3(v2.x, v2.y, v2.z);
            
            v1d *= 1e8;
            v2d *= 1e8;

            return DVector3.Normalize(DVector3.Cross(v1d, v2d));
        }
        
        private static DVector3 SafeCrossNormalized(DVector3 v1, DVector3 v2)
        {
            v1 *= 1e8;
            v2 *= 1e8;

            return DVector3.Normalize(DVector3.Cross(v1, v2));
        }
        
        private static DVector3 SafeNormalize(Vector3 v)
        {
            DVector3 vd = new DVector3(v.x, v.y, v.z);
            
            vd *= 1e8;

            return DVector3.Normalize(vd);
        }
        
        private static DVector3 SafeNormalize(DVector3 v)
        {
            v *= 1e8;

            return DVector3.Normalize(v);
        }
    }
}