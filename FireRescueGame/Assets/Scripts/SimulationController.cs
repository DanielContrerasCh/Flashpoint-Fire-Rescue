using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class SimulationController : MonoBehaviour
{
    private string simulationUrl = "http://localhost:5000/api/simulation";
    public float stepInterval = 1f; // Tiempo entre cada paso de la simulación
    public float moveSpeed = 5f;
    public GameObject smokePrefab;
    public GameObject firePrefab;
    
    private Dictionary<int, GameObject> agents = new Dictionary<int, GameObject>();
    private List<GameObject> activeFireObjects = new List<GameObject>();
    private List<GameObject> activeSmokeObjects = new List<GameObject>();
    private MapGenerator mapGenerator;
    private List<SimulationData> simulationSteps;
    private int currentStep = 0;
    private bool isSimulationRunning = false;

    void Start()
    {
        mapGenerator = GetComponent<MapGenerator>();
        LoadSimulation();
    }

    void LoadSimulation()
    {
        StartCoroutine(FetchSimulation());
    }

    IEnumerator FetchSimulation()
    {
        UnityWebRequest request = UnityWebRequest.Get(simulationUrl);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string json = request.downloadHandler.text;
            simulationSteps = JsonConvert.DeserializeObject<List<SimulationData>>(json);
            
            if (simulationSteps != null && simulationSteps.Count > 0)
            {
                Debug.Log($"Simulación cargada correctamente. Total de pasos: {simulationSteps.Count}");
                StartSimulation();
            }
            else
            {
                Debug.LogError("No se encontraron datos de simulación");
            }
        }
        else
        {
            Debug.LogError("Error fetching simulation: " + request.error);
        }
    }

    void StartSimulation()
    {
        currentStep = 0;
        isSimulationRunning = true;
        StartCoroutine(PlaySimulation());
    }

    IEnumerator PlaySimulation()
    {
        while (isSimulationRunning && currentStep < simulationSteps.Count)
        {
            UpdateGameState(simulationSteps[currentStep]);
            currentStep++;
            yield return new WaitForSeconds(stepInterval);
        }

        if (currentStep >= simulationSteps.Count)
        {
            Debug.Log("Simulación completada");
            isSimulationRunning = false;
        }
    }

    void UpdateGameState(SimulationData data)
    {
        // Actualizar agentes
        foreach (var agent in data.agents)
        {
            // Ajustar la posición del agente para alinearse con el grid
            Vector3 targetPosition = new Vector3(
                (agent.pos[1] - 1) * mapGenerator.cellSize + mapGenerator.cellSize +2,
                1f,
                -((agent.pos[0] - 1) * mapGenerator.cellSize + mapGenerator.cellSize +2)
            );

            if (!agents.ContainsKey(agent.id))
            {
                GameObject newAgent = GameObject.FindGameObjectWithTag("Player");
                if (newAgent == null)
                {
                    newAgent = Instantiate(mapGenerator.playerPrefab, targetPosition, 
                        mapGenerator.playerPrefab.transform.rotation);
                }
                agents[agent.id] = newAgent;
            }

            StartCoroutine(MoveAgent(agents[agent.id], targetPosition));
        }

        // Limpiar fuegos anteriores
        foreach (var fire in activeFireObjects)
        {
            Destroy(fire);
        }
        activeFireObjects.Clear();

        // Crear nuevos fuegos con posiciones corregidas
        foreach (var fire in data.fires)
        {
            Vector3 position = new Vector3(
                (fire.col - 1) * mapGenerator.cellSize + mapGenerator.cellSize +2,
                1f,
                -((fire.row - 1) * mapGenerator.cellSize + mapGenerator.cellSize +2)
            );
            GameObject fireObj = Instantiate(mapGenerator.firePrefab, position, 
                mapGenerator.firePrefab.transform.rotation);
            activeFireObjects.Add(fireObj);
        }

        // Limpiar humo anterior
        foreach (var smoke in activeSmokeObjects)
        {
            Destroy(smoke);
        }
        activeSmokeObjects.Clear();

        // Crear nuevo humo con posiciones corregidas
        if (data.smokes != null)
        {
            foreach (var smoke in data.smokes)
            {
                Vector3 position = new Vector3(
                    (smoke.col - 1) * mapGenerator.cellSize + mapGenerator.cellSize +2,
                    1f,
                    -((smoke.row - 1) * mapGenerator.cellSize + mapGenerator.cellSize +2)
                );
                GameObject smokeObj = Instantiate(smokePrefab, position, 
                    smokePrefab.transform.rotation);
                activeSmokeObjects.Add(smokeObj);
            }
        }
    }

    IEnumerator MoveAgent(GameObject agent, Vector3 targetPosition)
    {
        float startTime = Time.time;
        Vector3 startPosition = agent.transform.position;
        Quaternion originalRotation = mapGenerator.playerPrefab.transform.rotation;
        
        // Mantener la rotación original del prefab
        agent.transform.rotation = originalRotation;

        while (Vector3.Distance(agent.transform.position, targetPosition) > 0.01f)
        {
            float journeyLength = Vector3.Distance(startPosition, targetPosition);
            float distanceCovered = (Time.time - startTime) * moveSpeed;
            float fractionOfJourney = distanceCovered / journeyLength;
            
            agent.transform.position = Vector3.Lerp(startPosition, targetPosition, fractionOfJourney);
            // Mantener la rotación original durante todo el movimiento
            agent.transform.rotation = originalRotation;
            
            yield return null;
        }

        agent.transform.position = targetPosition;
        // Asegurar que la rotación final sea la correcta
        agent.transform.rotation = originalRotation;
    }

    // Métodos para control de la simulación
    public void PausarSimulacion()
    {
        isSimulationRunning = false;
    }

    public void ReanudarSimulacion()
    {
        if (!isSimulationRunning && currentStep < simulationSteps.Count)
        {
            isSimulationRunning = true;
            StartCoroutine(PlaySimulation());
        }
    }

    public void ReiniciarSimulacion()
    {
        isSimulationRunning = false;
        currentStep = 0;
        // Limpiar todos los objetos actuales
        foreach (var agent in agents.Values)
        {
            if (agent != null) Destroy(agent);
        }
        agents.Clear();
        foreach (var fire in activeFireObjects)
        {
            if (fire != null) Destroy(fire);
        }
        activeFireObjects.Clear();
        foreach (var smoke in activeSmokeObjects)
        {
            if (smoke != null) Destroy(smoke);
        }
        activeSmokeObjects.Clear();
        
        // Reiniciar la simulación
        StartSimulation();
    }
}

[System.Serializable]
public class SimulationData
{
    public List<Agent> agents;
    public List<FireData> fires;
    public List<SmokeData> smokes;
    public float[][] grid;
}

[System.Serializable]
public class Agent
{
    public int id;
    public int[] pos;
}

[System.Serializable]
public class FireData
{
    public int row;
    public int col;
}

[System.Serializable]
public class SmokeData
{
    public int row;
    public int col;
}