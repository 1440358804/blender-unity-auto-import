using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AcePro.Editor;

namespace AcePro.Tests.BlenderUnityImport
{
    public sealed class BlenderUnityAutoImportTests
    {
        private const string FixtureRoot = "Assets/Game/Tests/EditMode/BlenderUnityImport/Fixtures";

        [SetUp]
        public void SetUp()
        {
            if (AssetDatabase.IsValidFolder(FixtureRoot)) AssetDatabase.DeleteAsset(FixtureRoot);
            AssetDatabase.CreateFolder("Assets/Game/Tests/EditMode/BlenderUnityImport", "Fixtures");
            AssetDatabase.CreateFolder(FixtureRoot, "Search");
            AssetDatabase.CreateFolder(FixtureRoot + "/Search", "Nested");
            AssetDatabase.CreateFolder(FixtureRoot, "Other");
            AssetDatabase.CreateFolder(FixtureRoot, "Output");
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(FixtureRoot)) AssetDatabase.DeleteAsset(FixtureRoot);
        }

        [Test]
        public void MaterialSearchIsExactAndRestrictedToSelectedDirectory()
        {
            CreateMaterial(FixtureRoot + "/Search/Body.mat", "Body");
            CreateMaterial(FixtureRoot + "/Other/Body.mat", "Body");
            CreateMaterial(FixtureRoot + "/Search/body-lowercase.mat", "body");
            AssetDatabase.Refresh();

            var matches = BlenderUnityAutoImport.FindExactMaterials(FixtureRoot + "/Search", "Body");

            Assert.That(matches, Has.Count.EqualTo(1));
            Assert.That(AssetDatabase.GetAssetPath(matches[0]), Is.EqualTo(FixtureRoot + "/Search/Body.mat"));
            Assert.That(BlenderUnityAutoImport.FindExactMaterials(FixtureRoot + "/Search", "Missing"), Is.Empty);
            Assert.That(BlenderUnityAutoImport.FindExactMaterials(FixtureRoot + "/Search", "body"), Is.Empty);
            Assert.That(BlenderUnityAutoImport.FindExactMaterials("Assets/../Packages", "Body"), Is.Empty);

            CreateMaterial(FixtureRoot + "/Search/Nested/Body.mat", "Body");
            AssetDatabase.Refresh();
            Assert.That(BlenderUnityAutoImport.FindExactMaterials(FixtureRoot + "/Search", "Body"), Has.Count.EqualTo(2));
        }

