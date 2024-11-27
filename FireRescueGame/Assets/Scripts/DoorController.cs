using UnityEngine;

public class DoorController : MonoBehaviour
{
    public GameObject doorClosedPrefab;
    public GameObject doorOpenPrefab;

    private GameObject currentDoor;

    void Start()
    {
        // Inicialmente, la puerta está cerrada
        InstantiateDoor(false);
    }

    public void SetDoorState(bool open)
    {
        if (currentDoor != null)
        {
            Destroy(currentDoor);
        }
        InstantiateDoor(open);
    }

    private void InstantiateDoor(bool open)
    {
        if (open)
        {
            currentDoor = Instantiate(doorOpenPrefab, transform.position, transform.rotation, transform);
        }
        else
        {
            currentDoor = Instantiate(doorClosedPrefab, transform.position, transform.rotation, transform);
        }
    }
}