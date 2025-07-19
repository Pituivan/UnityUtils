using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Pituivan.UnityUtils
{
    /// <summary>
    /// In order for this controller to function properly, make sure of the following: <br/>
    /// - <see cref="backgroundProgression"/> has enough sprites to vertically cover the entire camera. <br/>
    /// - The main camera won't be changed or replaced after Awake. <br/>
    /// - The main camera won't rotate.
    /// </summary>
    public class InfiniteVerticalBackgroundController : MonoBehaviour
    {
        // ----- Nested Members

        private class LayerData
        {
            public Transform SectionPrefab { get; set; }
            public Sprite SectionSprite { get; set; }
            
            public Transform SectionToTheLeft { get; set; }
            public Transform SectionToTheRight { get; set; }
        }
        
        // ----- Serialized Fields & Properties
        
        [Tooltip("Drag here all the vertical layers of the background in order.")]
        [SerializeField] private Sprite[] backgroundProgression;
        
        [SerializeField] private int orderInLayer;
        
        // Rotation isn't handled
        [Tooltip("Leave this off only if you're sure your camera won't move in the x-axis.")]
        [SerializeField] private bool checkForCameraHorizontalMovement = true;
        [Tooltip("Leave this off only if you're sure your camera won't move in the y-axis.")]
        [SerializeField] private bool checkForCameraVerticalMovement = true;
        
        [field: Space]
        [field: SerializeField] 
        public float Speed { get; set; } = 0.25f;
        
        private float CameraWidth => camera.orthographicSize * 2f * camera.aspect;
        
        // ----- Private Fields

        // Note that this controller assumes main camera won't change
        private new Camera camera;
        private float lastCameraAspect;
        
        private Transform[] backgroundLayers;
        private int bottomLayerIndex, topLayerIndex;
        private bool repositioningLayer;

        private readonly Dictionary<Transform, LayerData> layerDataMap = new();
        
        // ----- Unity Callbacks

        void Start()
        {
            camera = Camera.main;
            lastCameraAspect = camera.aspect;
            bottomLayerIndex = 0;
            topLayerIndex = backgroundProgression.Length - 1;
            
            CreateAndArrangeBackgroundLayers();
        }

        void Update()
        {
            bool aspectRatioChanged = lastCameraAspect != camera.aspect;
            
            if (aspectRatioChanged)
                lastCameraAspect = camera.aspect;
            
            LoopBackground(aspectRatioChanged);
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            
            Gizmos.color = Color.aquamarine;
            Gizmos.DrawCube(
                center: backgroundLayers[topLayerIndex].position,
                size: new Vector3(backgroundProgression[topLayerIndex].bounds.size.x * backgroundLayers[topLayerIndex].childCount, backgroundProgression[topLayerIndex].bounds.size.y)
                );

            Gizmos.color = Color.darkRed;
            Gizmos.DrawWireCube(
                center: backgroundLayers[topLayerIndex].position,
                size: new Vector3(backgroundProgression[bottomLayerIndex].bounds.size.x * backgroundLayers[bottomLayerIndex].childCount, backgroundProgression[bottomLayerIndex].bounds.size.y)
                );

            Debug.Log((bottomLayerIndex, topLayerIndex));
        }

        // ----- Public Methods

        /// <summary>
        /// Always call from Awake function
        /// </summary>
        public void Init(Sprite[] backgroundProgression)
        {
            this.backgroundProgression = backgroundProgression;
        }
        
        // ----- Private Methods
        
        private void CreateAndArrangeBackgroundLayers()
        {
            backgroundLayers = new Transform[backgroundProgression.Length];

            float coveredHeight = 0f;
            for (int i = 0; i < backgroundLayers.Length; i++)
            {
                float layerHeight = backgroundProgression[i].bounds.size.y;
                
                Transform layer = backgroundLayers[i] = CreateBackgroundLayer(i);
                PlaceLayerAtTheBottom(layer, layerHeight);
                layer.position += Vector3.up * coveredHeight;
                
                coveredHeight += layerHeight;
            }
        }
        
        private Transform CreateBackgroundLayer(int index)
        {
            Sprite layerHorizontalSectionSprite = backgroundProgression[index];
            
            Transform layer = new GameObject($"Horizontal Layer {index}").transform;
            layerDataMap[layer] = new LayerData { SectionSprite = layerHorizontalSectionSprite };
            
            layer.SetParent(transform);
            PopulateLayer(layer);
            
            return layer;
        }

        private void PopulateLayer(Transform layer)
        {
            CreateHorizontalSection(layer);
            CoverLayerWithSections(layer);
        }

        private void CoverLayerWithSections(Transform layer)
        {
            float cameraWidth = CameraWidth;
            float sectionWidth = layerDataMap[layer].SectionSprite.bounds.size.x;
            
            float coveredArea = layer.childCount * sectionWidth;

            int i = layer.childCount / 2 + 1;
            do
            {
                PlaceTwoSections(layer, i);

                coveredArea += sectionWidth * 2f;
                i++;
            } while (coveredArea < cameraWidth);
        }

        private void PlaceTwoSections(Transform layer, int sectionPairIndex)
        {
            LayerData layerData = layerDataMap[layer];
            float sectionWidth = layerData.SectionSprite.bounds.size.x;
            
            Transform sectionToTheLeft = layerData.SectionToTheLeft = CreateHorizontalSection(layer);
            sectionToTheLeft.localPosition = sectionWidth * sectionPairIndex * Vector3.left;
            sectionToTheLeft.position += Vector3.right * 0.001f;
                    
            Transform sectionToTheRight = layerData.SectionToTheRight = CreateHorizontalSection(layer);
            sectionToTheRight.localPosition = sectionWidth * sectionPairIndex * Vector3.right;
            sectionToTheRight.position += Vector3.left * 0.001f;
        }

        private Transform CreateHorizontalSection(Transform layer)
        {
            Transform section;
            if (layerDataMap[layer].SectionPrefab)
            {
                section = Instantiate(layerDataMap[layer].SectionPrefab);
            }
            else
            {
                var sectionObj = new GameObject("Horizontal Section");
                var sr = sectionObj.AddComponent<SpriteRenderer>();
            
                sr.sprite = layerDataMap[layer].SectionSprite;
                sr.sortingOrder = orderInLayer;

                layerDataMap[layer].SectionPrefab = section = sectionObj.transform;
            }
            
            section.SetParent(layer);
            return section;
        }

        private void PlaceLayerAtTheBottom(Transform layer, float layerHeight)
        {
            float cameraBottom = camera.transform.position.y - camera.orthographicSize;
            layer.position = Vector3.up * (cameraBottom + layerHeight / 2f);
        }
        
        private void LoopBackground(bool aspectRatioChanged)
        {
            foreach (Transform layer in backgroundLayers)
                layer.localPosition += Vector3.down * (Speed * Time.deltaTime);

            if (BottomLayerUnderCamera())
            {
                BottomLayerToTop();
                
                topLayerIndex = bottomLayerIndex;
                bottomLayerIndex = (backgroundLayers.Length + bottomLayerIndex - 1) % backgroundLayers.Length;
            } 
            else if (CameraUnderBottomLayer())
            {
                TopLayerToBottom();
                
                bottomLayerIndex = topLayerIndex;
                topLayerIndex = (bottomLayerIndex + 1) % backgroundLayers.Length;
            }
            
            if (aspectRatioChanged)
            {
                RepositionLayerSections();
                
                foreach (Transform layer in backgroundLayers)
                    CoverLayerWithSections(layer);
            }

            if (checkForCameraHorizontalMovement)
                RepositionLayerSections();
        }
        
        private bool BottomLayerUnderCamera()
        {
            Transform bottomLayer = backgroundLayers[bottomLayerIndex];
            Sprite bottomLayerSprite = backgroundProgression[bottomLayerIndex];
            float bottomLayerHeight = bottomLayerSprite.bounds.size.y;

            float bottomLayerTop = bottomLayer.position.y + bottomLayerHeight / 2f;
            float cameraBottom = camera.transform.position.y - camera.orthographicSize;

            return bottomLayerTop < cameraBottom;
        }

        private void BottomLayerToTop()
        {
            Transform bottomLayer = backgroundLayers[bottomLayerIndex];
            Transform topLayer = backgroundLayers[topLayerIndex];
            
            Sprite topLayerSprite = backgroundProgression[bottomLayerIndex];
            Sprite bottomLayerSprite = backgroundProgression[bottomLayerIndex];
            float topLayerHeight = topLayerSprite.bounds.size.y;
            float bottomLayerHeight = bottomLayerSprite.bounds.size.y;

            float y = topLayer.transform.position.y + topLayerHeight / 2f + bottomLayerHeight / 2f;
            bottomLayer.transform.position = new Vector3(bottomLayer.transform.position.x, y);
        }

        private bool CameraUnderBottomLayer()
        {
            if (!checkForCameraVerticalMovement && Speed >= 0f) return false;
            
            Transform bottomLayer = backgroundLayers[bottomLayerIndex];
            Sprite bottomLayerSprite = backgroundProgression[bottomLayerIndex];
            float bottomLayerHeight = bottomLayerSprite.bounds.size.y;

            float bottomLayerBottom = bottomLayer.position.y - bottomLayerHeight / 2f;
            float cameraBottom = camera.transform.position.y - camera.orthographicSize;

            return cameraBottom < bottomLayerBottom;
        }

        private void TopLayerToBottom()
        {
            Transform topLayer = backgroundLayers[topLayerIndex];
            Transform bottomLayer = backgroundLayers[bottomLayerIndex];
            
            Sprite bottomLayerSprite = backgroundProgression[bottomLayerIndex];
            Sprite topLayerSprite = backgroundProgression[topLayerIndex];
            float bottomLayerHeight = bottomLayerSprite.bounds.size.y;
            float topLayerHeight = topLayerSprite.bounds.size.y;

            float y = bottomLayer.transform.position.y - bottomLayerHeight / 2f - topLayerHeight / 2f;
            topLayer.transform.position = new Vector3(bottomLayer.transform.position.x, y);
        }

        private void RepositionLayerSections()
        {
            float cameraWidth = CameraWidth;
            foreach (Transform layer in backgroundLayers)
            {
                while (LeftSectionOffCamera(layer, cameraWidth))
                    LeftSectionToTheRight(layer);
                
                while (RightSectionOffCamera(layer, cameraWidth))
                    RightSectionToTheLeft(layer);
            }
        }
        
        private bool RightSectionOffCamera(Transform layer, float cameraWidth)
        {
            Transform sectionToTheLeft = layerDataMap[layer].SectionToTheLeft;
            float sectionSpriteWidth = layerDataMap[layer].SectionSprite.bounds.size.x;

            float sectionToTheLeftLeftEdge = sectionToTheLeft.position.x - sectionSpriteWidth / 2f;
            float cameraLeftEdge = camera.transform.position.x - cameraWidth / 2f;

            return cameraLeftEdge < sectionToTheLeftLeftEdge;
        }

        private void LeftSectionToTheRight(Transform layer)
        {
            LayerData layerData = layerDataMap[layer];
            Transform sectionToTheLeft = layerData.SectionToTheLeft;
            Transform sectionToTheRight = layerData.SectionToTheRight;
            float sectionSpriteWidth = layerData.SectionSprite.bounds.size.x;

            float x = sectionToTheRight.transform.position.x + sectionSpriteWidth;
            sectionToTheLeft.transform.position = new Vector3(x, sectionToTheLeft.transform.position.y);
            
            layerData.SectionToTheRight = sectionToTheLeft;
            layerData.SectionToTheLeft = layer.Cast<Transform>()
                                              .OrderBy(s => s.position.x)
                                              .First();
        }
        
        private bool LeftSectionOffCamera(Transform layer, float cameraWidth)
        {
            Transform sectionToTheRight = layerDataMap[layer].SectionToTheRight;
            float sectionSpriteWidth = layerDataMap[layer].SectionSprite.bounds.size.x;
            
            float sectionToTheRightRightEdge = sectionToTheRight.position.x + sectionSpriteWidth / 2f;
            float cameraRightEdge = camera.transform.position.x + cameraWidth / 2f;
            
            return sectionToTheRightRightEdge < cameraRightEdge;
        }

        private void RightSectionToTheLeft(Transform layer)
        {
            LayerData layerData = layerDataMap[layer];
            Transform sectionToTheLeft = layerData.SectionToTheLeft;
            Transform sectionToTheRight = layerData.SectionToTheRight;
            float sectionSpriteWidth = layerData.SectionSprite.bounds.size.x;

            float x = sectionToTheLeft.transform.position.x - sectionSpriteWidth;
            sectionToTheRight.transform.position = new Vector3(x, sectionToTheLeft.transform.position.y);

            layerData.SectionToTheLeft = sectionToTheRight;
            layerData.SectionToTheRight = layer.Cast<Transform>()
                                              .OrderByDescending(s => s.position.x)
                                              .First();
        }
    }
}
