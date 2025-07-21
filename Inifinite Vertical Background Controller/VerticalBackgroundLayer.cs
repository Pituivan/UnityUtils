using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;

namespace Pituivan.UnityUtils
{
    internal class VerticalBackgroundLayer : MonoBehaviour
    {
        // ----- Nested Types

        public readonly struct LayerContext
        {
            public readonly InfiniteVerticalBackgroundController ParentController;
            public readonly VerticalBackgroundLayer Previous;
            public readonly VerticalBackgroundLayer Next;

            public LayerContext(InfiniteVerticalBackgroundController parentController, VerticalBackgroundLayer previous, VerticalBackgroundLayer next)
            {
                ParentController = parentController;
                Previous = previous;
                Next = next;
            }
        }
        
        // ----- Private Fields

        private LayerContext context;
        
        private Sprite sectionSprite;
        private Transform sectionPrefab;
        private Vector3 extents;
        
        private bool isHead;

        private readonly LinkedList<Transform> sections = new();
        private readonly Stack<Transform> disabledSections = new();
        
        // ----- Properties

        private Transform LeftSection
        {
            get => sections.First.Value;
            set => sections.AddFirst(value);
        }

        private Transform RightSection
        {
            get => sections.Last.Value;
            set => sections.AddLast(value);
        }
        
        // ----- Unity Callbacks

        void Start()
        {
            context.ParentController.CameraAspectRadioChanged += OnCameraAspectRadioChanged;
            PopulateLayer();
        }
        
        void Update()
        {
            transform.localPosition += context.ParentController.Speed * Time.deltaTime * Vector3.down;
            
            HandleLayersLoop();
            HandleLayersHorizontalRearrangement();
        }

        void OnDrawGizmos()
        {
            return;
            
            Gizmos.color = Color.aliceBlue;
            Gizmos.DrawWireCube(LeftSection.position, sectionSprite.bounds.size);
            Gizmos.DrawSphere(LeftSection.position, 1f);
            Gizmos.DrawWireCube(RightSection.position, sectionSprite.bounds.size);
            Gizmos.DrawSphere(RightSection.position, 1f);
            
            Gizmos.color = Color.deepPink;
            float x = LeftSection.position.x - extents.x;
            Gizmos.DrawLine(new Vector3(x, -20) , new Vector3(x, 20));
            Gizmos.color = Color.red;
            x = RightSection.position.x + extents.x;
            Gizmos.DrawLine(new Vector3(x, -20) , new Vector3(x, 20));

            Gizmos.color = Color.mediumSpringGreen;
            x = context.ParentController.CameraBounds.min.x;
            Gizmos.DrawLine(new Vector3(x, -20) , new Vector3(x, 20));
            Gizmos.color = Color.green;
            x = context.ParentController.CameraBounds.max.x;
            Gizmos.DrawLine(new Vector3(x, -20) , new Vector3(x, 20));
        }

        // ----- Public Methods

        public void Init(Sprite sectionSprite, LayerContext context, bool startsAsHead)
        {
            this.sectionSprite = sectionSprite;
            this.context = context;
            isHead = startsAsHead;

            extents = sectionSprite.bounds.extents;
        }
        
        // ----- Private Methods

        private void OnCameraAspectRadioChanged()
        {
            PopulateLayer();

            return;
            
            while (LeftSectionOffCamera())
            {
                Transform leftSection = LeftSection;
                sections.RemoveFirst();

                leftSection.gameObject.SetActive(false);
                disabledSections.Push(leftSection);
            }

            while (RightSectionOffCamera())
            {
                Transform rightSection = RightSection;
                sections.RemoveLast();
                
                rightSection.gameObject.SetActive(false);
                disabledSections.Push(rightSection);
            }
            
            PopulateLayer();
        }
        
        private bool LeftSectionOffCamera()
            => LeftSection.position.x + extents.x < context.ParentController.CameraBounds.min.x;
        
        private bool RightSectionOffCamera()
            => RightSection.position.x - extents.x > context.ParentController.CameraBounds.max.x;
        
