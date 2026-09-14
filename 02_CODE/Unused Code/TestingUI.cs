using UnityEngine;
using UnityEngine.SceneManagement; 
using UnityEngine.UI; 

public class MenuController : MonoBehaviour
{
    [Header("Configuraciones del Menú")]
    [SerializeField] private GameObject panelOpciones;
    [SerializeField] private GameObject panelPrincipal;


    public void IniciarJuego()
    {
        Debug.Log("Cargando el juego...");
    }

    public void AlternarPanelOpciones(bool estado)
    {
        if (panelOpciones != null)
        {
            panelOpciones.SetActive(estado);
            panelPrincipal.SetActive(!estado); 
            Debug.Log("Panel de opciones: " + (estado ? "Abierto" : "Cerrado"));
        }
    }

    public void AjustarVolumen(float valor)
    {
        Debug.Log("El volumen actual es: " + valor);
    }

    public void SalirDelJuego()
    {
        Debug.Log("Saliendo del juego...");
    }
}