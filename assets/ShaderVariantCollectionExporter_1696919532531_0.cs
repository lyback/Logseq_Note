using System.Collections.Generic;
using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEditor.SceneManagement;
using Debug = UnityEngine.Debug;
using System.Text;
using System.Text.RegularExpressions;
public static class ShaderVariantCollectionExporter
{
    // [MenuItem("Tools/Shader/Find Standant")]
    public static void CheckMatShader()
    {
        string[] ExIncludeShaderNames = new string[] { "Standard" };
        var guids = AssetDatabase.FindAssets("t:Material");
        foreach (var guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            for (int i = 0; i < ExIncludeShaderNames.Length; i++)
            {
                if (mat.shader.name == ExIncludeShaderNames[i])
                {
                    Debug.LogErrorFormat("Mat:{0} use shader:{1}", mat.name, mat.shader.name);
                }
            }
        }
    }
    static string[] ms_scenePath = new string[] { "Assets/Scenes/ShaderVariant" };
    const string shaderVariantPath = "Assets/Shader/ShaderVariants.shadervariants";
    const string splitShaderVariantPath = "Assets/Shader/SplitShaderVariant/ShaderVariants_{0}.shadervariants";
    static readonly Stopwatch _elapsedTime = new Stopwatch();
    const int WaitTimeBeforeSave = 1000;
    static List<string> _sceneGuids;
    static int _curSceneIndex;
    public static void Export()
    {
        Shader.globalMaximumLOD = 300;

        InvokeInternalStaticMethod(typeof(ShaderUtil), "ClearCurrentShaderVariantCollection");
        var guids = AssetDatabase.FindAssets("t:Scene", ms_scenePath);
        _sceneGuids = guids.ToList<string>();
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
        {
            _sceneGuids.Add(scenes[i].guid.ToString());
        }
        _curSceneIndex = 0;
        if (_sceneGuids.Count > 0)
        {
            _elapsedTime.Stop();
            _elapsedTime.Reset();
            _elapsedTime.Start();
            EditorApplication.update += () =>
            {
                if (_elapsedTime.ElapsedMilliseconds >= WaitTimeBeforeSave)
                {
                    _elapsedTime.Stop();
                    _elapsedTime.Reset();
                    Debug.Log("index:" + _curSceneIndex);
                    Debug.Log("Count:" + _sceneGuids.Count);
                    if (_sceneGuids.Count <= _curSceneIndex)
                    {
                        InvokeInternalStaticMethod(typeof(ShaderUtil), "GetCurrentShaderVariantCollectionShaderCount");
                        InvokeInternalStaticMethod(typeof(ShaderUtil), "SaveCurrentShaderVariantCollection", shaderVariantPath);
                        EditorApplication.update = null;
                        AddSpecailShaderVariant();
                        return;
                    }

                    string assetPath = AssetDatabase.GUIDToAssetPath(_sceneGuids[_curSceneIndex]);
                    EditorSceneManager.OpenScene(assetPath);
                    EditorApplication.isPlaying = false;
                    InvokeInternalStaticMethod(typeof(ShaderUtil), "GetCurrentShaderVariantCollectionShaderCount");
                    InvokeInternalStaticMethod(typeof(ShaderUtil), "SaveCurrentShaderVariantCollection", shaderVariantPath);
                    _curSceneIndex++;
                    _elapsedTime.Start();
                }
                else
                {
                    PlayAll();
                }
            };
        }
        else
        {
            Debug.LogError("ShaderVarantCollection SceneCount is 0");
            EditorApplication.update = null;
            _elapsedTime.Stop();
            _elapsedTime.Reset();
        }
    }

    public static void PlayAll()
    {
        var ps = GameObject.FindObjectsOfType<ParticleSystem>();
        for (int i = 0; i < ps.Length; i++)
        {
            Debug.Log("Play:" + ps[i].name);
            ps[i].Play();
            ps[i].Simulate(Time.deltaTime, true);
        }
    }