        private void PopulateLayer()
        {
            if (transform.childCount == 0)
            {
                sections.AddFirst(CreateSection());
                CreateNewEdgeSections();
            }
            
            float camWidth = context.ParentController.CameraBounds.size.x;
            float sectionWidth = sectionSprite.bounds.size.x;
            
            int enabledSectionCount = transform.Cast<Transform>()
                                               .Count(s => s.gameObject.activeInHierarchy);
            float coveredArea = enabledSectionCount * sectionWidth;
            while (coveredArea < camWidth)
            {
                CreateNewEdgeSections();
                coveredArea += sectionWidth * 2f;
            }
        }

        private void CreateNewEdgeSections()
        {
            float sectionWidth = sectionSprite.bounds.size.x;
            int i = Mathf.CeilToInt(transform.childCount / 2f);

            LeftSection = GetAvailableSection();
            LeftSection.localPosition += sectionWidth * i * Vector3.left;

            RightSection = GetAvailableSection();
            RightSection.localPosition += sectionWidth * i * Vector3.right;
        }

        private Transform GetAvailableSection()
        {
            if (disabledSections.Count == 0) return CreateSection();

            Transform section = disabledSections.Pop();
            section.gameObject.SetActive(true);
            return section;
        }
        
        private Transform CreateSection()
        {
            if (sectionPrefab)
            {
                Transform section = Instantiate(sectionPrefab, transform);
                section.gameObject.name = "Horizontal Section";
                section.localPosition = Vector3.zero;
                return section;
            }
            
            var sectionObj = new GameObject("Horizontal Section");
            var sr = sectionObj.AddComponent<SpriteRenderer>();
            
            sr.sprite = sectionSprite;
            sr.sortingOrder = context.ParentController.OrderInLayer;
            sectionObj.transform.SetParent(transform);
            sectionObj.transform.localPosition = Vector3.zero;
            
            return sectionPrefab = sectionObj.transform;
        }
        
        private void HandleLayersLoop()
        {
            var controller = context.ParentController;
            if (!controller.CheckForCameraVerticalMovement && !isHead) return;
            
            Bounds camBounds = controller.CameraBounds;

            bool layerUnderCamera = (controller.Speed > 0 || controller.CheckForCameraVerticalMovement)
                && transform.position.y + extents.y < camBounds.min.y;
            
            bool cameraUnderLayer = (controller.Speed < 0 || controller.CheckForCameraVerticalMovement)
                && camBounds.min.y < transform.position.y - extents.y;

            if (layerUnderCamera) ToTail();
            else if (cameraUnderLayer) context.Next.BackToHead();
        }

        private void ToTail()
        {
            VerticalBackgroundLayer tail = context.Next;
            transform.position = tail.transform.position + (tail.extents.y + extents.y) * Vector3.up;
            transform.position += extents.y / 100f * Vector3.down;

            isHead = false;
            context.Previous.isHead = true;
        }

        private void BackToHead()
        {
            VerticalBackgroundLayer head = context.Previous;
            transform.position = head.transform.position + (head.extents.y + extents.y) * Vector3.down;
            transform.position += extents.y / 100f * Vector3.up;
            
            head.isHead = false;
            isHead = true;
        }

        private void HandleLayersHorizontalRearrangement()
        {
            if (!context.ParentController.CheckForCameraHorizontalMovement) return;
            
            while (LeftSectionOffCamera()) LeftSectionToTheRight();
            while (RightSectionOffCamera()) RightSectionToLeft();
        }
        
        private void LeftSectionToTheRight()
        {
            LeftSection.position = RightSection.position + extents.x * 2f * Vector3.right;

            Transform leftSection = LeftSection;
            sections.RemoveFirst();
            RightSection = leftSection;
        }

        private void RightSectionToLeft()
        {
            RightSection.position = LeftSection.position + extents.x * 2f * Vector3.left;
            
            Transform rightSection = RightSection;
            sections.RemoveLast();
            LeftSection = rightSection;
        }
    }
}