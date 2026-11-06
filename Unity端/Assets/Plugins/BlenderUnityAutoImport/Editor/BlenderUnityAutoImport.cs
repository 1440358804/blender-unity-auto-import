using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace AcePro.Editor
{
    [Serializable]
    public sealed class BlenderUnityImportSettings
    {
        public int schemaVersion;
        public string modelAssetPath;
        public string materialSearchDirectory;
        public string prefabDirectory;
        public string prefabName;
    }

    [Serializable]
    internal sealed class BlenderUnityPrefabOwner
    {
        public int schemaVersion;
        public string modelAssetPath;
        public string modelContentHash;
        public string settingsContentHash;
    }

    public static class BlenderUnityAutoImport
    {
        public const string SettingsSuffix = ".acepro-import.json";
        private const string OwnerSuffix = ".acepro-owner.json";

        public static string SettingsPath(string modelAssetPath) => Normalize(modelAssetPath) + SettingsSuffix;

        public static string PrefabAssetPath(BlenderUnityImportSettings settings) =>
            Normalize(settings.prefabDirectory) + "/" + settings.prefabName + ".prefab";

        public static List<Material> FindExactMaterials(string directory, string materialName)
        {
            if (!IsAssetsPath(directory) || string.IsNullOrWhiteSpace(materialName))
                return new List<Material>();
            return AssetDatabase.FindAssets("t:Material", new[] { Normalize(directory) })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => AssetDatabase.LoadAssetAtPath<Material>(path))
                .Where(material => material != null && string.Equals(material.name, materialName, StringComparison.Ordinal))
                .OrderBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal)
                .ToList();
        }

        public static void AddPartColliders(GameObject root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            foreach (GameObject part in root.GetComponentsInChildren<Renderer>(true)
                         .Where(renderer => renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                         .Select(renderer => renderer.gameObject).Distinct())
            {
                var bounds = new Bounds();
                bool hasBounds = false;
                foreach (Renderer renderer in part.GetComponents<Renderer>())
                {
                    Bounds localBounds;
                    if (renderer is SkinnedMeshRenderer skinned)
                    {
                        if (skinned.sharedMesh == null)
                            throw new InvalidOperationException("Skinned renderer has no mesh: " + GetHierarchyPath(part.transform));
                        localBounds = skinned.localBounds;
                    }
                    else
                    {
                        MeshFilter filter = part.GetComponent<MeshFilter>();
                        if (filter == null || filter.sharedMesh == null)
                            throw new InvalidOperationException("Mesh renderer has no mesh filter: " + GetHierarchyPath(part.transform));
                        localBounds = filter.sharedMesh.bounds;
                    }

                    if (!hasBounds)
                    {
                        bounds = localBounds;
                        hasBounds = true;
                    }
                    else bounds.Encapsulate(localBounds);
                }

                if (!hasBounds || !IsFinite(bounds.size.x) || !IsFinite(bounds.size.y) || !IsFinite(bounds.size.z))
                    throw new InvalidOperationException("Cannot create a valid BoxCollider for: " + GetHierarchyPath(part.transform));
                Vector3 size = bounds.size;
                size.x = Mathf.Max(size.x, .001f);
                size.y = Mathf.Max(size.y, .001f);
                size.z = Mathf.Max(size.z, .001f);
                bounds.size = size;

                BoxCollider collider = part.GetComponent<BoxCollider>();
                if (collider == null)
                {
                    part.AddComponent<BoxCollider>();
                    collider = part.GetComponent<BoxCollider>();
                }
                if (collider == null)
                    throw new InvalidOperationException("Unity did not attach a BoxCollider to: " + GetHierarchyPath(part.transform));
                collider.center = bounds.center;
                collider.size = bounds.size;
                collider.isTrigger = false;
            }
        }

        public static void ProcessModelAsset(string modelAssetPath)
        {
            modelAssetPath = Normalize(modelAssetPath);
            string settingsPath = SettingsPath(modelAssetPath);
            if (!File.Exists(ToAbsolutePath(settingsPath))) return;

            BlenderUnityImportSettings settings = JsonUtility.FromJson<BlenderUnityImportSettings>(
                File.ReadAllText(ToAbsolutePath(settingsPath)));
            ValidateSettings(settings, modelAssetPath);

            var importer = AssetImporter.GetAtPath(modelAssetPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("FBX ModelImporter is unavailable: " + modelAssetPath);
            string ownerPath = PrefabAssetPath(settings) + OwnerSuffix;
            if (importer.materialImportMode == ModelImporterMaterialImportMode.None)
            {
                if (HasProcessedCurrentInputs(ownerPath, modelAssetPath, settingsPath))
                {
                    DeleteSettingsManifest(settingsPath);
                    return;
                }
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
                return;
            }
            if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
                return;
            }

            bool remapChanged = ApplyMaterialRemaps(importer, modelAssetPath, settings.materialSearchDirectory);
            if (remapChanged)
            {
                importer.SaveAndReimport();
                return;
            }

            CreateOrUpdatePrefab(modelAssetPath, settings);
            importer = AssetImporter.GetAtPath(modelAssetPath) as ModelImporter;
            if (importer != null && importer.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
            }
            DeleteSettingsManifest(settingsPath);
        }

        private static void DeleteSettingsManifest(string settingsPath)
        {
            if (!File.Exists(ToAbsolutePath(settingsPath))) return;
            if (!AssetDatabase.DeleteAsset(settingsPath))
                throw new IOException("Could not delete completed Blender import settings: " + settingsPath);
        }

        private static bool ApplyMaterialRemaps(ModelImporter importer, string modelPath, string materialDirectory)
        {
            bool changed = false;
            Dictionary<AssetImporter.SourceAssetIdentifier, UnityEngine.Object> current = importer.GetExternalObjectMap();
            foreach (Material source in AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>())
            {
                List<Material> matches = FindExactMaterials(materialDirectory, source.name);
                if (matches.Count > 1)
                {
                    string paths = string.Join(", ", matches.Select(AssetDatabase.GetAssetPath));
                    throw new InvalidOperationException("Ambiguous material name '" + source.name + "' in " + materialDirectory + ": " + paths);
                }
                if (matches.Count == 0)
                {
                    Debug.LogWarning("[Blender to Unity] No matching project material for '" + source.name + "' in " + materialDirectory +
                                     "; keeping the FBX-imported material. No material asset was created.");
                    continue;
                }

                var key = new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name);
                if (current.TryGetValue(key, out UnityEngine.Object mapped) && mapped == matches[0]) continue;
                importer.AddRemap(key, matches[0]);
                changed = true;
            }
            return changed;
        }

        private static void CreateOrUpdatePrefab(string modelPath, BlenderUnityImportSettings settings)
        {
            string prefabPath = PrefabAssetPath(settings);
            EnsureAssetFolder(settings.prefabDirectory);
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            string ownerPath = prefabPath + OwnerSuffix;
            if (existing != null && !HasMatchingOwner(ownerPath, modelPath))
                throw new InvalidOperationException("Refusing to overwrite a Prefab not owned by this importer: " + prefabPath);

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) throw new InvalidOperationException("Imported FBX root is unavailable: " + modelPath);
            GameObject instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (instance == null) throw new InvalidOperationException("Cannot instantiate imported FBX: " + modelPath);
            try
            {
                if (PrefabUtility.IsPartOfPrefabInstance(instance))
                    PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.name = settings.prefabName;
                AddPartColliders(instance);
                if (PrefabUtility.SaveAsPrefabAsset(instance, prefabPath) == null)
                    throw new InvalidOperationException("Unity did not save the generated Prefab: " + prefabPath);
                WriteOwner(ownerPath, modelPath, SettingsPath(modelPath));
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        private static bool HasMatchingOwner(string ownerPath, string modelPath)
        {
            string absolute = ToAbsolutePath(ownerPath);
            if (!File.Exists(absolute)) return false;
            try
            {
                BlenderUnityPrefabOwner owner = JsonUtility.FromJson<BlenderUnityPrefabOwner>(File.ReadAllText(absolute));
                return owner != null && owner.schemaVersion == 1 && Normalize(owner.modelAssetPath) == modelPath;
            }
            catch { return false; }
        }

        private static void WriteOwner(string ownerPath, string modelPath, string settingsPath)
        {
            var owner = new BlenderUnityPrefabOwner
            {
                schemaVersion = 1,
                modelAssetPath = modelPath,
                modelContentHash = GetFileHash(ToAbsolutePath(modelPath)),
                settingsContentHash = GetFileHash(ToAbsolutePath(settingsPath))
            };
            File.WriteAllText(ToAbsolutePath(ownerPath), JsonUtility.ToJson(owner, true));
            AssetDatabase.ImportAsset(ownerPath, ImportAssetOptions.ForceUpdate);
        }

        private static bool HasProcessedCurrentInputs(string ownerPath, string modelPath, string settingsPath)
        {
            string absoluteOwnerPath = ToAbsolutePath(ownerPath);
            if (!File.Exists(absoluteOwnerPath)) return false;
            try
            {
                BlenderUnityPrefabOwner owner = JsonUtility.FromJson<BlenderUnityPrefabOwner>(File.ReadAllText(absoluteOwnerPath));
                return owner != null && owner.schemaVersion == 1 &&
                       string.Equals(Normalize(owner.modelAssetPath), Normalize(modelPath), StringComparison.OrdinalIgnoreCase) &&
                       owner.modelContentHash == GetFileHash(ToAbsolutePath(modelPath)) &&
                       owner.settingsContentHash == GetFileHash(ToAbsolutePath(settingsPath));
            }
            catch { return false; }
        }

        private static string GetFileHash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private static void ValidateSettings(BlenderUnityImportSettings settings, string modelPath)
        {
            if (settings == null || settings.schemaVersion != 1)
                throw new InvalidOperationException("Unsupported or invalid Blender import settings: " + SettingsPath(modelPath));
            if (!IsAssetsPath(modelPath) || !modelPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Normalize(settings.modelAssetPath), modelPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Import settings model path does not match the FBX: " + modelPath);
            if (!IsAssetsPath(settings.materialSearchDirectory))
                throw new InvalidOperationException("Material search directory must be an existing folder under Assets: " + settings.materialSearchDirectory);
            if (!IsAssetsPath(settings.prefabDirectory))
                throw new InvalidOperationException("Prefab directory must be under Assets: " + settings.prefabDirectory);
            settings.materialSearchDirectory = CanonicalizeAssetPath(settings.materialSearchDirectory);
            settings.prefabDirectory = CanonicalizeAssetPath(settings.prefabDirectory);
            if (!AssetDatabase.IsValidFolder(settings.materialSearchDirectory))
                throw new InvalidOperationException("Material search directory must be an existing folder under Assets: " + settings.materialSearchDirectory);
            if (string.IsNullOrWhiteSpace(settings.prefabName) || settings.prefabName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                settings.prefabName.Contains("/") || settings.prefabName.Contains("\\"))
                throw new InvalidOperationException("Invalid Prefab name: " + settings.prefabName);
        }

        private static void EnsureAssetFolder(string directory)
        {
            directory = Normalize(directory);
            if (!IsAssetsPath(directory)) throw new InvalidOperationException("Asset directory must be inside Assets: " + directory);
            string current = "Assets";
            foreach (string segment in directory.Substring("Assets".Length).Trim('/').Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries))
            {
                string next = current + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment);
                current = next;
            }
        }

        private static bool IsAssetsPath(string path)
        {
            path = Normalize(path);
            string[] segments = path.Split('/');
            if (segments.Length == 0 || !string.Equals(segments[0], "Assets", StringComparison.OrdinalIgnoreCase)) return false;
            return !segments.Any(segment => segment == "." || segment == "..");
        }

        private static string CanonicalizeAssetPath(string assetPath)
        {
            string[] segments = Normalize(assetPath).Split('/');
            var canonical = new List<string> { "Assets" };
            for (int i = 1; i < segments.Length; i++)
            {
                string segment = segments[i];
                string parentPath = string.Join("/", canonical);
                string matchingDirectory = AssetDatabase.GetSubFolders(parentPath)
                    .FirstOrDefault(path => string.Equals(Path.GetFileName(path), segment, StringComparison.OrdinalIgnoreCase));
                if (matchingDirectory != null) segment = Path.GetFileName(matchingDirectory);
                canonical.Add(segment);
            }
            return string.Join("/", canonical);
        }

        private static string ToAbsolutePath(string assetPath) => Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), Normalize(assetPath)));
        private static string Normalize(string path) => (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static string GetHierarchyPath(Transform transform)
        {
            var parts = new Stack<string>();
            for (Transform current = transform; current != null; current = current.parent) parts.Push(current.name);
            return string.Join("/", parts);
        }
    }

    internal sealed class BlenderUnityModelImportPostprocessor : AssetPostprocessor
    {
        private static readonly HashSet<string> Pending = new HashSet<string>(StringComparer.Ordinal);
        private static bool scheduled;

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string path in (importedAssets ?? Array.Empty<string>()).Concat(movedAssets ?? Array.Empty<string>()))
            {
                if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) && File.Exists(BlenderUnityAutoImport.SettingsPath(path)))
                    Pending.Add(path.Replace('\\', '/'));
                else if (path.EndsWith(BlenderUnityAutoImport.SettingsSuffix, StringComparison.OrdinalIgnoreCase))
                    Pending.Add(path.Substring(0, path.Length - BlenderUnityAutoImport.SettingsSuffix.Length));
            }
            if (Pending.Count == 0 || scheduled) return;
            scheduled = true;
            EditorApplication.delayCall += ProcessPending;
        }

        private static void ProcessPending()
        {
            scheduled = false;
            string[] work = Pending.ToArray();
            Pending.Clear();
            foreach (string modelPath in work)
            {
                try { BlenderUnityAutoImport.ProcessModelAsset(modelPath); }
                catch (Exception exception) { Debug.LogError("[Blender to Unity] Import failed for " + modelPath + ":\n" + exception); }
            }
        }
    }
}
