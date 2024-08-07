﻿using Memoria.Scripts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Memoria.Assets
{
    public class WavefrontObject
    {
        public List<NamedObject> objects = new List<NamedObject>();
        public List<Vector3> allVertices = new List<Vector3>();
        public List<Vector3> allNormals = new List<Vector3>();
        public List<Vector2> allUvs = new List<Vector2>();

        public void LoadFromFile(String filename)
        {
            NamedObject currentObj = null;
            using (Stream input = File.OpenRead(filename))
            using (StreamReader reader = new StreamReader(input))
            {
                while (!reader.EndOfStream)
                {
                    String line = reader.ReadLine().Trim();
                    if (String.IsNullOrEmpty(line) || line.StartsWith("#"))
                        continue;
                    String[] splits = line.Split((Char[])null, StringSplitOptions.RemoveEmptyEntries);
                    String operation = splits[0];
                    if (operation == "o")
                    {
                        currentObj = new NamedObject();
                        currentObj.name = splits.Length > 1 ? splits[1] : String.Empty;
                        objects.Add(currentObj);
                        continue;
                    }
                    if (operation == "v")
                        ProcessVertex(currentObj, splits);
                    else if (line.StartsWith("vn"))
                        ProcessNormal(currentObj, splits);
                    else if (line.StartsWith("vt"))
                        ProcessUV(currentObj, splits);
                    else if (line.StartsWith("f"))
                        ProcessFace(currentObj, splits);
                }
            }
            SetupRelativeIndices();
        }

        public void LoadFromBGI(BGI_DEF bgi)
        {
            Dictionary<Int32, Int32> floorVert = new Dictionary<Int32, Int32>();
            Dictionary<Int32, Int32> floorNorm = new Dictionary<Int32, Int32>();
            Int32 vertexShift = 1;
            Int32 normalShift = 1;
            foreach (BGI_FLOOR_DEF floor in bgi.floorList)
            {
                NamedObject obj = new NamedObject();
                obj.name = $"Walhpath_{floor.floorNdx}";
                floorVert.Clear();
                floorNorm.Clear();
                for (Int32 i = 0; i < floor.triNdxList.Count; i++)
                {
                    BGI_TRI_DEF triangle = bgi.triList[floor.triNdxList[i]];
                    foreach (Int16 floorVId in triangle.vertexNdx)
                        floorVert[floorVId] = -1;
                    if (triangle.normalNdx >= 0)
                        floorNorm[triangle.normalNdx] = -1;
                }
                Int32 counter = 0;
                foreach (Int32 floorVId in floorVert.Keys.ToArray())
                {
                    Vector3 v = BgiVecToVector(bgi.orgPos) + BgiVecToVector(floor.orgPos) + BgiVecToVector(bgi.vertexList[floorVId]);
                    obj.vertices.Add(v);
                    allVertices.Add(v);
                    floorVert[floorVId] = counter++;
                }
                counter = 0;
                foreach (Int32 floorNId in floorNorm.Keys.ToArray())
                {
                    Vector3 n = BgiFVecToVector(bgi.normalList[floorNId]);
                    obj.normals.Add(n);
                    allNormals.Add(n);
                    floorNorm[floorNId] = counter++;
                }
                for (Int32 i = 0; i < floor.triNdxList.Count; i++)
                {
                    BGI_TRI_DEF triangle = bgi.triList[floor.triNdxList[i]];
                    Int32 normalId = triangle.normalNdx >= 0 ? floorNorm[triangle.normalNdx] : -1;
                    Face f = new Face();
                    foreach (Int16 floorVId in triangle.vertexNdx)
                    {
                        f.vId.Add(floorVert[floorVId]);
                        f.tId.Add(-1);
                        f.nId.Add(normalId);
                        f.vIdAbsolute.Add(vertexShift + floorVert[floorVId]);
                        f.tIdAbsolute.Add(0);
                        f.nIdAbsolute.Add(normalId >= 0 ? normalShift + normalId : 0);
                    }
                    obj.faces.Add(f);
                }
                vertexShift += floorVert.Count;
                normalShift += floorNorm.Count;
                objects.Add(obj);
            }
        }

        public String GenerateFile(String fieldName = "")
        {
            String content = "";
            if (!String.IsNullOrEmpty(fieldName))
                content += $"# Walkmesh of the field {fieldName}\n";
            content += $"# Generated by Memoria\n";
            Int32 vIndex = 1;
            Int32 vtIndex = 1;
            Int32 vnIndex = 1;
            foreach (NamedObject obj in objects)
            {
                content += $"o {obj.name}\n";
                foreach (Vector3 v in obj.vertices)
                    content += $"v {v.x} {v.y} {v.z}\n";
                foreach (Vector2 vt in obj.uvs)
                    content += $"vt {vt.x} {vt.y}\n";
                foreach (Vector3 vn in obj.normals)
                    content += $"vn {vn.x} {vn.y} {vn.z}\n";
                foreach (Face f in obj.faces)
                {
                    content += "f";
                    for (Int32 i = 0; i < f.vId.Count; i++)
                    {
                        Boolean hasVt = f.tId[i] >= 0;
                        Boolean hasVn = f.nId[i] >= 0;
                        content += $" {vIndex + f.vId[i]}";
                        if (hasVt || hasVn)
                        {
                            content += $"/";
                            if (hasVt)
                                content += $"{vtIndex + f.tId[i]}";
                            if (hasVn)
                                content += $"/{vnIndex + f.nId[i]}";
                        }
                    }
                    content += "\n";
                }
                vIndex += obj.vertices.Count;
                vtIndex += obj.uvs.Count;
                vnIndex += obj.normals.Count;
            }
            return content;
        }

        public BGI_DEF ConvertToBGI()
        {
            BGI_DEF bgi = new BGI_DEF();
            Dictionary<Edge, EdgeInfo> edgeDictionary = new Dictionary<Edge, EdgeInfo>();
            bgi.activeFloor = 0;
            bgi.activeTri = 0;
            Vector3 allVertMin = new Vector3(Single.MaxValue, Single.MaxValue, Single.MaxValue);
            Vector3 allVertMax = new Vector3(Single.MinValue, Single.MinValue, Single.MinValue);
            foreach (NamedObject obj in objects)
            {
                foreach (Vector3 v in obj.vertices)
                {
                    allVertMin = Vector3.Min(allVertMin, v);
                    allVertMax = Vector3.Max(allVertMax, v);
                }
            }
            Vector3 centerPos = (allVertMin + allVertMax) / 2f;
            bgi.curPos = VectorToBgiVec(centerPos);
            bgi.orgPos = VectorToBgiVec(centerPos);
            bgi.minPos = VectorToBgiVec(centerPos);
            bgi.maxPos = VectorToBgiVec(centerPos);
            bgi.charPos = VectorToBgiVec(centerPos);
            for (Int32 i = 0; i < objects.Count; i++)
            {
                NamedObject obj = objects[i];
                Vector3 pathMin = new Vector3(Single.MaxValue, Single.MaxValue, Single.MaxValue);
                Vector3 pathMax = new Vector3(Single.MinValue, Single.MinValue, Single.MinValue);
                foreach (Vector3 v in obj.vertices)
                {
                    pathMin = Vector3.Min(pathMin, v);
                    pathMax = Vector3.Max(pathMax, v);
                }
                Vector3 floorPos = (pathMin + pathMax) / 2f - BgiVecToVector(bgi.curPos);
                ConvertToBGI_AddFloor(bgi, floorPos);
                List<Int32> triNdxList = bgi.floorList[i].triNdxList;
                Int32 vStartId = bgi.vertexList.Count;
                foreach (Vector3 v in obj.vertices)
                    bgi.vertexList.Add(VectorToBgiVec(v - centerPos - floorPos));
                foreach (Face f in obj.faces)
                {
                    for (Int32 j = 2; j < f.vId.Count; j++)
                    {
                        triNdxList.Add(bgi.triList.Count);
                        ConvertToBGI_AddTriangle(bgi, edgeDictionary, i, 1, vStartId + f.vId[0], vStartId + f.vId[j - 1], vStartId + f.vId[j], f.vIdAbsolute[0], f.vIdAbsolute[j - 1], f.vIdAbsolute[j]);
                    }
                }
            }
            bgi.UpdateOffsets();
            return bgi;
        }

        private void ConvertToBGI_AddFloor(BGI_DEF bgi, Vector3 pos)
        {
            BGI_FLOOR_DEF floor = new BGI_FLOOR_DEF();
            floor.floorFlags = 0;
            floor.floorNdx = (UInt16)bgi.floorList.Count;
            floor.orgPos = VectorToBgiVec(pos);
            floor.curPos = VectorToBgiVec(pos);
            floor.minPos = VectorToBgiVec(pos);
            floor.maxPos = VectorToBgiVec(pos);
            bgi.floorList.Add(floor);
        }

        private void ConvertToBGI_AddTriangle(BGI_DEF bgi, Dictionary<Edge, EdgeInfo> edgeDictionary, Int32 floorNdx, UInt16 triFlags, Int32 i1, Int32 i2, Int32 i3, Int32 iAbs1, Int32 iAbs2, Int32 iAbs3)
        {
            BGI_FLOOR_DEF floor = bgi.floorList[floorNdx];
            NamedObject obj = objects[floorNdx];
            BGI_VEC_DEF v1 = bgi.vertexList[i1];
            BGI_VEC_DEF v2 = bgi.vertexList[i2];
            BGI_VEC_DEF v3 = bgi.vertexList[i3];
            Vector3 vect1 = BgiVecToVector(v1);
            Vector3 vect2 = BgiVecToVector(v2);
            Vector3 vect3 = BgiVecToVector(v3);
            Vector3 normal = Vector3.Cross(vect2 - vect1, vect3 - vect1).normalized;
            Boolean isFlatTri = v1.coord[1] == v2.coord[1] && v1.coord[1] == v3.coord[1];
            if (normal.y > 0)
                normal *= -1;
            BGI_TRI_DEF tri = new BGI_TRI_DEF();
            tri.center = VectorToBgiVec((vect1 + vect2 + vect3) / 3f);
            tri.floorNdx = (Int16)floorNdx;
            tri.triFlags = triFlags;
            tri.triData = 0;
            tri.vertexNdx[0] = (Int16)i1;
            tri.vertexNdx[1] = (Int16)i2;
            tri.vertexNdx[2] = (Int16)i3;

            tri.d = Math3D.Float2Fixed(Vector3.Dot(BgiVecToVector(bgi.orgPos) + BgiVecToVector(floor.orgPos) + vect1, normal));
            if (isFlatTri)
            {
                tri.thetaX = 0;
                tri.thetaZ = 0;
                tri.normalNdx = -1;
            }
            else
            {
                BGI_FVEC_DEF n = VectorToBgiFVec(normal);
                tri.normalNdx = (Int16)bgi.normalList.Count;
                bgi.normalList.Add(n);
                Vector3 nx = normal;
                Vector3 nz = normal;
                nx.z = 0f;
                nz.x = 0f;
                if (nx == Vector3.zero)
                    tri.thetaX = 0;
                else
                    tri.thetaX = (Int16)(-Math.Sign(nx.x) * Vector3.Angle(Vector3.down, nx) / 90f * 1000f);
                if (nz == Vector3.zero)
                    tri.thetaZ = 0;
                else
                    tri.thetaZ = (Int16)(Math.Sign(nz.x) * Vector3.Angle(Vector3.down, nz) / 90f * 1000f);
            }

            Int32[] iAbs = new Int32[3] { iAbs1, iAbs2, iAbs3 };
            iAbs1 = iAbs.Min();
            iAbs3 = iAbs.Max();
            iAbs2 = iAbs.FirstOrDefault(index => index != iAbs1 && index != iAbs3);
            Edge[] edges = new Edge[] { new Edge(iAbs1, iAbs2), new Edge(iAbs2, iAbs3), new Edge(iAbs3, iAbs1) };
            for (Int32 i = 0; i < 3; i++)
            {
                BGI_EDGE_DEF bgiEdge = new BGI_EDGE_DEF();
                bgiEdge.edgeClone = -1;
                bgiEdge.edgeFlags = 0;
                tri.edgeNdx[i] = (Int16)bgi.edgeList.Count;
                if (edgeDictionary.TryGetValue(edges[i], out EdgeInfo edgeInfo))
                {
                    bgiEdge.edgeClone = (Int16)edgeInfo.edgeId;
                    tri.neighborNdx[i] = (Int16)edgeInfo.triId;
                    bgi.triList[edgeInfo.triId].neighborNdx[edgeInfo.edgeIndex] = (Int16)bgi.triList.Count;
                }
                else
                {
                    edgeInfo = new EdgeInfo();
                    edgeInfo.edgeId = bgi.edgeList.Count;
                    edgeInfo.triId = bgi.triList.Count;
                    edgeInfo.edgeIndex = i;
                    tri.edgeNdx[i] = (Int16)bgi.edgeList.Count;
                    tri.neighborNdx[i] = -1;
                    edgeDictionary[edges[i]] = edgeInfo;
                }
                bgi.edgeList.Add(bgiEdge);
            }

            tri.triIdx = bgi.triList.Count;
            bgi.triList.Add(tri);
        }

        private void ProcessVertex(NamedObject obj, String[] splits)
        {
            if (splits.Length < 4)
                return;
            Vector3 v = default(Vector3);
            Single.TryParse(splits[1], out v.x);
            Single.TryParse(splits[2], out v.y);
            Single.TryParse(splits[3], out v.z);
            allVertices.Add(v);
        }

        private void ProcessNormal(NamedObject obj, String[] splits)
        {
            if (splits.Length < 4)
                return;
            Vector3 vn = default(Vector3);
            Single.TryParse(splits[1], out vn.x);
            Single.TryParse(splits[2], out vn.y);
            Single.TryParse(splits[3], out vn.z);
            allNormals.Add(vn);
        }

        private void ProcessUV(NamedObject obj, String[] splits)
        {
            if (splits.Length < 3)
                return;
            Vector2 vt = default(Vector2);
            Single.TryParse(splits[1], out vt.x);
            Single.TryParse(splits[2], out vt.y);
            allUvs.Add(vt);
        }

        private void ProcessFace(NamedObject obj, String[] splits)
        {
            if (splits.Length < 4)
                return;
            Char[] separator = new Char[] { '/' };
            Face f = new Face();
            for (Int32 i = 1; i < splits.Length; i++)
            {
                String[] faceSplits = splits[i].Split(separator);
                Boolean hasVt = faceSplits.Length >= 2 && !String.IsNullOrEmpty(faceSplits[1]);
                Boolean hasVn = faceSplits.Length >= 3;
                if (!Int32.TryParse(faceSplits[0], out Int32 vId))
                    continue;
                if (vId < 0)
                    vId = allVertices.Count + vId;
                f.vIdAbsolute.Add(vId);
                if (hasVt && Int32.TryParse(faceSplits[1], out Int32 tId))
                {
                    if (tId < 0)
                        tId = allUvs.Count + tId;
                    f.tIdAbsolute.Add(tId);
                }
                else
                {
                    f.tIdAbsolute.Add(0);
                }
                if (hasVn && Int32.TryParse(faceSplits[2], out Int32 nId))
                {
                    if (nId < 0)
                        nId = allNormals.Count + nId;
                    f.nIdAbsolute.Add(nId);
                }
                else
                {
                    f.nIdAbsolute.Add(0);
                }
            }
            obj.faces.Add(f);
        }

        private void SetupRelativeIndices()
        {
            foreach (NamedObject obj in objects)
            {
                Dictionary<Int32, Int32> addV = new Dictionary<Int32, Int32>();
                Dictionary<Int32, Int32> addN = new Dictionary<Int32, Int32>();
                Dictionary<Int32, Int32> addT = new Dictionary<Int32, Int32>();
                obj.vertices.Clear();
                obj.normals.Clear();
                obj.uvs.Clear();
                foreach (Face f in obj.faces)
                {
                    SetupRelativeIndices_SingleIndex(addV, f.vIdAbsolute, f.vId, obj.vertices, allVertices, false);
                    SetupRelativeIndices_SingleIndex(addN, f.nIdAbsolute, f.nId, obj.normals, allNormals, true);
                    SetupRelativeIndices_SingleIndex(addT, f.tIdAbsolute, f.tId, obj.uvs, allUvs, true);
                }
            }
        }

        private void SetupRelativeIndices_SingleIndex<T>(Dictionary<Int32, Int32> addIndices, List<Int32> absoluteList, List<Int32> relativeList, List<T> objList, List<T> allObjList, Boolean acceptInvalidId)
        {
            relativeList.Clear();
            foreach (Int32 id in absoluteList)
            {
                if (id > 0)
                {
                    if (!addIndices.ContainsKey(id))
                    {
                        addIndices[id] = objList.Count;
                        relativeList.Add(objList.Count);
                        objList.Add(allObjList[id - 1]);
                    }
                    else
                    {
                        relativeList.Add(addIndices[id]);
                    }
                }
                else
                {
                    if (acceptInvalidId)
                        relativeList.Add(-1);
                    else
                        throw new ArgumentOutOfRangeException(nameof(absoluteList), id, $"Wavefront .obj importer: Id cannot be 0 or negative");
                }
            }
        }

        public class NamedObject
        {
            public String name;
            public List<Vector3> vertices = new List<Vector3>();
            public List<Vector3> normals = new List<Vector3>();
            public List<Vector2> uvs = new List<Vector2>();
            public List<Face> faces = new List<Face>();

            public GameObject CreateGameObject(Boolean doubleSide)
            {
                // Rendering Wavefront object directly instead of converting it to BGI first might be useful for showing the texture even that texture is not ported to the walkmesh in the end
                // That is, if texture and material was supported here...
                GameObject meshGo = new GameObject(name);
                Int32 triangleIndexCount = (doubleSide ? 6 : 3) * faces.Sum(f => f.vId.Count - 2);
                Vector2[] uvArray = new Vector2[vertices.Count];
                Int32[] triangleArray = new Int32[triangleIndexCount];
                for (Int32 i = 0; i < vertices.Count; i++)
                {
                    uvArray[i] = default(Vector2);
                }
                Int32 triangleIndex = 0;
                foreach (Face f in faces)
                {
                    for (Int32 j = 2; j < f.vId.Count; j++)
                    {
                        triangleArray[triangleIndex++] = f.vId[0];
                        triangleArray[triangleIndex++] = f.vId[j - 1];
                        triangleArray[triangleIndex++] = f.vId[j];
                        if (doubleSide)
                        {
                            triangleArray[triangleIndex++] = f.vId[0];
                            triangleArray[triangleIndex++] = f.vId[j];
                            triangleArray[triangleIndex++] = f.vId[j - 1];
                        }
                    }
                }
                Mesh mesh = new Mesh
                {
                    vertices = vertices.ToArray(),
                    uv = uvArray,
                    triangles = triangleArray
                };
                MeshRenderer meshRenderer = meshGo.AddComponent<MeshRenderer>();
                MeshFilter meshFilter = meshGo.AddComponent<MeshFilter>();
                meshFilter.mesh = mesh;
                meshRenderer.material = new Material(ShadersLoader.Find("Unlit/AdjustableTransparent"));
                return meshGo;
            }

            public Vector3 GetFaceCenter(Int32 faceId)
            {
                Vector3 center = Vector3.zero;
                foreach (Int32 vId in faces[faceId].vId)
                    center += vertices[vId];
                return center / faces[faceId].vId.Count;
            }
        }

        public class Face
        {
            public List<Int32> vIdAbsolute = new List<Int32>();
            public List<Int32> nIdAbsolute = new List<Int32>();
            public List<Int32> tIdAbsolute = new List<Int32>();
            public List<Int32> vId = new List<Int32>();
            public List<Int32> nId = new List<Int32>();
            public List<Int32> tId = new List<Int32>();
        }

        private struct Edge
        {
            public Int32 vId1;
            public Int32 vId2;

            public Edge(Int32 i1, Int32 i2)
            {
                vId1 = i1;
                vId2 = i2;
            }
        }

        private struct EdgeInfo
        {
            public Int32 edgeId;
            public Int32 triId;
            public Int32 edgeIndex;
        }

        public static Vector3 BgiVecToVector(BGI_VEC_DEF v)
        {
            Vector3 vect = v.ToVector3();
            vect.y *= -1;
            vect.z *= -1;
            return vect;
        }

        public static BGI_VEC_DEF VectorToBgiVec(Vector3 vect)
        {
            BGI_VEC_DEF v = BGI_VEC_DEF.FromVector3(vect);
            v.coord[1] *= -1;
            v.coord[2] *= -1;
            return v;
        }

        public static Vector3 BgiFVecToVector(BGI_FVEC_DEF v)
        {
            Vector3 vect = v.ToVector3();
            vect.y *= -1;
            vect.z *= -1;
            return vect;
        }

        public static BGI_FVEC_DEF VectorToBgiFVec(Vector3 vect)
        {
            BGI_FVEC_DEF v = BGI_FVEC_DEF.FromVector3(vect);
            v.coord[1] *= -1;
            v.coord[2] *= -1;
            v.oneOverY *= -1;
            return v;
        }
    }
}
