using UnityEngine;

public class GrenadeThrower : MonoBehaviour
{
    public GameObject grenadePrefab;
    public Transform spawnPoint;

    private void Update()
    {
        if ((Input.GetKeyDown(KeyCode.Space)))
        {
            Debug.Log("Бросаем гранату");
            GameObject currentGrenade = Instantiate(grenadePrefab, spawnPoint.position, Quaternion.identity);
            currentGrenade.GetComponent<Grenade>().ThrowTowardsCursor();
        }
        else
        {

        }
    }


}
