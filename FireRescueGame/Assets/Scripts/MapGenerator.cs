using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;

public class MapGenerator : MonoBehaviour
{
    // URLs del servidor
    private string serverUrl = "http://localhost:5000/api/map";
    private string simulationUrl = "http://localhost:5000/api/simulation";

    // Prefabs para la generación del mapa
    public GameObject wallPrefab;
    public GameObject floorPrefab;
    public GameObject doorPrefab;
    public GameObject firePrefab;
    public GameObject entryPointPrefab;
    public GameObject victimPrefab;
    public GameObject smokePrefab;
    public GameObject playerPrefab;
    public GameObject falseAlarmPrefab;
    public GameObject POIPrefab;



    // Tamaño de las celdas
    public float cellSize = 2f;
    private const float WALL_LENGTH = 2f;
    private const float WALL_THICKNESS = 0.1f;

    // Contenedores para organizar los objetos en la jerarquía
    private Transform _floorsContainer;
    private Transform _wallsContainer;
    private Transform _itemsContainer;

    // Variables para la simulación
    public float stepInterval = 1f; // Tiempo entre cada paso de la simulación
    public float moveSpeed = 5f;

    private Dictionary<int, GameObject> agents = new Dictionary<int, GameObject>();
    private Dictionary<int, GameObject> victims = new Dictionary<int, GameObject>();
    private List<GameObject> activeFireObjects = new List<GameObject>();
    private List<GameObject> activeSmokeObjects = new List<GameObject>();
    private List<GameObject> activeFireSimulationObjects = new List<GameObject>();
    private List<GameObject> activeSmokeSimulationObjects = new List<GameObject>();
    private Dictionary<string, GameObject> wallObjects = new Dictionary<string, GameObject>();

    private List<SimulationData> simulationSteps;
    private int currentStep = 0;
    private bool isSimulationRunning = false;
    private int rescuedVictims = 0;
    private int totalDamage = 0;

    void Start()
    {
        _floorsContainer = new GameObject("Floors").transform;
        _wallsContainer = new GameObject("Walls").transform;
        _itemsContainer = new GameObject("Items").transform;

        _floorsContainer.parent = transform;
        _wallsContainer.parent = transform;
        _itemsContainer.parent = transform;

        StartCoroutine(LoadScenario());
    }


