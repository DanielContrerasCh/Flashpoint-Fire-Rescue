using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

public class MapGenerator : MonoBehaviour
{
    private string serverUrl = "http://localhost:5000/api/map";

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

    public float cellSize = 2f;
    private const float WALL_LENGTH = 2f;
    private const float WALL_THICKNESS = 0.1f;

    private Transform _floorsContainer;
    private Transform _wallsContainer;
    private Transform _itemsContainer;

    void Start()
    {
        // Crear contenedores para mejor organización
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
        }
        else
        {
            Debug.LogError("Error fetching scenario: " + request.error);
        }
    }

void GenerateScenario(ScenarioData scenario)
    {
        // Generar el piso primero
        GenerateFloor(scenario.cells.Length, scenario.cells[0].Length);

        // Crear un conjunto para almacenar las posiciones de las paredes que no deben generarse
        HashSet<string> wallsToSkip = new HashSet<string>();

        // Procesar las puertas cerradas
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

        // Modificar el procesamiento de los puntos de entrada
        foreach (var entry in scenario.entryPoints)
        {
            string cellValue = scenario.cells[entry.x - 1][entry.y - 1];
            
            // Determinar qué paredes remover basado en la posición del entry point
            bool isTopEdge = entry.x == 1;
            bool isBottomEdge = entry.x == scenario.cells.Length;
            bool isLeftEdge = entry.y == 1;
            bool isRightEdge = entry.y == scenario.cells[0].Length;

            if (isTopEdge)
            {
                // Remover pared superior y paredes adyacentes que comparten esquina
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
                // Remover pared inferior y paredes adyacentes que comparten esquina
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
                // Remover pared izquierda y paredes adyacentes que comparten esquina
                wallsToSkip.Add($"{entry.x},{entry.y},left");
            }
            else if (isRightEdge)
            {
                // Remover pared derecha y paredes adyacentes que comparten esquina
                wallsToSkip.Add($"{entry.x},{entry.y},right");
            }

            // Manejar las paredes de las celdas adyacentes
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

        // Generar paredes, saltando aquellas que coinciden con puertas o puntos de entrada
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
        // Generar puntos de interés
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

        // Generar puntos de entrada
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
        // Obtener todos los colliders de las paredes
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

        // Lista para almacenar paredes que deben ser destruidas
        List<GameObject> wallsToDestroy = new List<GameObject>();

        // Comparar cada pared con las demás para detectar colisiones
        for (int i = 0; i < wallColliders.Count; i++)
        {
            for (int j = i + 1; j < wallColliders.Count; j++)
            {
                if (wallColliders[i].bounds.Intersects(wallColliders[j].bounds))
                {
                    // Decidir cuál pared destruir
                    // Por ejemplo, destruir la pared en la posición j
                    wallsToDestroy.Add(wallColliders[j].gameObject);
                }
            }
        }

        // Eliminar las paredes duplicadas
        foreach (GameObject wall in wallsToDestroy)
        {
            Destroy(wall);
        }
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
