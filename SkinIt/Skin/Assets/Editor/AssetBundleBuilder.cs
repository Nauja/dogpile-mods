using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class AssetBundleBuilder : EditorWindow
{
    public static string prefix = "TeK's AssetBundle Builder";
    public static string[] assetBundles = new string[0];
    private BuildAssetBundleOptions selectedBuildOptions = BuildAssetBundleOptions.None;

    [MenuItem("Assets/Build AssetBundles")]
    static void BuildAssetBundlesContextMenu()
    {
        assetBundles = AssetDatabase.GetAllAssetBundleNames().Where(x => x.EndsWith(".assets")).ToArray();
        AssetBundleBuilder window = GetWindow<AssetBundleBuilder>(prefix);
        float contentHeight = (assetBundles.Length * EditorGUIUtility.singleLineHeight) + 55f;
        window.position = new Rect(Screen.width / 2, Screen.height / 2, 250, contentHeight); // Set initial position and size
        window.Show();
    }

    void OnGUI()
    {
        GUILayout.Space(2f);
        GUIStyle titleLabelStyle = new GUIStyle(GUI.skin.label);
        titleLabelStyle.alignment = TextAnchor.MiddleCenter;
        titleLabelStyle.fontStyle = FontStyle.Bold;
        GUILayout.Label("Select the assetbundle you want to build", titleLabelStyle);
        GUILayout.Space(2f);

        if (assetBundles.Length == 0)
        {
            GUIStyle centeredLabelStyle = new GUIStyle(GUI.skin.label);
            centeredLabelStyle.alignment = TextAnchor.MiddleCenter;
            centeredLabelStyle.fontStyle = FontStyle.Bold;
            centeredLabelStyle.fontSize = 17;
            GUILayout.Label("No assetbundles found !\n(*.assets only)", centeredLabelStyle);
        }

        foreach (string bundleName in assetBundles)
            if (GUILayout.Button(bundleName))
                BuildAssetBundle(bundleName);

        GUILayout.Space(2f);
        selectedBuildOptions = (BuildAssetBundleOptions)EditorGUILayout.EnumPopup("Build Options", selectedBuildOptions);
        GUILayout.Space(2f);
    }

    void Update() => assetBundles = AssetDatabase.GetAllAssetBundleNames().Where(x => x.EndsWith(".assets")).ToArray();

    void BuildAssetBundle(string bundleName)
    {
        string assetBundleDirectory = "Assets/AssetBundles";
        if (!Directory.Exists(assetBundleDirectory))
            Directory.CreateDirectory(assetBundleDirectory);

        try
        {
            List<AssetBundleBuild> builds = new List<AssetBundleBuild>();
            var assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);
            AssetBundleBuild build = new AssetBundleBuild();
            build.assetBundleName = bundleName;
            build.assetNames = assetPaths;
            builds.Add(build);
            BuildPipeline.BuildAssetBundles("Assets/AssetBundles", builds.ToArray(), selectedBuildOptions, EditorUserBuildSettings.activeBuildTarget);

            DirectoryInfo d = new DirectoryInfo(assetBundleDirectory);
            File.Delete("Assets/AssetBundles/AssetBundles");
            foreach (var file in d.GetFiles("*.manifest"))
                file.Delete();
            foreach (var file in d.GetFiles("*.manifest.meta"))
                file.Delete();
            AssetDatabase.Refresh();
            Debug.Log("<b>"+prefix+"</b>: The assetbundle \""+bundleName+"\" has been sucessfully built!"+(selectedBuildOptions == BuildAssetBundleOptions.None ? "" : " (Selected Option: "+selectedBuildOptions.ToString()+")"));
        }
        catch (Exception e)
        {
            Debug.LogError("<b>" + prefix + "</b>: A fatal error occured when building the assetbundle \"" + bundleName+"\"! \n" + e.ToString());
        }


        
    }
}