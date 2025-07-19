# Features
## Level Loader
Did you ever feel like using Unity's `SceneManager` and loading levels by their scene names is dirty? I mean, that definitely creates a tight coupling between your code and your scene names. Well, I did, so I made this little system that allows you to create `LevelLoader` ScriptableObjects in which to define level sets. Then, you can make the `LevelLoader` load a specific level by specifying the name of the level set it belongs to and its index, or load levels from the default level set.

> Note that this feature is still in development. For now, you can only define one level set — the default levels.