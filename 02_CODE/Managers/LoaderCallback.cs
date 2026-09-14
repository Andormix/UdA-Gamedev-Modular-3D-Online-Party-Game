using UnityEngine;

public class LoaderCallback : MonoBehaviour
{
    private bool isFirsUpdate = true;

    private void Update()
    {
        if (!isFirsUpdate) return;
        isFirsUpdate = false;
        Loader.LoaderCallback();
    }

    
}
    
