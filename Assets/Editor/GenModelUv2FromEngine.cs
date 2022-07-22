using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class GenModelUv2FromEngine
{
    private static bool staticOnly = false;
    private static void SetUV(GameObject[] gos)
    {
        if (gos == null || gos.Length == 0)
        {
            return;
        }

        var paths = new HashSet<string>();

        for (int i = 0, len = gos.Length;i<len;i++)
        {
            var go = gos[i];
            if (go == null)
            {
                continue;
            }

            var isDiskRes = EditorUtility.IsPersistent(go);
            if (!isDiskRes)
            {
                if (staticOnly)
                {
                    continue;
                }

                go = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
            }
        
            var dependencies = AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(go));
            foreach (var d in dependencies)
            {
                if (PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadMainAssetAtPath(d)) == PrefabAssetType.Model)
                {
                    paths.Add(d);
                }
            }
        }
        
        foreach (var path in paths)
        {
            var m = AssetImporter.GetAtPath(path) as ModelImporter;
            if (m != null)
            {
                // 先判断是否有2Uv
                var rootGo = AssetDatabase.LoadMainAssetAtPath(path) as GameObject;
                if (!rootGo)
                {
                    Debug.LogError($"{path} Load MainAsset Failed .");
                    continue;
                }
                
                var filters = rootGo.GetComponentsInChildren<MeshFilter>();
                if (filters.Length == 0)
                {
                    continue;
                }

                if (!m.generateSecondaryUV)
                {
                    // 如果没有勾选自动生成，并且有UV2的对象就直接跳过
                    if (filters.Any(f => f.sharedMesh != null && f.sharedMesh.uv2.Length > 0))
                    {
                        continue;
                    }

                    m.generateSecondaryUV = true;
                }

                var originReadableState = m.isReadable; 
                m.isReadable = true;
                m.SaveAndReimport();

                var meshUvDic = new Dictionary<Mesh, Vector2[]>();
                
                foreach (var filter in filters)
                {
                    if (filter.sharedMesh == null)
                    {
                        continue;
                    }

                    if (filter.sharedMesh.uv2 == null || filter.sharedMesh.uv2.Length == 0)
                    {
                        continue;
                    }

                    // var shareMesh = filter.sharedMesh;
                    // var newMesh = new Mesh();
                    // var combineInstance = new CombineInstance[1];
                    // combineInstance[0].mesh = shareMesh;
                    // newMesh.CombineMeshes(combineInstance);
                    meshUvDic.Add(filter.sharedMesh, filter.sharedMesh.uv2);
                }
                m.generateSecondaryUV = false;
                m.SaveAndReimport();
                
                foreach (var item in meshUvDic)
                {
                    if (item.Key.vertices.Length != item.Value.Length)
                    {
                        continue;
                    }

                    item.Key.uv2 = item.Value;
                }
            }
            Debug.Log(path);
        }
        AssetDatabase.Refresh();
    }

    [MenuItem("GameObject/GenerateUV2", false, priority = 30)]
    private static void Test()
    {
        // SetUV(Selection.gameObjects);
    }

    [MenuItem("Assets/GenerateUV2", false, priority = 30)]
    private static void Test1()
    {
        var assetsParentPath = Directory.GetParent(Application.dataPath)?.ToString();
        if (string.IsNullOrEmpty(assetsParentPath))
        {
            Debug.LogError("Assets Parent Didnt Exist .");
            return;
        }

        var objs = Selection.GetFiltered<Object>(SelectionMode.Assets);
        var genUvList = new List<GameObject>();
        
        foreach (var obj in objs)
        {
            if (obj is DefaultAsset)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                var direction = new DirectoryInfo(path);
                var files = direction.GetFiles("*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    if (!IsSupportModelFile(file))
                    {
                        continue;
                    }

                    var resRelativePath = file.FullName.Remove(0, assetsParentPath.Length + 1);
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(resRelativePath);
                    if (go)
                    {
                        genUvList.Add(go);
                    }
                }
            }else if (obj is GameObject go)
            {
                genUvList.Add(go);
            }
        }
        
        SetUV(genUvList.ToArray());
    }

    private static bool IsSupportModelFile(FileInfo fileInfo)
    {
        if (fileInfo.Extension.ToLower() == ".fbx")
        {
            return true;
        }

        return false;
    }
}