        [Test]
        public void ImportSettingsMatchModelPathWithoutWindowsCaseSensitivity()
        {
            var settings = new BlenderUnityImportSettings
            {
                schemaVersion = 1,
                modelAssetPath = "assets/game/tests/editmode/blenderunityimport/fixtures/model.fbx",
                materialSearchDirectory = "assets/game/tests/editmode/blenderunityimport/fixtures/search",
                prefabDirectory = "assets/game/tests/editmode/blenderunityimport/fixtures/output",
                prefabName = "Model"
            };
            AssetDatabase.Refresh();
            string normalizedPath = "Assets/Game/Tests/EditMode/BlenderUnityImport/Fixtures/Model.fbx";
            var validate = typeof(BlenderUnityAutoImport).GetMethod(
                "ValidateSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            Assert.That(validate, Is.Not.Null);
            Assert.DoesNotThrow(() => validate.Invoke(null, new object[] { settings, normalizedPath }));
            Assert.That(settings.materialSearchDirectory, Is.EqualTo(FixtureRoot + "/Search"));
            Assert.That(settings.prefabDirectory, Is.EqualTo(FixtureRoot + "/Output"));
        }

        [Test]
        public void MateriallessImporterSkipsItsOwnReimportUntilInputsChange()
        {
            string modelPath = FixtureRoot + "/Model.bin";
            string settingsPath = BlenderUnityAutoImport.SettingsPath(modelPath);
            string ownerPath = FixtureRoot + "/Output/Model.prefab.acepro-owner.json";
            File.WriteAllText(modelPath, "fbx-v1");
            File.WriteAllText(settingsPath, "settings-v1");
            File.WriteAllText(ownerPath,
                "{\"schemaVersion\":1,\"modelAssetPath\":\"" + modelPath + "\",\"modelContentHash\":\"" + HashFile(modelPath) +
                "\",\"settingsContentHash\":\"" + HashFile(settingsPath) + "\"}");

            var processed = typeof(BlenderUnityAutoImport).GetMethod(
                "HasProcessedCurrentInputs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(processed, Is.Not.Null);
            object[] arguments = { ownerPath, modelPath, settingsPath };
            Assert.That(processed.Invoke(null, arguments), Is.True);

            File.WriteAllText(modelPath, "fbx-v2");
            Assert.That(processed.Invoke(null, arguments), Is.False);
            File.WriteAllText(modelPath, "fbx-v1");
            File.WriteAllText(settingsPath, "settings-v2");
            Assert.That(processed.Invoke(null, arguments), Is.False);
        }

        [Test]
        public void CompletedImportSettingsManifestIsDeletedWithItsMetaFile()
        {
            string modelPath = FixtureRoot + "/Model.fbx";
            string settingsPath = BlenderUnityAutoImport.SettingsPath(modelPath);
            File.WriteAllText(settingsPath, "{}");
            AssetDatabase.Refresh();

            var deleteManifest = typeof(BlenderUnityAutoImport).GetMethod(
                "DeleteSettingsManifest", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            Assert.That(deleteManifest, Is.Not.Null);
            Assert.That(File.Exists(settingsPath), Is.True);
            deleteManifest.Invoke(null, new object[] { settingsPath });

            Assert.That(File.Exists(settingsPath), Is.False);
            Assert.That(File.Exists(settingsPath + ".meta"), Is.False);
        }

        [Test]
        public void EachMeshBearingObjectGetsOneLocalBoundsColliderAndEmptyNodesGetNone()
        {
            var root = new GameObject("Model");
            var first = CreateMeshPart(root.transform, "PartA", new Vector3(-2, -1, -3), new Vector3(2, 1, 3));
            var second = CreateMeshPart(root.transform, "PartB", new Vector3(-.25f, -.5f, -.75f), new Vector3(.25f, .5f, .75f));
            new GameObject("Empty").transform.SetParent(root.transform, false);
            try
            {
                Assert.That(root.GetComponentsInChildren<Renderer>(true), Has.Length.EqualTo(2));
                BlenderUnityAutoImport.AddPartColliders(root);
                BlenderUnityAutoImport.AddPartColliders(root);

                BoxCollider firstCollider = first.GetComponent<BoxCollider>();
                BoxCollider secondCollider = second.GetComponent<BoxCollider>();
                Assert.That(firstCollider, Is.Not.Null);
                Assert.That(secondCollider, Is.Not.Null);
                Assert.That(first.GetComponents<BoxCollider>(), Has.Length.EqualTo(1));
                Assert.That(second.GetComponents<BoxCollider>(), Has.Length.EqualTo(1));
                Assert.That(firstCollider.center, Is.EqualTo(Vector3.zero));
                Assert.That(firstCollider.size, Is.EqualTo(new Vector3(4, 2, 6)));
                Assert.That(secondCollider.size, Is.EqualTo(new Vector3(.5f, 1, 1.5f)));
                Assert.That(root.transform.Find("Empty").GetComponent<BoxCollider>(), Is.Null);
            }
            finally { DestroyFixture(root); }
        }

        [Test]
        public void PlanarMeshGetsMinimumColliderThickness()
        {
            var root = new GameObject("Model");
            var part = CreateMeshPart(root.transform, "Plane", Vector3.zero, new Vector3(2, 0, 3));
            try
            {
                BlenderUnityAutoImport.AddPartColliders(root);
                Assert.That(part.GetComponent<BoxCollider>().size, Is.EqualTo(new Vector3(2, .001f, 3)));
            }
            finally { DestroyFixture(root); }
        }

        [Test]
        public void UnityCanAddBoxColliderToMeshPartInEditMode()
        {
            var root = new GameObject("Model");
            var part = CreateMeshPart(root.transform, "Part", Vector3.zero, Vector3.one);
            try
            {
                BoxCollider collider = part.AddComponent<BoxCollider>();
                Assert.That(collider, Is.Not.Null);
                Assert.That(collider.gameObject, Is.SameAs(part));
                collider.center = Vector3.zero;
            }
            finally { DestroyFixture(root); }
        }

        private static GameObject CreateMeshPart(Transform parent, string name, Vector3 min, Vector3 max)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            var mesh = new Mesh { name = name + "Mesh" };
            mesh.vertices = new[]
            {
                new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
                new Vector3(min.x, max.y, min.z), new Vector3(min.x, min.y, max.z)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>();
            return part;
        }

        private static void CreateMaterial(string path, string materialName)
        {
            var material = new Material(Shader.Find("Standard")) { name = materialName };
            AssetDatabase.CreateAsset(material, path);
        }

        private static string HashFile(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return System.BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private static void DestroyFixture(GameObject root)
        {
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null) Object.DestroyImmediate(filter.sharedMesh);
            Object.DestroyImmediate(root);
        }
    }
}
