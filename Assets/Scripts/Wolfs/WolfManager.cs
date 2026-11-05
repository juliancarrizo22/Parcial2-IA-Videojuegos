using System.Collections.Generic;
using UnityEngine;

public class WolfManager : MonoBehaviour
{
    private static WolfManager instance;
    public static WolfManager Instance
    {
        get
        {
            if (instance == null)
            {
                // Se auto-crea un GameObject en la escena si olvidaste colocarlo manualmente
                GameObject managerObj = new GameObject("WolfManager_AutoCreated");
                instance = managerObj.AddComponent<WolfManager>();
            }
            return instance;
        }
    }

    private List<WolfAgent> wolves = new List<WolfAgent>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    public void RegisterWolf(WolfAgent wolf)
    {
        if (!wolves.Contains(wolf))
        {
            wolves.Add(wolf);
        }
    }

    public void UnregisterWolf(WolfAgent wolf)
    {
        if (wolves.Contains(wolf))
        {
            wolves.Remove(wolf);
        }
    }

    public void BroadcastHowlAlert(WolfAgent emitter, Vector3 alertPosition)
    {
        Debug.Log($"[AULLIDO] ¡El lobo {emitter.name} detectó al Cazador! Alerta enviada a {wolves.Count - 1} lobos de la jauría.");

        foreach (var wolf in wolves)
        {
            if (wolf == null || wolf == emitter) continue;

            // Cada lobo lejano recibe la alerta y reacciona
            wolf.ReceiveHowlAlert(alertPosition);
        }
    }
    private void OnApplicationQuit()
    {
        // Esto evita que el Singleton intente recrear objetos cuando la aplicación se está cerrando
        instance = null;
    }
}
