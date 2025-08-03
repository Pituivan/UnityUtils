using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pituivan.UnityUtils
{
    [CustomEditor(typeof(LevelLoader))]
    internal class LevelLoaderInspector : UnityEditor.Editor
    {
        // ----- Serialized Fields
        
        [SerializeField] private VisualTreeAsset ui;
        [SerializeField] private VisualTreeAsset noLevelsMsg;
        [SerializeField] private VisualTreeAsset repeatedNameErrorMsg;
        
        // ----- Private Fields

        private SerializedProperty defaultLevelNamesProp;
        private ObservableCollection<SceneAsset> defaultLevels;

        private VisualElement inspector;
        private VisualElement defaultLevelSetHeader;
        private ListView defaultLevelsListView;

        private bool isInRepeatedNameErrorState;
        
        // ----- Public Methods

        public override VisualElement CreateInspectorGUI()
        {
            inspector ??= new VisualElement();
            inspector.Clear();
            inspector.Add(ui.Instantiate());

            if (TryLoadLevels())
            {
                isInRepeatedNameErrorState = false;
                
                HandleLevelListChanges();
                InitVisualElements();
                ToggleAddFirstLevelMode(defaultLevels.Count == 0);
            }

            return inspector;
        }
        
        // ----- Private Methods

        private bool TryLoadLevels()
        {
            defaultLevelNamesProp = serializedObject.FindProperty("defaultLevelNames");
            var defaultLevels = new SceneAsset[defaultLevelNamesProp.arraySize];
            for (int i = 0; i < defaultLevelNamesProp.arraySize; i++)
            {
                SerializedProperty nameProp = defaultLevelNamesProp.GetArrayElementAtIndex(i);
                string name = nameProp.stringValue;
                if (name == null) continue;
                
                var paths = (from guid in AssetDatabase.FindAssets("t:SceneAsset " + name) 
                             let path = AssetDatabase.GUIDToAssetPath(guid)
                             let fileName = Path.GetFileNameWithoutExtension(path)
                             where fileName == name
                             select path).ToArray();

                if (paths.Length == 0)
                {
                    nameProp.stringValue = null;
                    continue;
                }
                
                if (paths.Length > 1)
                {
                    DisplayRepeatedLevelNameError(name);
                    return false;
                }
                
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(paths[0]);
                defaultLevels[i] = sceneAsset;
            }
            
            this.defaultLevels = new ObservableCollection<SceneAsset>(defaultLevels);
            return true;
        }

        private void DisplayRepeatedLevelNameError(string name)
        {
            VisualElement errorMsg = repeatedNameErrorMsg.Instantiate();
            
            HelpBox warning = errorMsg.Q<HelpBox>();
            warning.text = warning.text.Replace("[name]", name);
            LogErrorMsgOrPulseExisting(warning);
            
            Button solvedBtn = errorMsg.Q<Button>();
            solvedBtn.clicked += () => CreateInspectorGUI();
            
            inspector.Clear();
            inspector.Add(errorMsg);

            isInRepeatedNameErrorState = true;
        }

        private void LogErrorMsgOrPulseExisting(HelpBox errorMsg)
        {
            if (isInRepeatedNameErrorState)
            {
                errorMsg.transform.scale = Vector3.one * 1.025f;
                errorMsg.schedule.Execute(() => errorMsg.transform.scale = Vector3.one)
                    .StartingIn(10);
            }
            else
            {
                Debug.LogError(errorMsg.text);
            }
        }
        
        private void HandleLevelListChanges()
        {
            defaultLevels.CollectionChanged += (_, args) =>
            {
                switch (args.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        defaultLevelNamesProp.arraySize++;
                        ToggleAddFirstLevelMode(false);
                        break;
                    
                    case NotifyCollectionChangedAction.Remove:
                        defaultLevelNamesProp.arraySize--;
                        if (defaultLevels.Count == 0)
                            ToggleAddFirstLevelMode(true);
                        break;
                    
                    case NotifyCollectionChangedAction.Reset:
                        defaultLevelNamesProp.ClearArray();
                        ToggleAddFirstLevelMode(true);
                        break;
                }
                
                defaultLevelsListView.RefreshItems();
                serializedObject.ApplyModifiedProperties();
            };
        }

        private void ToggleAddFirstLevelMode(bool value)
        {
            defaultLevelSetHeader.style.display = value ? DisplayStyle.None : DisplayStyle.Flex;
            defaultLevelsListView.showBorder = !value;
            defaultLevelsListView.showAddRemoveFooter = !value;
        }
        
        private void InitVisualElements()
        {
            defaultLevelSetHeader = inspector.Q("default-levels-header");

            Button clearDefaultLevelsBtn = defaultLevelSetHeader.Q<Button>("clear-default-levels");
            clearDefaultLevelsBtn.clicked += () => defaultLevels.Clear();
            
            defaultLevelsListView = inspector.Q<ListView>("default-levels");
            defaultLevelsListView.makeNoneElement = NoLevelsMsgFactory;
            defaultLevelsListView.itemsSource = defaultLevels;
            defaultLevelsListView.bindItem = BindLevelReference;
            defaultLevelsListView.unbindItem = (ve, _) => UnbindLevelReference(ve);
        }

        private VisualElement NoLevelsMsgFactory()
        {
            VisualElement result = noLevelsMsg.Instantiate();
            
            Button addLvlBtn = result.Q<Button>("add-level");
            addLvlBtn.clicked += () => defaultLevels.Add(null);
            
            return result;
        }
        
        private void BindLevelReference(VisualElement levelReference, int levelIndex)
        {
            ObjectField levelField = levelReference.Q<ObjectField>();
            levelField.label = "Level " + (levelIndex + 1);
            levelField.value = defaultLevels[levelIndex];

            EventCallback<ChangeEvent<Object>> callback = evt =>
            {
                var newLevel = (SceneAsset)evt.newValue;
                defaultLevels[levelIndex] = newLevel;
                
                bool RepeatedName(SceneAsset lvl) => lvl != newLevel && lvl?.name == newLevel?.name;
                if (defaultLevels.Any(RepeatedName))
                {
                    DisplayRepeatedLevelNameError(newLevel.name);
                    return;
                }
                
                defaultLevelNamesProp.GetArrayElementAtIndex(levelIndex).stringValue = newLevel?.name;
                serializedObject.ApplyModifiedProperties();
            };
            
            levelField.RegisterCallback(callback);
            levelField.userData = callback;
        }
        
        private void UnbindLevelReference(VisualElement levelReference)
        {
            ObjectField levelField = levelReference.Q<ObjectField>();
            var callback = (EventCallback<ChangeEvent<Object>>)levelField.userData;
            levelField.UnregisterCallback(callback);
        }
    }
}