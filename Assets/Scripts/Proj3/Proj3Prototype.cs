using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Proj3
{
    public interface IImageSource
    {
        Texture2D GetImage();
    }

    [Serializable]
    public class SegmentedItemData
    {
        public string id;
        public string label;
        public Rect normalizedBounds;
        public Color tint = Color.white;
        public float confidence = 1f;
        public string cropPath;
        public string maskPath;
        public string[] relatedItemIds = Array.Empty<string>();
    }

    [Serializable]
    public class SegmentationFile
    {
        public string sceneId;
        public string imagePath;
        public SegmentationFileItem[] items = Array.Empty<SegmentationFileItem>();
        public InteractionEdgeData[] interactions = Array.Empty<InteractionEdgeData>();
    }

    [Serializable]
    public class SegmentationFileItem
    {
        public string id;
        public string label;
        public NormalizedBounds bounds;
        public float x;
        public float y;
        public float width;
        public float height;
        public float confidence = 1f;
        public string cropPath;
        public string maskPath;
        public string[] relatedItemIds = Array.Empty<string>();
    }

    [Serializable]
    public class NormalizedBounds
    {
        public float x;
        public float y;
        public float width;
        public float height;

        public Rect ToRect()
        {
            return new Rect(x, y, width, height);
        }
    }

    [Serializable]
    public class InteractionEdgeData
    {
        public string sourceId;
        public string targetId;
        public string action;
        public string instruction;
    }

    public class StaticImageSource : MonoBehaviour, IImageSource
    {
        [SerializeField] private Texture2D imageAsset;
        [SerializeField] private string streamingAssetsRelativePath = "Proj3/test-image.png";
        [SerializeField] private int fallbackTextureSize = 512;

        private Texture2D loadedImage;

        public Texture2D GetImage()
        {
            if (imageAsset != null) return imageAsset;
            if (loadedImage != null) return loadedImage;

            string path = Path.Combine(Application.streamingAssetsPath, streamingAssetsRelativePath);
            if (File.Exists(path))
            {
                byte[] bytes = File.ReadAllBytes(path);
                loadedImage = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (loadedImage.LoadImage(bytes))
                {
                    loadedImage.name = "Proj3 Uploaded Image";
                    loadedImage.wrapMode = TextureWrapMode.Clamp;
                    loadedImage.filterMode = FilterMode.Bilinear;
                    return loadedImage;
                }
            }

            loadedImage = CreateFallbackTexture(fallbackTextureSize);
            return loadedImage;
        }

        private static Texture2D CreateFallbackTexture(int size)
        {
            int clampedSize = Mathf.Max(64, size);
            Texture2D texture = new Texture2D(clampedSize, clampedSize, TextureFormat.RGBA32, false);
            texture.name = "Proj3 Fallback Test Image";

            Color[] pixels = new Color[clampedSize * clampedSize];
            for (int y = 0; y < clampedSize; y++)
            {
                for (int x = 0; x < clampedSize; x++)
                {
                    float u = x / (float)(clampedSize - 1);
                    float v = y / (float)(clampedSize - 1);
                    Color baseColor = Color.Lerp(new Color(0.12f, 0.16f, 0.22f), new Color(0.32f, 0.28f, 0.18f), v);
                    pixels[y * clampedSize + x] = Color.Lerp(baseColor, new Color(u, 0.45f, 0.8f), 0.25f);
                }
            }

            FillRect(pixels, clampedSize, new Rect(0.08f, 0.18f, 0.28f, 0.34f), new Color(0.85f, 0.2f, 0.18f));
            FillRect(pixels, clampedSize, new Rect(0.42f, 0.14f, 0.22f, 0.42f), new Color(0.18f, 0.68f, 0.28f));
            FillRect(pixels, clampedSize, new Rect(0.68f, 0.32f, 0.22f, 0.28f), new Color(0.95f, 0.78f, 0.16f));
            FillRect(pixels, clampedSize, new Rect(0.24f, 0.66f, 0.5f, 0.16f), new Color(0.22f, 0.5f, 0.95f));

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static void FillRect(Color[] pixels, int textureSize, Rect normalizedRect, Color color)
        {
            int xMin = Mathf.Clamp(Mathf.RoundToInt(normalizedRect.xMin * textureSize), 0, textureSize - 1);
            int xMax = Mathf.Clamp(Mathf.RoundToInt(normalizedRect.xMax * textureSize), 0, textureSize - 1);
            int yMin = Mathf.Clamp(Mathf.RoundToInt(normalizedRect.yMin * textureSize), 0, textureSize - 1);
            int yMax = Mathf.Clamp(Mathf.RoundToInt(normalizedRect.yMax * textureSize), 0, textureSize - 1);

            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    pixels[y * textureSize + x] = color;
                }
            }
        }
    }

    public class SegmentationProvider : MonoBehaviour
    {
        [SerializeField] private TextAsset manualSegmentationJson;
        [SerializeField] private string streamingAssetsJsonPath = "Proj3/segmentation.json";

        private readonly List<InteractionEdgeData> lastInteractions = new List<InteractionEdgeData>();
        public IReadOnlyList<InteractionEdgeData> LastInteractions => lastInteractions;

        public List<SegmentedItemData> Segment(Texture2D image)
        {
            List<SegmentedItemData> manualItems = TryLoadManualSegmentation();
            if (manualItems.Count > 0)
            {
                return manualItems;
            }

            lastInteractions.Clear();
            return CreateMockItems(image);
        }

        private List<SegmentedItemData> TryLoadManualSegmentation()
        {
            string json = manualSegmentationJson != null ? manualSegmentationJson.text : null;
            if (string.IsNullOrWhiteSpace(json))
            {
                string path = Path.Combine(Application.streamingAssetsPath, streamingAssetsJsonPath);
                if (File.Exists(path))
                {
                    json = File.ReadAllText(path);
                }
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<SegmentedItemData>();
            }

            SegmentationFile file = JsonUtility.FromJson<SegmentationFile>(json);
            List<SegmentedItemData> items = new List<SegmentedItemData>();
            if (file == null || file.items == null)
            {
                return items;
            }

            lastInteractions.Clear();
            if (file.interactions != null)
            {
                lastInteractions.AddRange(file.interactions);
            }

            for (int i = 0; i < file.items.Length; i++)
            {
                SegmentationFileItem source = file.items[i];
                Rect bounds = source.bounds != null
                    ? source.bounds.ToRect()
                    : new Rect(source.x, source.y, source.width, source.height);
                items.Add(new SegmentedItemData
                {
                    id = string.IsNullOrWhiteSpace(source.id) ? "item-" + i : source.id,
                    label = string.IsNullOrWhiteSpace(source.label) ? "Item " + (i + 1) : source.label,
                    normalizedBounds = ClampRect(bounds),
                    tint = Color.HSVToRGB((i * 0.17f) % 1f, 0.65f, 1f),
                    confidence = source.confidence,
                    cropPath = source.cropPath,
                    maskPath = source.maskPath,
                    relatedItemIds = source.relatedItemIds ?? Array.Empty<string>()
                });
            }

            return items;
        }

        private static List<SegmentedItemData> CreateMockItems(Texture2D image)
        {
            return new List<SegmentedItemData>
            {
                new SegmentedItemData
                {
                    id = "knife",
                    label = "Knife",
                    normalizedBounds = new Rect(0.24f, 0.66f, 0.5f, 0.16f),
                    tint = new Color(0.45f, 0.68f, 1f),
                    confidence = 1f,
                    relatedItemIds = new[] { "apple" }
                },
                new SegmentedItemData
                {
                    id = "apple",
                    label = "Apple",
                    normalizedBounds = new Rect(0.08f, 0.18f, 0.28f, 0.34f),
                    tint = new Color(1f, 0.35f, 0.32f),
                    confidence = 1f,
                    relatedItemIds = new[] { "knife", "bottle" }
                },
                new SegmentedItemData
                {
                    id = "bottle",
                    label = "Bottle",
                    normalizedBounds = new Rect(0.42f, 0.14f, 0.22f, 0.42f),
                    tint = new Color(0.35f, 0.95f, 0.45f),
                    confidence = 1f,
                    relatedItemIds = new[] { "cup", "apple" }
                },
                new SegmentedItemData
                {
                    id = "cup",
                    label = "Cup",
                    normalizedBounds = new Rect(0.68f, 0.32f, 0.22f, 0.28f),
                    tint = new Color(1f, 0.82f, 0.25f),
                    confidence = 1f,
                    relatedItemIds = new[] { "bottle" }
                }
            };
        }

        private static Rect ClampRect(Rect rect)
        {
            float x = Mathf.Clamp01(rect.x);
            float y = Mathf.Clamp01(rect.y);
            float width = Mathf.Clamp(rect.width, 0.02f, 1f - x);
            float height = Mathf.Clamp(rect.height, 0.02f, 1f - y);
            return new Rect(x, y, width, height);
        }
    }

    public class InteractionGraph : MonoBehaviour
    {
        private readonly Dictionary<string, HashSet<string>> relatedIds = new Dictionary<string, HashSet<string>>();
        private readonly Dictionary<string, string> labelsById = new Dictionary<string, string>();
        private readonly Dictionary<string, InteractionEdgeData> edgesByPair = new Dictionary<string, InteractionEdgeData>();

        public void Build(IReadOnlyList<SegmentedItemData> items, IReadOnlyList<InteractionEdgeData> interactions = null)
        {
            relatedIds.Clear();
            labelsById.Clear();
            edgesByPair.Clear();

            for (int i = 0; i < items.Count; i++)
            {
                SegmentedItemData item = items[i];
                relatedIds[item.id] = new HashSet<string>();
                labelsById[item.id] = item.label;
            }

            if (interactions != null)
            {
                for (int i = 0; i < interactions.Count; i++)
                {
                    AddInteraction(interactions[i]);
                }
            }

            for (int i = 0; i < items.Count; i++)
            {
                SegmentedItemData item = items[i];
                if (item.relatedItemIds != null)
                {
                    for (int j = 0; j < item.relatedItemIds.Length; j++)
                    {
                        AddRelation(item.id, item.relatedItemIds[j]);
                    }
                }
            }

            for (int a = 0; a < items.Count; a++)
            {
                for (int b = a + 1; b < items.Count; b++)
                {
                    if (LabelsSuggestInteraction(items[a].label, items[b].label))
                    {
                        AddRelation(items[a].id, items[b].id);
                    }
                }
            }
        }

        public bool CanInteract(string sourceId, string targetId)
        {
            if (string.IsNullOrEmpty(sourceId) || string.IsNullOrEmpty(targetId)) return false;
            return relatedIds.TryGetValue(sourceId, out HashSet<string> targets) && targets.Contains(targetId);
        }

        public bool TryGetInteraction(string sourceId, string targetId, out InteractionEdgeData edge)
        {
            edge = null;
            if (string.IsNullOrEmpty(sourceId) || string.IsNullOrEmpty(targetId)) return false;

            if (edgesByPair.TryGetValue(PairKey(sourceId, targetId), out edge))
            {
                return true;
            }
            return edgesByPair.TryGetValue(PairKey(targetId, sourceId), out edge);
        }

        private void AddInteraction(InteractionEdgeData edge)
        {
            if (edge == null || string.IsNullOrEmpty(edge.sourceId) || string.IsNullOrEmpty(edge.targetId) || edge.sourceId == edge.targetId)
            {
                return;
            }

            AddRelation(edge.sourceId, edge.targetId);
            edgesByPair[PairKey(edge.sourceId, edge.targetId)] = edge;
        }

        private void AddRelation(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || a == b) return;
            if (!relatedIds.ContainsKey(a)) relatedIds[a] = new HashSet<string>();
            if (!relatedIds.ContainsKey(b)) relatedIds[b] = new HashSet<string>();
            relatedIds[a].Add(b);
            relatedIds[b].Add(a);
        }

        private static string PairKey(string sourceId, string targetId)
        {
            return sourceId + "->" + targetId;
        }

        private static bool LabelsSuggestInteraction(string a, string b)
        {
            string pair = (a + " " + b).ToLowerInvariant();
            return pair.Contains("knife") && (pair.Contains("apple") || pair.Contains("fruit") || pair.Contains("bread"))
                || pair.Contains("cup") && (pair.Contains("bottle") || pair.Contains("water") || pair.Contains("drink"))
                || pair.Contains("spoon") && (pair.Contains("bowl") || pair.Contains("cup"))
                || pair.Contains("pen") && (pair.Contains("paper") || pair.Contains("notebook"));
        }
    }

    public class ProxySpriteSpawner : MonoBehaviour
    {
        [SerializeField] private float worldHeight = 0.11f;
        [SerializeField] private Material spriteMaterial;

        public List<Proj3ProxyItem> Spawn(Texture2D source, IReadOnlyList<SegmentedItemData> items, Proj3PrototypeController controller)
        {
            List<Proj3ProxyItem> spawned = new List<Proj3ProxyItem>();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            for (int i = 0; i < items.Count; i++)
            {
                SegmentedItemData item = items[i];
                Texture2D thumbnail = TryLoadCropTexture(item.cropPath) ?? CreateThumbnail(source, item.normalizedBounds, item.tint);
                Sprite sprite = Sprite.Create(thumbnail, new Rect(0, 0, thumbnail.width, thumbnail.height), new Vector2(0.5f, 0.5f), thumbnail.height / worldHeight);
                sprite.name = item.label + " Sprite";

                GameObject proxyObject = new GameObject("Proxy_" + item.label);
                proxyObject.transform.SetParent(transform, false);

                SpriteRenderer renderer = proxyObject.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sharedMaterial = spriteMaterial;
                renderer.color = Color.white;

                BoxCollider collider = proxyObject.AddComponent<BoxCollider>();
                collider.size = new Vector3(sprite.bounds.size.x, sprite.bounds.size.y, 0.02f);

                Proj3ProxyItem proxy = proxyObject.AddComponent<Proj3ProxyItem>();
                proxy.Initialize(item, renderer, controller);
                AddLabel(proxyObject.transform, item.label, sprite.bounds.size.y);
                spawned.Add(proxy);
            }

            return spawned;
        }

        private static Texture2D TryLoadCropTexture(string cropPath)
        {
            if (string.IsNullOrWhiteSpace(cropPath)) return null;

            string path = Path.IsPathRooted(cropPath)
                ? cropPath
                : Path.Combine(Application.streamingAssetsPath, cropPath);
            if (!File.Exists(path)) return null;

            byte[] bytes = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes)) return null;

            texture.name = "Proj3 Item Crop";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }

        private static void AddLabel(Transform parent, string label, float spriteHeight)
        {
            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = new Vector3(0f, -spriteHeight * 0.72f, 0f);

            TextMesh text = labelObject.AddComponent<TextMesh>();
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.0025f;
            text.color = Color.white;
        }

        private static Texture2D CreateThumbnail(Texture2D source, Rect normalizedBounds, Color tint)
        {
            Rect rect = normalizedBounds;
            int x = Mathf.Clamp(Mathf.RoundToInt(rect.x * source.width), 0, source.width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(rect.y * source.height), 0, source.height - 1);
            int width = Mathf.Clamp(Mathf.RoundToInt(rect.width * source.width), 8, source.width - x);
            int height = Mathf.Clamp(Mathf.RoundToInt(rect.height * source.height), 8, source.height - y);

            Color[] crop = source.GetPixels(x, y, width, height);
            Texture2D thumbnail = new Texture2D(width, height, TextureFormat.RGBA32, false);
            thumbnail.name = "Proj3 Item Thumbnail";

            int border = Mathf.Max(2, Mathf.RoundToInt(Mathf.Min(width, height) * 0.04f));
            for (int py = 0; py < height; py++)
            {
                for (int px = 0; px < width; px++)
                {
                    bool isBorder = px < border || py < border || px >= width - border || py >= height - border;
                    int index = py * width + px;
                    if (isBorder)
                    {
                        crop[index] = Color.Lerp(crop[index], tint, 0.8f);
                    }
                }
            }

            thumbnail.SetPixels(crop);
            thumbnail.Apply();
            thumbnail.wrapMode = TextureWrapMode.Clamp;
            thumbnail.filterMode = FilterMode.Bilinear;
            return thumbnail;
        }
    }

    public class Proj3ProxyItem : MonoBehaviour
    {
        private const float SelectedScale = 1.22f;
        private const float RelatedScale = 1.12f;

        private SpriteRenderer spriteRenderer;
        private Proj3PrototypeController controller;
        private Vector3 targetPosition;
        private Quaternion targetRotation = Quaternion.identity;
        private float targetScale = 1f;
        private Color targetColor = Color.white;

        public SegmentedItemData Data { get; private set; }

        public void Initialize(SegmentedItemData data, SpriteRenderer renderer, Proj3PrototypeController owner)
        {
            Data = data;
            spriteRenderer = renderer;
            controller = owner;
            targetPosition = transform.position;
            targetColor = Color.white;
        }

        public void SetTargetPose(Vector3 position, Quaternion rotation)
        {
            targetPosition = position;
            targetRotation = rotation;
        }

        public void SetVisualState(bool selected, bool related)
        {
            targetScale = selected ? SelectedScale : related ? RelatedScale : 1f;
            if (selected)
            {
                targetColor = Color.Lerp(Color.white, Data.tint, 0.25f);
            }
            else if (related)
            {
                targetColor = Color.Lerp(Color.white, Data.tint, 0.55f);
            }
            else
            {
                targetColor = new Color(1f, 1f, 1f, 0.72f);
            }
        }

        private void Update()
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 12f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 12f);
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * targetScale, Time.deltaTime * 12f);
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.Lerp(spriteRenderer.color, targetColor, Time.deltaTime * 12f);
            }
        }

        private void OnMouseDown()
        {
            if (controller != null)
            {
                controller.SelectItem(this, true);
            }
        }
    }

    public class HandProxyLayout : MonoBehaviour
    {
        [SerializeField] private Transform handAnchor;
        [SerializeField] private Transform headAnchor;
        [SerializeField] private Vector3 localOffset = new Vector3(0.18f, 0.07f, 0.16f);
        [SerializeField] private float spacing = 0.13f;
        [SerializeField] private float relatedForwardOffset = 0.12f;
        [SerializeField] private float relatedSpacingScale = 0.72f;

        private readonly List<Proj3ProxyItem> items = new List<Proj3ProxyItem>();
        private InteractionGraph graph;
        private Proj3ProxyItem selectedItem;

        public void Configure(Transform preferredHandAnchor, Transform preferredHeadAnchor, InteractionGraph interactionGraph)
        {
            handAnchor = preferredHandAnchor;
            headAnchor = preferredHeadAnchor;
            graph = interactionGraph;
        }

        public void SetItems(IReadOnlyList<Proj3ProxyItem> newItems)
        {
            items.Clear();
            items.AddRange(newItems);
            selectedItem = items.Count > 0 ? items[0] : null;
        }

        public void SetSelected(Proj3ProxyItem item)
        {
            selectedItem = item;
        }

        private void LateUpdate()
        {
            if (items.Count == 0) return;

            Transform anchor = handAnchor != null ? handAnchor : transform;
            Transform head = headAnchor != null ? headAnchor : Camera.main != null ? Camera.main.transform : null;

            Vector3 anchorPosition = anchor.position;
            Vector3 headForward = head != null ? head.forward : Vector3.forward;
            Vector3 headRight = head != null ? head.right : Vector3.right;
            Vector3 headUp = head != null ? head.up : Vector3.up;
            Vector3 basePosition = anchorPosition + headRight * localOffset.x + headUp * localOffset.y + headForward * localOffset.z;
            Quaternion facingRotation = head != null
                ? Quaternion.LookRotation((basePosition - head.position).normalized, Vector3.up)
                : Quaternion.identity;

            int count = items.Count;
            for (int i = 0; i < count; i++)
            {
                Proj3ProxyItem item = items[i];
                bool selected = item == selectedItem;
                bool related = selectedItem != null && item != selectedItem && graph != null && graph.CanInteract(selectedItem.Data.id, item.Data.id);

                float centeredIndex = i - (count - 1) * 0.5f;
                float appliedSpacing = related ? spacing * relatedSpacingScale : spacing;
                Vector3 target = basePosition + headRight * centeredIndex * appliedSpacing;
                if (selected)
                {
                    target += headUp * 0.03f;
                }
                else if (related)
                {
                    target += headForward * relatedForwardOffset + headUp * 0.015f;
                }

                item.SetTargetPose(target, facingRotation);
                item.SetVisualState(selected, related);
            }
        }
    }

    public class Proj3PrototypeController : MonoBehaviour
    {
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private KeyCode rebuildKey = KeyCode.B;
        [SerializeField] private KeyCode nextSelectionKey = KeyCode.Tab;
        [SerializeField] private StaticImageSource imageSource;
        [SerializeField] private SegmentationProvider segmentationProvider;
        [SerializeField] private ProxySpriteSpawner spriteSpawner;
        [SerializeField] private HandProxyLayout handLayout;
        [SerializeField] private InteractionGraph interactionGraph;
        [SerializeField] private Transform handAnchor;
        [SerializeField] private Transform headAnchor;

        private readonly List<Proj3ProxyItem> spawnedItems = new List<Proj3ProxyItem>();
        private int selectedIndex;

        public void Configure(Transform preferredHandAnchor, Transform preferredHeadAnchor)
        {
            handAnchor = preferredHandAnchor;
            headAnchor = preferredHeadAnchor;
        }

        private void Awake()
        {
            EnsureComponents();
        }

        private void Start()
        {
            if (runOnStart)
            {
                Rebuild();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(rebuildKey))
            {
                Rebuild();
            }

            if (Input.GetKeyDown(nextSelectionKey) && spawnedItems.Count > 0)
            {
                selectedIndex = (selectedIndex + 1) % spawnedItems.Count;
                SelectItem(spawnedItems[selectedIndex]);
            }
        }

        public void Rebuild()
        {
            EnsureComponents();

            Texture2D image = imageSource.GetImage();
            List<SegmentedItemData> items = segmentationProvider.Segment(image);
            interactionGraph.Build(items, segmentationProvider.LastInteractions);

            spawnedItems.Clear();
            spawnedItems.AddRange(spriteSpawner.Spawn(image, items, this));
            handLayout.Configure(handAnchor, headAnchor, interactionGraph);
            handLayout.SetItems(spawnedItems);

            selectedIndex = 0;
            if (spawnedItems.Count > 0)
            {
                SelectItem(spawnedItems[0]);
            }
        }

        public void SelectItem(Proj3ProxyItem item, bool commitIfRelated = false)
        {
            if (item == null) return;
            Proj3ProxyItem previousItem = selectedIndex >= 0 && selectedIndex < spawnedItems.Count ? spawnedItems[selectedIndex] : null;
            if (commitIfRelated && previousItem != null && previousItem != item)
            {
                TryCommitInteraction(previousItem, item);
            }

            int index = spawnedItems.IndexOf(item);
            if (index >= 0)
            {
                selectedIndex = index;
            }

            handLayout.SetSelected(item);
        }

        private void TryCommitInteraction(Proj3ProxyItem source, Proj3ProxyItem target)
        {
            if (interactionGraph == null || source == null || target == null) return;
            if (interactionGraph.TryGetInteraction(source.Data.id, target.Data.id, out InteractionEdgeData edge))
            {
                Debug.LogFormat(
                    "Proj3 command: {0} -> {1}, action={2}, instruction={3}",
                    edge.sourceId,
                    edge.targetId,
                    edge.action,
                    edge.instruction);
            }
        }

        private void EnsureComponents()
        {
            if (imageSource == null) imageSource = GetComponent<StaticImageSource>() ?? gameObject.AddComponent<StaticImageSource>();
            if (segmentationProvider == null) segmentationProvider = GetComponent<SegmentationProvider>() ?? gameObject.AddComponent<SegmentationProvider>();
            if (interactionGraph == null) interactionGraph = GetComponent<InteractionGraph>() ?? gameObject.AddComponent<InteractionGraph>();

            if (spriteSpawner == null)
            {
                GameObject spawnerObject = new GameObject("Proxy Sprite Spawner");
                spawnerObject.transform.SetParent(transform, false);
                spriteSpawner = spawnerObject.AddComponent<ProxySpriteSpawner>();
            }

            if (handLayout == null)
            {
                handLayout = GetComponent<HandProxyLayout>() ?? gameObject.AddComponent<HandProxyLayout>();
            }

            if (headAnchor == null && Camera.main != null)
            {
                headAnchor = Camera.main.transform;
            }
        }
    }

    public static class Proj3SceneBootstrap
    {
        private const string SceneName = "Proj3Scene";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (SceneManager.GetActiveScene().name != SceneName) return;
            if (UnityEngine.Object.FindObjectOfType<Proj3PrototypeController>() != null) return;

            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.position = new Vector3(0f, 1.55f, -0.75f);
                cameraObject.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
                camera = cameraObject.AddComponent<Camera>();
            }

            Transform leftHand = FindOrCreateAnchor("LeftHandAnchor", camera.transform, new Vector3(-0.22f, -0.22f, 0.48f));
            Transform rightHand = FindOrCreateAnchor("RightHandAnchor", camera.transform, new Vector3(0.22f, -0.22f, 0.48f));

            GameObject runtime = new GameObject("Proj3 Runtime");
            Proj3PrototypeController controller = runtime.AddComponent<Proj3PrototypeController>();
            runtime.AddComponent<Proj3ExternalAnalyzer>();
            controller.Configure(rightHand != null ? rightHand : leftHand, camera.transform);
        }

        private static Transform FindOrCreateAnchor(string anchorName, Transform parent, Vector3 localPosition)
        {
            GameObject existing = GameObject.Find(anchorName);
            if (existing != null) return existing.transform;

            GameObject anchor = new GameObject(anchorName);
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = localPosition;
            anchor.transform.localRotation = Quaternion.identity;
            return anchor.transform;
        }
    }
}
