using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Pituivan.UnityUtils
{
    /// <summary>
    /// In order for this controller to function properly, make sure of the following: <br/>
    /// - The main camera won't be changed or replaced after Awake. <br/>
    /// - The main camera won't rotate. <br/>
    /// - The main camera is orthographic, of course.
    /// </summary>
    public class InfiniteVerticalBackgroundController : MonoBehaviour
    {
        // ----- Serialized Fields

        [Tooltip("Drag here all the vertical layers of the background in order.")]
        [SerializeField]
        private Sprite[] backgroundProgression;

        [SerializeField]
        private int orderInLayer;

        [Tooltip("Leave this off only if you're sure your camera won't move in the x-axis.")]
        [SerializeField]
        private bool checkForCameraHorizontalMovement = true;

        [Tooltip("Leave this off only if you're sure your camera won't move in the y-axis.")]
        [SerializeField]
        private bool checkForCameraVerticalMovement = true;

        [Space]
        [SerializeField]
        private float speed = 0.25f;

        // ----- Private Fields

        private new Camera camera;
        private Bounds cameraBounds;
        private float lastCameraAspect;
        
        // ----- Properties

        public float Speed
        {
            get => speed;
            set => speed = value;
        }

        public bool CheckForCameraVerticalMovement
        {
            get => checkForCameraVerticalMovement;
            set => checkForCameraVerticalMovement = value;
        }

        public bool CheckForCameraHorizontalMovement
        {
            get => checkForCameraHorizontalMovement;
            set => checkForCameraHorizontalMovement = value;
        }

        internal int OrderInLayer => orderInLayer;

        internal Bounds CameraBounds => cameraBounds;

        // ----- Events

        internal event Action CameraAspectRadioChanged;

        // ----- Unity Callbacks

        void Start()
        {
            camera = Camera.main;

            cameraBounds.center = transform.position;
            UpdateCameraBoundsSize();
            
            FillCameraWithBackgroundLayers();
        }

        void LateUpdate()
        {
            CheckForChangesInCamera();
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
        
        private void FillCameraWithBackgroundLayers()
        {
            var layers = GetComponentsInChildren<VerticalBackgroundLayer>().ToList();

            float coveredHeight = 0f;
            for (int i = 0; i < layers.Count; i++)
            {
                Sprite layerSprite = backgroundProgression[i % backgroundProgression.Length];
                coveredHeight = layerSprite.bounds.size.y;
            }
            
            while (coveredHeight < camera.orthographicSize * 2f)
            {
                foreach (Sprite sectionSprite in backgroundProgression)
                {
                    var layerObj = new GameObject("Background Layer");
                    var layer = layerObj.AddComponent<VerticalBackgroundLayer>();
                    layer.transform.SetParent(transform);

                    layer.transform.position += coveredHeight * Vector3.up;
                    coveredHeight += sectionSprite.bounds.size.y;
                    
                    layers.Add(layer);
                }
            }
            
            InitBackgroundLayers(layers);
        }

        private void InitBackgroundLayers(List<VerticalBackgroundLayer> layers)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                layers[i].Init(
                    sectionSprite: backgroundProgression[i % backgroundProgression.Length],
                    startsAsHead: i == 0,
                    context: new VerticalBackgroundLayer.LayerContext(
                        parentController: this,
                        previous: layers[(i + 1) % layers.Count],
                        next: layers[(layers.Count + i - 1) % layers.Count]
                        )
                );
            }
        }

        private void UpdateCameraBoundsSize()
        {
            float camHeight = camera.orthographicSize * 2f;
            float camWidth = camHeight * camera.aspect;

            cameraBounds.size = new Vector3(camWidth, camHeight);
        }

        private void CheckForChangesInCamera()
        {
            if (checkForCameraVerticalMovement || checkForCameraHorizontalMovement)
                cameraBounds.center = camera.transform.position;
            
            if (lastCameraAspect != camera.aspect)
            {
                float oldHeight = cameraBounds.size.y;
                UpdateCameraBoundsSize();
                
                if (cameraBounds.size.y > oldHeight)
                    FillCameraWithBackgroundLayers();
                
                CameraAspectRadioChanged?.Invoke();
                lastCameraAspect = camera.aspect;   
            }
        }
    }
}