    // static List<string> ShaderPath = new List<string>{
    //     "Assets/Shader/DianDian/FX/CommonFX.shader",
    //     "Assets/Shader/DianDian/FX/ScreenDistortionFX.shader",
    //     "Assets/Shader/fx/snowrongjieEffect.shader",
    // };
    public static void AddSpecailShaderVariant()
    {
        var shaderVariant = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(shaderVariantPath);
        // List<Shader> targetShaders = new List<Shader>();
        // List<List<PassType>> passTypes = new List<List<PassType>>();
        // for (int i = 0; i < ShaderPath.Count; i++)
        // {
        //     Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath[i]);
        //     targetShaders.Add(shader);
        //     passTypes.Add(GetShaderPassType(shader));
        // }
        var files = FileHelper.GetAllChildFiles(Application.dataPath + "/Art/effect/ui/material/", ".mat");
        for (int i = 0; i < files.Count; i++)
        {
            string path = files[i].ReplaceEmpty(Application.dataPath).StandardPath();
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets" + path);

            var kws = GetShaderKeywords(mat.shader);
            List<string> reskw = new List<string>();
            for (int p = 0; p < mat.shaderKeywords.Length; p++)
            {
                if (kws.Contains(mat.shaderKeywords[p]))
                {
                    reskw.Add(mat.shaderKeywords[p]);
                }
            }
            if (reskw.Count == 0)
            {
                continue;
            }
            List<PassType> pts = GetShaderPassType(mat.shader);
            for (int x = 0; x < pts.Count; x++)
            {
                string str = "";
                try
                {
                    for (int y = 0; y < reskw.Count; y++)
                    {
                        str += reskw[y];
                    }
                    ShaderVariantCollection.ShaderVariant sv = new ShaderVariantCollection.ShaderVariant(mat.shader, pts[x], reskw.ToArray());
                    shaderVariant.Add(sv);
                }
                catch (System.Exception e)
                {
                    Debug.LogError(string.Format("{0}\n{1}\n{2}\n{3}", path, pts[x], str, e.ToString()));
                }
            }
        }
        AssetDatabase.SaveAssets();
    }