    IEnumerator LoadScenario()
    {
        UnityWebRequest request = UnityWebRequest.Get(serverUrl);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string json = request.downloadHandler.text;
            ScenarioData scenario = JsonConvert.DeserializeObject<ScenarioData>(json);
            if (scenario == null || scenario.cells == null)
            {
                Debug.LogError("Error: JSON no se deserializó correctamente.");
                yield break;
            }
            GenerateScenario(scenario);

            StartCoroutine(FetchSimulation());
        }
        else
        {
            Debug.LogError("Error fetching scenario: " + request.error);
        }
    }

    void GenerateScenario(ScenarioData scenario)
    {
        GenerateFloor(scenario.cells.Length, scenario.cells[0].Length);

        HashSet<string> wallsToSkip = new HashSet<string>();

        foreach (var door in scenario.doors)
        {
            if (door.r1 == door.r2) // Puerta horizontal
            {
                int minCol = Mathf.Min(door.c1, door.c2);
                string wallKey1 = $"{door.r1},{minCol},right"; // Pared derecha de la celda izquierda
                string wallKey2 = $"{door.r1},{minCol + 1},left"; // Pared izquierda de la celda derecha
                wallsToSkip.Add(wallKey1);
                wallsToSkip.Add(wallKey2);
            }
            else if (door.c1 == door.c2) // Puerta vertical
            {
                int minRow = Mathf.Min(door.r1, door.r2);
                string wallKey1 = $"{minRow},{door.c1},bottom"; // Pared inferior de la celda superior
                string wallKey2 = $"{minRow + 1},{door.c1},top"; // Pared superior de la celda inferior
                wallsToSkip.Add(wallKey1);
                wallsToSkip.Add(wallKey2);
            }
        }
        foreach (var entry in scenario.entryPoints)
        {
            string cellValue = scenario.cells[entry.x - 1][entry.y - 1];
            
            bool isTopEdge = entry.x == 1;
            bool isBottomEdge = entry.x == scenario.cells.Length;
            bool isLeftEdge = entry.y == 1;
            bool isRightEdge = entry.y == scenario.cells[0].Length;

            if (isTopEdge)
            {
                wallsToSkip.Add($"{entry.x},{entry.y},top");
                if (isLeftEdge)
                {
                    wallsToSkip.Add($"{entry.x},{entry.y},left");
                }
                if (isRightEdge)
                {
                    wallsToSkip.Add($"{entry.x},{entry.y},right");
                }
            }
            else if (isBottomEdge)
            {
                wallsToSkip.Add($"{entry.x},{entry.y},bottom");
                if (isLeftEdge)
                {
                    wallsToSkip.Add($"{entry.x},{entry.y},left");
                }
                if (isRightEdge)
                {
                    wallsToSkip.Add($"{entry.x},{entry.y},right");
                }
            }
            else if (isLeftEdge)
            {
                wallsToSkip.Add($"{entry.x},{entry.y},left");
            }
            else if (isRightEdge)
            {

                wallsToSkip.Add($"{entry.x},{entry.y},right");
            }

            if (isTopEdge && entry.x > 1)
            {
                wallsToSkip.Add($"{entry.x - 1},{entry.y},bottom");
            }
            if (isBottomEdge && entry.x < scenario.cells.Length)
            {
                wallsToSkip.Add($"{entry.x + 1},{entry.y},top");
            }
            if (isLeftEdge && entry.y > 1)
            {
                wallsToSkip.Add($"{entry.x},{entry.y - 1},right");
            }
            if (isRightEdge && entry.y < scenario.cells[0].Length)
            {
                wallsToSkip.Add($"{entry.x},{entry.y + 1},left");
            }
        }

        for (int row = 0; row < scenario.cells.Length; row++)
        {
            for (int col = 0; col < scenario.cells[row].Length; col++)
            {
                string cell = scenario.cells[row][col];
                Vector3 basePosition = new Vector3(col * cellSize, 0, -row * cellSize);

                // Pared superior (Norte)
                if (cell[0] == '1' && !wallsToSkip.Contains($"{row + 1},{col + 1},top"))
                {
                    Vector3 topWallPos = new Vector3(
                        basePosition.x,
                        0,
                        basePosition.z - 1
                    );
                    GameObject wall = Instantiate(wallPrefab, topWallPos, Quaternion.identity, _wallsContainer);
                    wall.transform.Translate(cellSize / 2, 0, 0);
                    wallObjects[$"{row + 1},{col + 1},North"] = wall;
                }

                // Pared izquierda (Oeste)
                if (cell[1] == '1' && !wallsToSkip.Contains($"{row + 1},{col + 1},left"))
                {
                    Vector3 leftWallPos = new Vector3(
                        basePosition.x + 2,
                        0,
                        basePosition.z - 1
                    );
                    GameObject wall = Instantiate(wallPrefab, leftWallPos, Quaternion.Euler(0, 90, 0), _wallsContainer);
                    wall.transform.Translate(0, 0, -cellSize / 2);
                    wallObjects[$"{row + 1},{col + 1},West"] = wall;
                }

                // Pared inferior (Sur)
                if (cell[2] == '1' && !wallsToSkip.Contains($"{row + 1},{col + 1},bottom"))
                {
                    Vector3 bottomWallPos = new Vector3(
                        basePosition.x,
                        0,
                        basePosition.z - cellSize - 1
                    );
                    GameObject wall = Instantiate(wallPrefab, bottomWallPos, Quaternion.identity, _wallsContainer);
                    wall.transform.Translate(cellSize / 2, 0, 0);
                    wallObjects[$"{row + 1},{col + 1},South"] = wall;
                }

                // Pared derecha (Este)
                if (cell[3] == '1' && !wallsToSkip.Contains($"{row + 1},{col + 1},right"))
                {
                    Vector3 rightWallPos = new Vector3(
                        basePosition.x + cellSize + 2,
                        0,
                        basePosition.z - 1
                    );
                    GameObject wall = Instantiate(wallPrefab, rightWallPos, Quaternion.Euler(0, 90, 0), _wallsContainer);
                    wall.transform.Translate(0, 0, -cellSize / 2);
                    wallObjects[$"{row + 1},{col + 1},East"] = wall;
                }
            }
        }

        RemoveOverlappingWalls();
        GenerateOtherElements(scenario);
    }

    void GenerateFloor(int rows, int cols)
    {
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                Vector3 position = new Vector3(
                    col * cellSize + cellSize / 2,
                    -0.1f,
                    -row * cellSize - cellSize / 2
                );

                Instantiate(floorPrefab, position, Quaternion.identity, _floorsContainer);
            }
        }
    }

    void GenerateOtherElements(ScenarioData scenario)
    {
        foreach (var poi in scenario.pointsOfInterest)
        {
            Vector3 position = new Vector3(
                (poi.col - 1) * cellSize + cellSize,
                1,
                -((poi.row - 1) * cellSize + cellSize)
            );
            
            GameObject prefab = poi.type == "v" ? victimPrefab : falseAlarmPrefab;
            Quaternion rotation = prefab.transform.rotation;
            GameObject instantiatedPOI = Instantiate(prefab, position, rotation, _itemsContainer);
            instantiatedPOI.transform.localScale = prefab.transform.localScale;
        }

        // Generar puertas
        foreach (var door in scenario.doors)
        {
            Vector3 pos1 = new Vector3(
                (door.c1) * cellSize,
                0,
                -(door.r1) * cellSize
            );
            Vector3 pos2;
            if (door.r1 == door.r2)
            {
                pos2 = new Vector3(
                    (door.c2) * cellSize,
                    0,
                    -(door.r2 - 1) * cellSize
                );
            }
            else
            {
                pos2 = new Vector3(
                    (door.c2 - 1) * cellSize,
                    0,
                    -(door.r2) * cellSize
                );
            }
            Vector3 midPoint = (pos1 + pos2) / 2;
            float rotation = (door.r1 == door.r2) ? 90 : 0;
            GameObject doorObj = Instantiate(doorPrefab, midPoint, Quaternion.Euler(0, rotation, 0), _itemsContainer);
            doorObj.tag = "Door";
        }

        foreach (var entry in scenario.entryPoints)
        {
            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;

            int totalRows = scenario.cells.Length;
            int totalCols = scenario.cells[0].Length;

            bool isTop = entry.x == 1;
            bool isBottom = entry.x == totalRows;
            bool isLeft = entry.y == 1;
            bool isRight = entry.y == totalCols;

            if (isTop)
            {
                position = new Vector3(
                    (entry.y - 1) * cellSize + cellSize / 2,
                    0,
                    -(entry.x) * cellSize /2
                );
                rotation = Quaternion.Euler(0, 0, 0);
            }
            else if (isBottom)
            {
                position = new Vector3(
                    (entry.y -1) * cellSize + cellSize / 2,
                    0,
                    -(entry.x) * cellSize - 1
                );
                rotation = Quaternion.Euler(0, 0, 0);
            }
            else if (isLeft)
            {
                position = new Vector3(
                    (entry.y) * cellSize -1,
                    0,
                    -(entry.x - 1) * cellSize - cellSize / 2
                );
                rotation = Quaternion.Euler(0, 90, 0);
            }
            else if (isRight)
            {
                position = new Vector3(
                    entry.y * cellSize +1,
                    0,
                    -(entry.x - 1) * cellSize - cellSize / 2
                );
                rotation = Quaternion.Euler(0, 90, 0);
            }
            else
            {
                Debug.LogWarning($"Punto de entrada en posición ({entry.x}, {entry.y}) no está en el borde del mapa.");
                continue;
            }

            Instantiate(entryPointPrefab, position, rotation, _itemsContainer);
        }
    }

    void RemoveOverlappingWalls()
    {
        List<Collider> wallColliders = new List<Collider>();
        foreach (Transform wall in _wallsContainer)
        {
            Collider col = wall.GetComponent<Collider>();
            if (col != null)
            {
                wallColliders.Add(col);
            }
            else
            {
                Debug.LogWarning("Una pared no tiene un componente Collider.");
            }
        }

        List<GameObject> wallsToDestroy = new List<GameObject>();

        for (int i = 0; i < wallColliders.Count; i++)
        {
            for (int j = i + 1; j < wallColliders.Count; j++)
            {
                if (wallColliders[i].bounds.Intersects(wallColliders[j].bounds))
                {

                    wallsToDestroy.Add(wallColliders[j].gameObject);
                }
            }
        }

        foreach (GameObject wall in wallsToDestroy)
        {
            Destroy(wall);
        }
    }


    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 24;
        style.normal.textColor = Color.white;
        style.fontStyle = FontStyle.Bold;

        Texture2D bgTexture = new Texture2D(1, 1);
        bgTexture.SetPixel(0, 0, new Color(0, 0, 0, 0.5f));
        bgTexture.Apply();
        style.normal.background = bgTexture;

        Rect boxRect = new Rect(10, 10, 300, 150);
        GUI.Box(boxRect, "Estadísticas de la Simulación", style);

        GUIStyle labelStyle = new GUIStyle();
        labelStyle.fontSize = 20;
        labelStyle.normal.textColor = Color.white;

        GUI.Label(new Rect(20, 40, 280, 30), $"Pasos: {currentStep}", labelStyle);
        GUI.Label(new Rect(20, 70, 280, 30), $"Víctimas Rescatadas: {rescuedVictims}", labelStyle);
        GUI.Label(new Rect(20, 100, 280, 30), $"Daño Acumulado: {totalDamage}", labelStyle);
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
        if (data.walls != null)
        {
            HashSet<string> currentWalls = new HashSet<string>();
            foreach (var wall in data.walls)
            {
                string wallKey = $"{wall.row},{wall.col},{wall.direction}";
                currentWalls.Add(wallKey);
            }

            List<string> wallsToRemove = new List<string>();
            foreach (var wallPair in wallObjects)
            {
                if (!currentWalls.Contains(wallPair.Key))
                {
                    wallsToRemove.Add(wallPair.Key);
                }
            }

            foreach (var wallKey in wallsToRemove)
            {
                if (wallObjects.TryGetValue(wallKey, out GameObject wallObject))
                {
                    Destroy(wallObject);
                    wallObjects.Remove(wallKey);
                }
            }
        }

        foreach (var agent in data.agents)
        {

            Vector3 targetPosition = new Vector3(
                (agent.pos[1] - 1) * cellSize + cellSize + 2,
                1f,
                -((agent.pos[0] - 1) * cellSize + cellSize + 2)
            );

            if (!agents.ContainsKey(agent.id))
            {
                GameObject newAgent = Instantiate(playerPrefab, targetPosition, 
                    playerPrefab.transform.rotation, _itemsContainer);
                agents[agent.id] = newAgent;
            }

            StartCoroutine(MoveAgent(agents[agent.id], targetPosition));
        }


        foreach (var fire in activeFireObjects)
        {
            Destroy(fire);
        }
        activeFireObjects.Clear();

        foreach (var fire in data.fires)
        {
            Vector3 position = new Vector3(
                (fire.col - 1) * cellSize + cellSize + 2,
                1f,
                -((fire.row - 1) * cellSize + cellSize + 2)
            );
            GameObject fireObj = Instantiate(firePrefab, position, 
                firePrefab.transform.rotation, _itemsContainer);
            activeFireObjects.Add(fireObj);
        }


        foreach (var smoke in activeSmokeObjects)
        {
            Destroy(smoke);
        }
        activeSmokeObjects.Clear();


        if (data.smokes != null)
        {
            foreach (var smoke in data.smokes)
            {
                Vector3 position = new Vector3(
                    (smoke.col - 1) * cellSize + cellSize + 2,
                    1f,
                    -((smoke.row - 1) * cellSize + cellSize + 2)
                );
                GameObject smokeObj = Instantiate(smokePrefab, position, 
                    smokePrefab.transform.rotation, _itemsContainer);
                activeSmokeObjects.Add(smokeObj);
            }
        }


        if (data.victims != null)
        {

            foreach (var victim in data.victims)
            {
                Vector3 victimPosition = new Vector3(
                    (victim.pos[1] - 1) * cellSize + cellSize + 2,
                    1f,
                    -((victim.pos[0] - 1) * cellSize + cellSize + 2)
                );

                if (!victims.ContainsKey(victim.id))
                {
                    GameObject newVictim = Instantiate(victimPrefab, victimPosition, Quaternion.identity, _itemsContainer);
                    newVictim.name = $"Victim_{victim.id}";
                    victims[victim.id] = newVictim;
                }
            }


            var currentVictimIds = new HashSet<int>(data.victims.Select(v => v.id));
            var victimsToRemove = victims.Keys.Where(id => !currentVictimIds.Contains(id)).ToList();

            foreach (var victimId in victimsToRemove)
            {
                if (victims.ContainsKey(victimId))
                {
                    Destroy(victims[victimId]);
                    victims.Remove(victimId);
                    Debug.Log($"Víctima {victimId} rescatada y eliminada del mapa.");
                }
            }
        }


        rescuedVictims = data.rescued_victims;
        totalDamage = data.total_damage;
    }

    IEnumerator MoveAgent(GameObject agent, Vector3 targetPosition)
    {
        float startTime = Time.time;
        Vector3 startPosition = agent.transform.position;
        Quaternion originalRotation = playerPrefab.transform.rotation;
        

        agent.transform.rotation = originalRotation;

        while (Vector3.Distance(agent.transform.position, targetPosition) > 0.01f)
        {
            float journeyLength = Vector3.Distance(startPosition, targetPosition);
            float distanceCovered = (Time.time - startTime) * moveSpeed;
            float fractionOfJourney = distanceCovered / journeyLength;
            
            agent.transform.position = Vector3.Lerp(startPosition, targetPosition, fractionOfJourney);

            agent.transform.rotation = originalRotation;
            
            yield return null;
        }

        agent.transform.position = targetPosition;

        agent.transform.rotation = originalRotation;
    }


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
        foreach (var victim in victims.Values)
        {
            if (victim != null) Destroy(victim);
        }
        victims.Clear();

        StartSimulation();
    }

}


[System.Serializable]
public class ScenarioData
{
    public string[][] cells;
    public List<Door> doors;
    public List<EntryPoint> entryPoints;
    public List<FirePosition> firePositions;
    public List<PointOfInterest> pointsOfInterest;
}

[System.Serializable]
public class Door
{
    public int r1, c1, r2, c2;
}

[System.Serializable]
public class EntryPoint
{
    public int x, y;
}

[System.Serializable]
public class FirePosition
{
    public int x, y;
}

[System.Serializable]
public class PointOfInterest
{
    public int row, col;
    public string type;
}

[System.Serializable]
public class SimulationData
{
    public List<Agent> agents;
    public List<FireData> fires;
    public List<SmokeData> smokes;
    public List<VictimData> victims;
    public float[][] grid;
    public List<WallData> walls;
    public List<DoorData> doors;
    public int rescued_victims; 
    public int total_damage;
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

[System.Serializable]
public class WallData
{
    public int row;
    public int col;
    public string direction; // "N", "E", "S", "W"
}

[System.Serializable]
public class DoorData
{
    public int row1;
    public int col1;
    public int row2;
    public int col2;
    public bool is_open;
}

[System.Serializable]
public class VictimData
{
    public int id;
    public int[] pos;
}