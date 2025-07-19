using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pituivan.UnityUtils
{
    [CreateAssetMenu(menuName = "Pituivan/Unity Utils/Level Loader")]
    public class LevelLoader : ScriptableObject
    {
        // ----- Serialized Fields

        [SerializeField]
        private string[] defaultLevelNames;
        
        // ----- Public Methods

        public void LoadDefaultLevel(int index)
        {
            if (index < 0 || index >= defaultLevelNames.Length)
                throw new ArgumentOutOfRangeException(nameof(index), $"There's no level {index + 1} in default level set!");
            
            SceneManager.LoadScene(defaultLevelNames[index]);
        }
    }
}