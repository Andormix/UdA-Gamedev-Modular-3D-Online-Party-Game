using UnityEngine;
using TMPro;

public class TitleEffects : MonoBehaviour
{
    private TMP_Text _textMesh;
    [SerializeField] private float speed = 2.0f;
    [SerializeField] private float amount = 10.0f;

    void Awake() => _textMesh = GetComponent<TMP_Text>();

    void Update()
    {
        _textMesh.ForceMeshUpdate();
        var textInfo = _textMesh.textInfo;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            var charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible) continue;

            var verts = textInfo.meshInfo[charInfo.materialReferenceIndex].vertices;

            for (int j = 0; j < 4; j++)
            {
                var orig = verts[charInfo.vertexIndex + j];
                // Creamos un desfase basado en el tiempo y la posición de la letra
                verts[charInfo.vertexIndex + j] = orig + new Vector3(0, Mathf.Sin(Time.time * speed + orig.x * 0.01f) * amount, 0);
            }
        }

        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
            _textMesh.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
        }
    }
}