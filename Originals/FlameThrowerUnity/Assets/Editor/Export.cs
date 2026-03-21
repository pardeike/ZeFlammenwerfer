using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class CreateAssetBundles
{
	const string BundleName = "flamethrower";
	static readonly BuildTarget[] SupportedStandaloneTargets =
	{
		BuildTarget.StandaloneWindows64,
		BuildTarget.StandaloneLinux64,
		BuildTarget.StandaloneOSX,
	};

	static readonly string[] BundleAssets =
	{
		"Assets/Mat_GlowDot.mat",
		"Assets/BlockerVisual.prefab",
		"Assets/Fire.prefab",
		"Assets/FireBlock.shader",
		"Assets/T_Flames.psd",
		"Assets/Mat_Fire.mat",
		"Assets/T_Spark.psd",
		"Assets/Mat_Block.mat",
		"Assets/ParticleUnlit.shader",
		"Assets/BlockCube.prefab",
		"Assets/Mat_Smoke.mat",
		"Assets/Smoke.prefab",
		"Assets/T_Smoke.psd",
	};

	[MenuItem("Assets/Build Standalone AssetBundles")]
	public static void BuildStandaloneAssetBundles()
	{
		var buildTarget = EditorUserBuildSettings.activeBuildTarget;

		if (!IsSupportedStandaloneTarget(buildTarget))
		{
			EditorUtility.DisplayDialog(
				"Unsupported Build Target",
				$"Switch Build Settings to a standalone target before building AssetBundles. Current target: {buildTarget}.",
				"OK");
			return;
		}

		if (BuildAndDeployAssetBundle(buildTarget))
		{
			AssetDatabase.Refresh();
			EditorUtility.RevealInFinder(Path.Combine(FindModRoot(Directory.GetParent(Application.dataPath).FullName), "Resources"));
		}
	}

	[MenuItem("Assets/Build All Standalone AssetBundles")]
	public static void BuildAllStandaloneAssetBundles()
	{
		var buildCount = 0;
		for (var i = 0; i < SupportedStandaloneTargets.Length; i++)
		{
			if (BuildAndDeployAssetBundle(SupportedStandaloneTargets[i]))
				buildCount++;
		}

		if (buildCount == 0)
		{
			Debug.LogError("No standalone AssetBundles were built.");
			return;
		}

		var projectRoot = Directory.GetParent(Application.dataPath).FullName;
		var modRoot = FindModRoot(projectRoot);
		if (modRoot == null)
		{
			Debug.LogError("Built AssetBundles, but could not locate the RimWorld mod root to reveal the Resources directory.");
			return;
		}

		AssetDatabase.Refresh();
		EditorUtility.RevealInFinder(Path.Combine(modRoot, "Resources"));
	}

	static bool BuildAndDeployAssetBundle(BuildTarget buildTarget)
	{
		var bundleFileName = GetBundleFileName(buildTarget);
		if (bundleFileName == null)
		{
			Debug.LogError($"Unsupported standalone build target: {buildTarget}");
			return false;
		}

		var projectRoot = Directory.GetParent(Application.dataPath).FullName;
		var relativeOutputPath = Path.Combine("Assets/AssetBundles", buildTarget.ToString());
		var absoluteOutputPath = Path.Combine(projectRoot, relativeOutputPath);
		var bundleBuilds = CreateBundleBuilds(bundleFileName);

		if (bundleBuilds == null)
			return false;

		PreBuildDirectoryCheck(absoluteOutputPath);
		var manifest = BuildPipeline.BuildAssetBundles(relativeOutputPath, bundleBuilds, BuildAssetBundleOptions.None, buildTarget);

		if (manifest == null)
		{
			Debug.LogError($"AssetBundle build failed for {buildTarget}.");
			return false;
		}

		var builtBundlePath = Path.Combine(absoluteOutputPath, bundleFileName);
		if (!File.Exists(builtBundlePath))
		{
			Debug.LogError($"Expected AssetBundle was not written: {builtBundlePath}");
			return false;
		}

		var modRoot = FindModRoot(projectRoot);
		if (modRoot == null)
		{
			Debug.LogError("Could not locate the RimWorld mod root. Expected a parent directory containing About/About.xml.");
			return false;
		}

		var resourcesDirectory = Path.Combine(modRoot, "Resources");
		PreBuildDirectoryCheck(resourcesDirectory);
		var deployedBundlePath = Path.Combine(resourcesDirectory, bundleFileName);
		File.Copy(builtBundlePath, deployedBundlePath, true);

		Debug.Log($"AssetBundle '{bundleFileName}' for {buildTarget} built to: {builtBundlePath}");
		Debug.Log($"Deployed AssetBundle to: {deployedBundlePath}");
		return true;
	}

	static AssetBundleBuild[] CreateBundleBuilds(string bundleFileName)
	{
		var missingAssets = BundleAssets
			.Where(assetPath => AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
			.ToArray();

		if (missingAssets.Length > 0)
		{
			Debug.LogError("AssetBundle build is missing required assets:\n" + string.Join("\n", missingAssets));
			return null;
		}

		return new[]
		{
			new AssetBundleBuild
			{
				assetBundleName = bundleFileName,
				assetNames = BundleAssets,
			}
		};
	}

	static void PreBuildDirectoryCheck(string directory)
	{
		if (!Directory.Exists(directory))
			Directory.CreateDirectory(directory);
	}

	static string FindModRoot(string projectRoot)
	{
		var current = new DirectoryInfo(projectRoot);
		while (current != null)
		{
			if (File.Exists(Path.Combine(current.FullName, "About", "About.xml")))
				return current.FullName;

			current = current.Parent;
		}

		return null;
	}

	static bool IsSupportedStandaloneTarget(BuildTarget buildTarget)
	{
		return GetBundleFileName(buildTarget) != null;
	}

	static string GetBundleFileName(BuildTarget buildTarget)
	{
		switch (buildTarget)
		{
			case BuildTarget.StandaloneWindows64:
				return $"{BundleName}-win";
			case BuildTarget.StandaloneLinux64:
				return $"{BundleName}-linux";
			case BuildTarget.StandaloneOSX:
				return $"{BundleName}-mac";
			default:
				return null;
		}
	}
}
