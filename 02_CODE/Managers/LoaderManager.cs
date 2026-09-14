using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Loader
{
    private static Scene targetScene;

    public enum Scene
    {
        MenuScene,
        LoadingScene,
        Map_01,
        LobbyScene,
        CharacterSelectScene,
        CampaignSelectScene,
        Map_02,
        StartScene,
        Map_03_M,
    }

    public static void Load(Scene sceneName)
    {
        Loader.targetScene = sceneName;
        SceneManager.LoadScene(Scene.LoadingScene.ToString());
    }

    public static void LoadNetwork(Scene targetScene)
    {
        NetworkManager.Singleton.SceneManager.LoadScene(targetScene.ToString(), LoadSceneMode.Single);
    }

    // We ensure Loading will charge
    public static void LoaderCallback()
    {
        SceneManager.LoadScene(targetScene.ToString());
    }

    public static AsyncOperation GetLoadingAsyncOperation()
    {
        return SceneManager.LoadSceneAsync(targetScene.ToString());
    }
}