    static List<List<string>> splitShaderList = new List<List<string>>{
        new List<string>{"DianDian/FX/CommonFX", "DianDian/FX/ScreenDistortionFX"},
    };
    public static void SplitShaderVariant()
    {
        var shaderVariant = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(shaderVariantPath);
        SerializedObject so = new SerializedObject(shaderVariant);
        var shaders = so.FindProperty("m_Shaders");
        for (int i = 0; i < splitShaderList.Count; i++)
        {
            ShaderVariantCollection _shaderVar = new ShaderVariantCollection();
            var _shaderList = splitShaderList[i];
            for (int shaderIndex = shaders.arraySize - 1; shaderIndex >= 0; shaderIndex--)
            {
                var entryProp = shaders.GetArrayElementAtIndex(shaderIndex);
                var first = entryProp.FindPropertyRelative("first");
                var variants = entryProp.FindPropertyRelative("second.variants");
                Shader shader = (Shader)first.objectReferenceValue;
                if (shader != null)
                {
                    for (int j = 0; j < _shaderList.Count; j++)
                    {
                        if (shader.name == _shaderList[j])
                        {
                            CreateSplitShaderVariant(_shaderVar, variants, shader);
                            shaders.DeleteArrayElementAtIndex(shaderIndex);
                        }
                    }
                }
            }
            if (_shaderVar.shaderCount > 0)
            {
                string path = string.Format(splitShaderVariantPath, i);
                FileHelper.CreateDirectoryFromFile(path);
                AssetDatabase.CreateAsset(_shaderVar, path);
            }
        }
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }
    public static void TestSplitShaderVariant()
    {
        var shaderVariant = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(shaderVariantPath);
        SerializedObject so = new SerializedObject(shaderVariant);
        var shaders = so.FindProperty("m_Shaders");
        for (int shaderIndex = shaders.arraySize - 1; shaderIndex >= 0; shaderIndex--)
        {
            ShaderVariantCollection _shaderVar = new ShaderVariantCollection();
            var entryProp = shaders.GetArrayElementAtIndex(shaderIndex);
            var first = entryProp.FindPropertyRelative("first");
            var variants = entryProp.FindPropertyRelative("second.variants");
            Shader shader = (Shader)first.objectReferenceValue;
            CreateSplitShaderVariant(_shaderVar, variants, shader);
            if (_shaderVar.shaderCount > 0)
            {
                string path = $"Assets/SplitShaderVariant/{shader.name.Replace('/', '_')}.shadervariants";
                FileHelper.CreateDirectoryFromFile(path);
                AssetDatabase.CreateAsset(_shaderVar, path);
            }
        }
        AssetDatabase.SaveAssets();
    }
    private static void CreateSplitShaderVariant(ShaderVariantCollection targetShaderVariant, SerializedProperty variants, Shader shader)
    {
        for (int i = 0; i < variants.arraySize; i++)
        {
            var prop = variants.GetArrayElementAtIndex(i);
            var keywords = prop.FindPropertyRelative("keywords").stringValue;
            var passType = (UnityEngine.Rendering.PassType)prop.FindPropertyRelative("passType").intValue;
            var _res = new ShaderVariantCollection.ShaderVariant(shader, passType, keywords.Split(' '));
            targetShaderVariant.Add(_res);
        }
    }
    public static void CleanSplitShaderVariant()
    {
        string path = $"{Application.dataPath}/Shader/SplitShaderVariant";
        if (Directory.Exists(path))
        {
            FileHelper.DeleteDirectory(path);
        }
    }
    //InvokeInternalStaticMethod(typeof(ShaderUtil), "SaveCurrentShaderVariantCollection", "path");
    //InvokeInternalStaticMethod(typeof(ShaderUtil), "ClearCurrentShaderVariantCollection");
    //InvokeInternalStaticMethod(typeof(ShaderUtil), "GetCurrentShaderVariantCollectionShaderCount");
    private static object InvokeInternalStaticMethod(System.Type type, string method, params object[] parameters)
    {
        var methodInfo = type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
        if (methodInfo == null)
        {
            Debug.LogError(string.Format("{0} method didn't exist", method));
            return null;
        }

        return methodInfo.Invoke(null, parameters);
    }

    public static List<PassType> GetShaderPassType(Shader shader)
    {
        int[] pts;
        string[] kw;
        GetShaderVariantEntries(shader, new ShaderVariantCollection(), out pts, out kw);
        List<PassType> res = new List<PassType>();
        for (int i = 0; i < pts.Length; i++)
        {
            var _pt = (PassType)pts[i];
            if (!res.Contains(_pt))
            {
                res.Add(_pt);
            }
        }
        return res;
    }
    private static HashSet<string> GetShaderKeywords(Shader shader)
    {
        int[] pts;
        string[] kw;
        GetShaderVariantEntries(shader, new ShaderVariantCollection(), out pts, out kw);
        HashSet<string> totalKeywords = new HashSet<string>();
        for (int i = 0; i < kw.Length; i++)
        {
            var _kws = kw[i].Split(' ');
            for (int j = 0; j < _kws.Length; j++)
            {
                if (!totalKeywords.Contains(_kws[j]))
                {
                    totalKeywords.Add(_kws[j]);
                }
            }
        }
        return totalKeywords;
    }
    public static void GetShaderVariantEntries(
            Shader shader,
            ShaderVariantCollection skipAlreadyInCollection,
            out int[] types,
            out string[] keywords)
    {
        var assembly = typeof(EditorApplication).Assembly;
        var shaderUtilType = assembly.GetType("UnityEditor.ShaderUtil");
        var getShaderVariantEntries =
            shaderUtilType.GetMethod("GetShaderVariantEntries",
            BindingFlags.Static | BindingFlags.NonPublic);

        var parameters = new object[]
        {
                shader,
                skipAlreadyInCollection,
                null,
                null
        };
        getShaderVariantEntries.Invoke(null, parameters);
        types = (int[])parameters[2];
        keywords = (string[])parameters[3];
    }
}