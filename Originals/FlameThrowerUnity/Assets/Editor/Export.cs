using System.IO;
using System.Collections.Generic;
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
	static readonly string[] ExpectedAssetNames =
	{
		"assets/fire.prefab",
		"assets/smoke.prefab",
		"assets/blockcube.prefab",
		"assets/fireblock.shader",
		"assets/particleunlit.shader",
	};

	static string DeploymentDir(string modRoot)
	{
		var env = System.Environment.GetEnvironmentVariable("ZEFLAMMENWERFER_RESOURCES_DIR");
		if (string.IsNullOrEmpty(env) == false)
			return env;

		return Path.Combine(modRoot, "Resources");
	}

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
			if (Application.isBatchMode == false)
				EditorUtility.RevealInFinder(DeploymentDir(FindModRoot(Directory.GetParent(Application.dataPath).FullName)));
		}
	}

	[MenuItem("Assets/Build Current Platform AssetBundle")]
	public static void BuildCurrentMachineAssetBundle()
	{
		BuildAndDeployAssetBundle(CurrentBuildTarget());
	}

	public static void BuildWin64AssetBundle()
	{
		BuildAndDeployAssetBundle(BuildTarget.StandaloneWindows64);
	}

	public static void BuildLinuxAssetBundle()
	{
		BuildAndDeployAssetBundle(BuildTarget.StandaloneLinux64);
	}

	public static void BuildMacOSAssetBundle()
	{
		BuildAndDeployAssetBundle(BuildTarget.StandaloneOSX);
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
		if (Application.isBatchMode == false)
			EditorUtility.RevealInFinder(DeploymentDir(modRoot));
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

		var resourcesDirectory = DeploymentDir(modRoot);
		PreBuildDirectoryCheck(resourcesDirectory);
		var deployedBundlePath = Path.Combine(resourcesDirectory, bundleFileName);
		File.Copy(builtBundlePath, deployedBundlePath, true);
		ValidateBundle(GetArchitectureName(buildTarget), deployedBundlePath);

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

	static BuildTarget CurrentBuildTarget()
	{
		switch (Application.platform)
		{
			case RuntimePlatform.WindowsEditor:
				return BuildTarget.StandaloneWindows64;
			case RuntimePlatform.LinuxEditor:
				return BuildTarget.StandaloneLinux64;
			case RuntimePlatform.OSXEditor:
				return BuildTarget.StandaloneOSX;
			default:
				throw new System.Exception($"Unsupported Unity editor platform for Ze Flammenwerfer asset bundle build: {Application.platform}");
		}
	}

	static string GetArchitectureName(BuildTarget buildTarget)
	{
		switch (buildTarget)
		{
			case BuildTarget.StandaloneWindows64:
				return "Win64";
			case BuildTarget.StandaloneLinux64:
				return "Linux";
			case BuildTarget.StandaloneOSX:
				return "MacOS";
			default:
				return buildTarget.ToString();
		}
	}

	static void ValidateBundle(string arch, string path)
	{
		var bundle = AssetBundle.LoadFromFile(path);
		if (bundle == null)
			throw new System.Exception($"Could not load asset bundle {path}");

		var actualNames = new HashSet<string>(bundle.GetAllAssetNames());
		var missingNames = ExpectedAssetNames.Where(name => actualNames.Contains(name) == false).ToArray();
		if (missingNames.Length > 0)
			throw new System.Exception($"Ze Flammenwerfer bundle {arch} is missing assets: {string.Join(", ", missingNames)}");

		var fire = RequireAsset<GameObject>(bundle, arch, ExpectedAssetNames[0]);
		var smoke = RequireAsset<GameObject>(bundle, arch, ExpectedAssetNames[1]);
		var blockCube = RequireAsset<GameObject>(bundle, arch, ExpectedAssetNames[2]);
		var fireBlock = RequireAsset<Shader>(bundle, arch, ExpectedAssetNames[3]);
		var particleUnlit = RequireAsset<Shader>(bundle, arch, ExpectedAssetNames[4]);

		Debug.Log($"Ze Flammenwerfer bundle validated {arch}: Fire={fire.name}, Smoke={smoke.name}, BlockCube={blockCube.name}, FireBlock={fireBlock.name}, ParticleUnlit={particleUnlit.name}, assets={actualNames.Count}, Unity={Application.unityVersion}, path={path}");
		bundle.Unload(false);
	}

	static T RequireAsset<T>(AssetBundle bundle, string arch, string assetName) where T : UnityEngine.Object
	{
		var asset = bundle.LoadAsset<T>(assetName);
		if (asset == null)
			throw new System.Exception($"Ze Flammenwerfer bundle {arch} could not load {assetName} as {typeof(T).Name}");
		return asset;
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
