using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Pituivan.UnityUtils
{
    internal class BackgroundLayer : MonoBehaviour
    {
        // ----- Nested Types
        
        public readonly struct Context
        {
            public readonly InfiniteVerticalBackgroundController Controller;
            public readonly BackgroundLayer Previous;
            public readonly BackgroundLayer Next;

            public Context(InfiniteVerticalBackgroundController controller, BackgroundLayer previous, BackgroundLayer next)
            {
                Controller = controller;
                Previous = previous;
                Next = next;
            }
        }       
        
        // ----- Private Fields

        private Context context;
        
        private Sprite sectionSprite;
        private Transform sectionPrefab;
        private float verticalExtent;
        
        private bool isHead;

        private readonly LinkedList<Transform> sections = new();
        
        // ----- Unity Callbacks
        
        void Update()
        {
            transform.localPosition += context.Controller.Speed * Time.deltaTime * Vector3.down;

            HandleLayersLoop();
            HandleSectionsLoop(cameraBoundsChanged: false);
        }

        // ----- Public Methods

        public void Init(Sprite sectionSprite, Context context, bool startsAsHead)
        {
            this.sectionSprite = sectionSprite;
            this.context = context;
            isHead = startsAsHead;

            verticalExtent = sectionSprite.bounds.extents.y;
            for (int i = 0; i < 3; i++)
                sections.AddFirst(CreateSection());
            
            float spriteWidth = sectionSprite.bounds.size.x;
            sections.First.Value.position += spriteWidth * Vector3.left;
            sections.Last.Value.position += spriteWidth * Vector3.right;
            
            context.Controller.CameraBoundsChanged += () => HandleSectionsLoop(cameraBoundsChanged: true);
        }
        
        // ----- Private Methods
        
        private void HandleLayersLoop()
        {
            if (!context.Controller.CheckForCameraVerticalMovement && !isHead) return;
            
            var controller = context.Controller;
            Bounds camBounds = controller.CameraBounds;

            bool layerUnderCamera = (controller.Speed > 0 || controller.CheckForCameraVerticalMovement)
                                    && transform.position.y + verticalExtent < camBounds.min.y;
            
            bool cameraUnderLayer = (controller.Speed < 0 || controller.CheckForCameraVerticalMovement)
                                    && camBounds.min.y < transform.position.y - verticalExtent;

            if (layerUnderCamera) ToTail();
            else if (cameraUnderLayer) context.Next.BackToHead();
        }

        private void ToTail()
        {
            BackgroundLayer tail = context.Next;
            transform.position = tail.transform.position + (tail.verticalExtent + verticalExtent) * Vector3.up;
            transform.position += verticalExtent / 100f * Vector3.down;

            isHead = false;
            context.Previous.isHead = true;
        }

        private void BackToHead()
        {
            BackgroundLayer head = context.Previous;
            transform.position = head.transform.position + (head.verticalExtent + verticalExtent) * Vector3.down;
            transform.position += verticalExtent / 100f * Vector3.up;
            
            head.isHead = false;
            isHead = true;
        }

        private void HandleSectionsLoop(bool cameraBoundsChanged)
        {
            if (!cameraBoundsChanged && !context.Controller.CheckForCameraHorizontalMovement) return;

            float spriteWidth = sectionSprite.bounds.size.x;
            Bounds camBounds = context.Controller.CameraBounds;
            var sectionClosestToCenter = sections.Find(sections.Aggregate(ClosestToCenter));

            sectionClosestToCenter.Value.gameObject.SetActive(true);
            
            var section = sectionClosestToCenter;
            while (section.Previous != null)
            {
                section = section.Previous;
                
                section.Value.position = section.Next.Value.position + spriteWidth * Vector3.left;

                bool isOffCam = section.Value.position.x + spriteWidth / 2f < camBounds.min.x;
                section.Value.gameObject.SetActive(!isOffCam);
                
                if (!isOffCam && section.Previous == null) sections.AddFirst(CreateSection());
            }

            section = sectionClosestToCenter;
            while (section.Next != null)
            {
                section = section.Next;
                
                section.Value.position = section.Previous.Value.position + spriteWidth * Vector3.right;

                bool isOffCam = section.Value.position.x - spriteWidth / 2f > camBounds.max.x;
                section.Value.gameObject.SetActive(!isOffCam);

                if (!isOffCam && section.Next == null) sections.AddLast(CreateSection());
            }
        }

        private Transform ClosestToCenter(Transform a, Transform b)
        {
            float center = context.Controller.CameraBounds.center.x;
            return Mathf.Abs(a.position.x - center) < Mathf.Abs(b.position.x - center) ? a : b;
        }
        
        private Transform CreateSection()
        {
            if (sectionPrefab)
            {
                Transform clone = Instantiate(sectionPrefab, transform);
                clone.name = "Horizontal Section";
                return clone;
            }
            
            var sectionObj = new GameObject("Horizontal Section");
            var sr = sectionObj.AddComponent<SpriteRenderer>();
            
            sr.sprite = sectionSprite;
            sr.sortingOrder = context.Controller.OrderInLayer;
            sectionObj.transform.SetParent(transform);
            sectionObj.transform.localPosition = Vector3.zero;

            return sectionPrefab = sectionObj.transform;
        }
    }
}