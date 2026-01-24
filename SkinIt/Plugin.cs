using _ScriptableObject;
using _Utility;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Cuttle;
using Dog_Scripts;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UI_Scripts.Panels;
using UnityEngine;
using UnityEngine.UIElements;

namespace SkinIt
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static Plugin Context;
        internal static new ManualLogSource Logger;
        internal static FieldInfo dogArtField = typeof(CardVisual).GetField("dogArt", BindingFlags.NonPublic | BindingFlags.Instance);

        private ConfigEntry<bool> ModEnabled;
        private ConfigEntry<string> skinName;
        //private ConfigEntry<KeyboardShortcut> spawnKey;
        internal static bool skinLoaded;
        // Map dog sizes to skins
        internal static Dictionary<int, Skin> sizeToSkin;
        private bool dogPrefabsPatched;

        /// <summary>
        /// Sprite and colliders for the skin of a dog.
        /// </summary>
        public struct Skin
        {
            public Sprite sprite;
            public Sprite cratedSprite;
            public Collider2D[] colliders;
        }

        public Plugin()
        {
            Context = this;

            ModEnabled = Config.Bind("General", "ModEnabled", true, new ConfigDescription("Enable/Disable this mod on startup"));
            skinName = Config.Bind("General", "SkinName", "suika", new ConfigDescription("Name of the skin to load"));
            //spawnKey = Config.Bind("General", "SpawnKey", new KeyboardShortcut(KeyCode.F6), new ConfigDescription("Spawn some dogs"));

            Logger = base.Logger;

            if (ModEnabled.Value)
            {
                LoadSkin(skinName.Value);

                if (skinLoaded)
                {
                    Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
                }
            }
        }

        /// <summary>
        /// Dog size to the skin prefab in the asset bundle.
        /// </summary>
        private static string sizeToSkinPrefab(int size)
        {
            switch(size)
            {
                case 1: return "ace";
                case 2: return "two";
                case 3: return "three";
                case 4: return "four";
                case 5: return "five";
                case 6: return "six";
                case 7: return "seven";
                case 8: return "eight";
                case 9: return "nine";
                case 10: return "ten";
                case 11: return "jack";
                case 12: return "queen";
                case 13: return "king";
                default:
                    return null;
            }
        }

        /// <summary>
        /// Load the skin from asset bundle <skinName>.asset.
        /// </summary>
        /// <param name="skinName">Name of the skin</param>
        private void LoadSkin(string skinName)
        {
            // Load the asset bundle
            var assetBundleName = $"{skinName}.assets";
            var assetBundlePath = Path.Combine(Path.GetDirectoryName(Info.Location), "skins", assetBundleName);
            if (!File.Exists(assetBundlePath))
            {
                Logger.LogError($"Asset bundle {assetBundlePath} not found");
                return;
            }

            var assetBundleBuffer = File.ReadAllBytes(assetBundlePath);
            AssetBundle assetBundle = AssetBundle.LoadFromMemory(assetBundleBuffer);
            Logger.LogInfo($"Asset bundle {assetBundleName} loaded");

            // Load the dog skins
            sizeToSkin = new Dictionary<int, Skin>();
            for (int size = 1; ; size++)
            {
                var dogPrefabName = Constants.SizeToPrefab(size);
                if (dogPrefabName == null)
                {
                    break;
                }

                var skinPrefabName = sizeToSkinPrefab(size);
                if (skinPrefabName == null)
                {
                    break;
                }
                
                var skinPrefab = assetBundle.LoadAsset<GameObject>(skinPrefabName);
                if (skinPrefab == null)
                {
                    Logger.LogWarning($"Prefab {skinPrefabName} not found in asset bundle {assetBundleName}");
                    continue;
                }

                var spriteRenderer = skinPrefab.GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    Logger.LogWarning($"Missing SpriteRenderer on prefab {skinPrefabName}");
                    continue;
                }

                var colliders = skinPrefab.GetComponentsInChildren<Collider2D>();
                if (colliders.Length == 0)
                {
                    Logger.LogWarning($"Missing Collider2D on prefab {skinPrefabName}");
                    continue;
                }

                var skin = new Skin();
                skin.sprite = spriteRenderer.sprite;
                skin.colliders = colliders;

                var cratedSpriteRenderer = skinPrefab.transform.Find("crated")?.GetComponent<SpriteRenderer>();
                if (cratedSpriteRenderer == null || cratedSpriteRenderer.sprite == null)
                {
                    Logger.LogWarning($"Missing crated Sprite on prefab {skinPrefabName}");
                } else { 
                    skin.cratedSprite = cratedSpriteRenderer.sprite;
                }

                sizeToSkin[size] = skin;
                Logger.LogInfo($"Prefab {skinPrefabName} loaded");
            }

            skinLoaded = true;
        }

        /// <summary>
        /// Try to patch until done.
        /// </summary>
        void LateUpdate()
        {
            if (!ModEnabled.Value) return;

            if (skinLoaded && !dogPrefabsPatched) {
                PatchDogPrefabs();
            }

            /*if (spawnKey.Value.IsPressed())
            {
                GimmeSomeDogs();
            }*/
        }

        /// <summary>
        /// Apply the skins to dog prefabs.
        /// </summary>
        private void PatchDogPrefabs()
        {
            // Check if the dog prefabs have been spawned
            var dogs = Resources.FindObjectsOfTypeAll<Dog>().Where(dog => Constants.PrefabToSize(dog.name) != -1).ToArray();
            if (dogs.Length == 0)
            {
                return;
            }

            foreach (var dog in dogs)
            {
                PatchDogPrefab(dog);
            }

            dogPrefabsPatched = true;
        }

        private static void PatchDogPrefab(Dog dog)
        {
            // Check the GameObject corresponds to a prefab
            var prefabName = dog.gameObject.name;
            var size = Constants.PrefabToSize(prefabName);
            if (size == -1)
            {
                return;
            }

            // Get the skin for the dog
            Skin skin;
            if (!sizeToSkin.TryGetValue(size, out skin))
            {
                Logger.LogWarning($"No skin for dog size {size} of prefab {prefabName}");
                return;
            }

            // Replace the sprite
            dog.GetComponent<SpriteRenderer>()?.sprite = skin.sprite;
            dog.GetComponent<Animator>()?.enabled = false;
            dog.GetComponent<YardDogAnimator>()?.enabled = false;

            // Find the game object with the colliders
            var colliderGameObject = dog.GetComponentInChildren<PhysicsAudioPlayer>()?.gameObject;
            if (colliderGameObject == null)
            {
                Logger.LogWarning($"No collider object found on prefab {prefabName}");
                return;
            }

            // Disable the colliders
            foreach (var collider in colliderGameObject.GetComponents<Collider2D>())
            {
                collider.enabled = false;
            }

            // Get the collider material
            var colliderMaterial = colliderGameObject.GetComponent<SetAllColliderMaterial>()?.Mat;

            // Add the ones from the skin to a separate GameObject
            var skinColliderGameObject = dog.transform.Find("skinCollider")?.gameObject;
            if (skinColliderGameObject == null)
            {
                skinColliderGameObject = new GameObject("skinCollider");
            }
            skinColliderGameObject.layer = colliderGameObject.layer;
            skinColliderGameObject.transform.parent = colliderGameObject.transform.parent;
            skinColliderGameObject.transform.localPosition = colliderGameObject.transform.localPosition;
            skinColliderGameObject.transform.localRotation = colliderGameObject.transform.localRotation;
            skinColliderGameObject.transform.localScale = colliderGameObject.transform.localScale;

            foreach (var skinPrefabCollider in skin.colliders)
            {
                if (skinPrefabCollider is CircleCollider2D)
                {
                    var skinCollider = skinColliderGameObject.gameObject.AddComponent<CircleCollider2D>();
                    skinCollider.radius = (skinPrefabCollider as CircleCollider2D).radius;
                    skinCollider.offset = skinPrefabCollider.offset;
                    skinCollider.sharedMaterial = colliderMaterial;
                }
                else if (skinPrefabCollider is BoxCollider2D)
                {
                    var skinCollider = skinColliderGameObject.gameObject.AddComponent<BoxCollider2D>();
                    skinCollider.size = (skinPrefabCollider as BoxCollider2D).size;
                    skinCollider.edgeRadius = (skinPrefabCollider as BoxCollider2D).edgeRadius;
                    skinCollider.offset = skinPrefabCollider.offset;
                    skinCollider.sharedMaterial = colliderMaterial;
                }
            }

            // Patch the crated sprite
            if (skin.cratedSprite != null)
            {
                // Disable the original SpriteRenderer
                var cratedGameObject = dog.transform.Find("Crated");
                if (cratedGameObject != null)
                {
                    var cratedSpriteRenderer = cratedGameObject.GetComponent<SpriteRenderer>();
                    cratedSpriteRenderer.enabled = false;

                    var skinCratedGameObject = new GameObject("skinCrated");
                    skinCratedGameObject.transform.parent = cratedGameObject.transform;
                    skinCratedGameObject.transform.localPosition = Vector3.zero;
                    skinCratedGameObject.transform.localScale = Vector3.one;
                    skinCratedGameObject.transform.localRotation = Quaternion.identity;
                    skinCratedGameObject.layer = cratedGameObject.gameObject.layer;
                    var skinCratedSpriteRenderer = skinCratedGameObject.AddComponent<SpriteRenderer>();
                    skinCratedSpriteRenderer.sprite = skin.cratedSprite;
                    skinCratedSpriteRenderer.sortingGroupID = cratedSpriteRenderer.sortingGroupID;
                    skinCratedSpriteRenderer.sortingGroupOrder = cratedSpriteRenderer.sortingGroupOrder;
                    skinCratedSpriteRenderer.sortingOrder = cratedSpriteRenderer.sortingOrder;
                    skinCratedSpriteRenderer.sortingLayerID = cratedSpriteRenderer.sortingLayerID;
                    skinCratedSpriteRenderer.sortingLayerName = cratedSpriteRenderer.sortingLayerName;
                    skinCratedGameObject.AddComponent<OverlayMask>();
                }
            }

            Logger.LogInfo($"Dog prefab patched for {prefabName}");
        }

        /// <summary>
        /// Spawn all 13 dogs for debug purpose.
        /// </summary>
        private void GimmeSomeDogs()
        {
            for (var i = 13; i >= 1; --i)
            {
                string prefab = Constants.SizeToPrefab(i);
                string image = Constants.SizeToImage(i);
                if (string.IsNullOrEmpty(prefab))
                    return;

                CardData cardData = new CardData()
                {
                    BaseDogPrefab = prefab,
                    Image = image
                };
                Vector3 position = new Vector3(UnityEngine.Random.Range(2.5f, -2.5f), UnityEngine.Random.Range(10f, 20f), 0.0f);
                Spawning.SpawnEntity(cardData, position);
            }
        }

        /// <summary>
        /// Override the dog sprite shown on card with the skin sprite.
        /// </summary>
        [HarmonyPatch(typeof(CardVisual), "UpdateVisuals")]
        static class CardVisual_UpdateVisuals_Patch
        {
            static void Postfix(CardVisual __instance)
            {
                // Replace the foregroundSprite of the card
                var spriteName = __instance.foregroundSprite?.name;
                if (spriteName == null || Constants.ImageToSize(spriteName) == -1)
                {
                    return;
                }

                Skin skin;
                if (!sizeToSkin.TryGetValue(__instance.parentCard.Data.Size, out skin))
                {
                    return;
                }

                __instance.foregroundSprite = skin.sprite;

                // Replace the sprite of dogArt.
                // dogArt is the dog you drop in the yard
                var dogArt = dogArtField.GetValue(__instance) as UnityEngine.UI.Image;
                if (dogArt != null)
                {
                    dogArt.sprite = skin.sprite;
                }
            }
        }

        /// <summary>
        /// Add a component to disable the dogArt animator on cards.
        /// This is required because the sprite is reset if enabled back.
        /// </summary>
        [HarmonyPatch(typeof(CardVisual), "Awake")]
        static class CardVisual_Awake_Patch
        {
            static void Postfix(CardVisual __instance)
            {
                if (__instance.GetComponent<DisableAnimator>() == null)
                {
                    var animator = (dogArtField.GetValue(__instance) as UnityEngine.UI.Image)?.gameObject?.GetComponent<Animator>();
                    if (animator != null)
                    {
                        var component = __instance.gameObject.AddComponent<DisableAnimator>();
                        component.animator = animator;
                    }
                }
            }
        }

        class DisableAnimator : MonoBehaviour
        {
            public Animator animator;

            void Update()
            {
                animator?.enabled = false;
            }
        }

        /// <summary>
        /// For the glossary it is simpler hook OnEnable.
        /// </summary>
        [HarmonyPatch(typeof(GlossaryPanel), "OnEnable")]
        static class GlossaryPanel_OnEnable_Patch
        {
            static void Postfix(GlossaryPanel __instance)
            {
                // The dogs panel has a list of GameObjects named ace, two, three, ..., each with an Image.
                // The simpler is to go through the list and check if the name of a GameObject corresponds to a dog.
                Skin skin;
                foreach(var image in __instance.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                {
                    // The name is in lowercase and must be capitalized
                    var size = Constants.LabelToSize(image.gameObject.name.ToPascalCase());
                    if (size == -1)
                    {
                        continue;
                    }

                    if (!sizeToSkin.TryGetValue(size, out skin))
                    {
                        Logger.LogWarning($"Glossary missing skin for {image.gameObject.name}");
                        continue;
                    }

                    image.sprite = skin.sprite;
                    image.SetNativeSize();
                    Logger.LogInfo($"Glossary patched for {image.gameObject.name}");
                }

                Logger.LogInfo("Glossary patched");
            }
        }

    }
}
