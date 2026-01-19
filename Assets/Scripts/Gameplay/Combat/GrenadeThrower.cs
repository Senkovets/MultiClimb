using UnityEngine;

public class GrenadeThrower : MonoBehaviour
{
    [Header("Grenade Settings")]
    public GameObject grenadePrefab;   // префаб гранаты
    public Transform spawnPoint;       // точка по€влени€ гранаты

    private GrenadeNetwork previewGrenade;    // временна€ граната дл€ визуализации

    private void Update()
    {
        // «ажата кнопка Ч рисуем траекторию и радиус
        if (Input.GetKey(KeyCode.Space))
        {
            if (previewGrenade == null)
            {
                // создаЄм "фантомную" гранату дл€ расчЄтов
                GameObject g = Instantiate(grenadePrefab, spawnPoint.position, Quaternion.identity);
                previewGrenade = g.GetComponent<GrenadeNetwork>();

                // отключаем физику, чтобы фантом не падал
                Rigidbody rb = g.GetComponent<Rigidbody>();
                rb.isKinematic = true;
            }

            // считаем направление броска
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector3 targetPos = hit.point;
                Vector3 direction = (targetPos - spawnPoint.position).normalized;
             //   Vector3 velocity = direction * previewGrenade.throwForce;

                // рисуем траекторию и радиус приземлени€
              //  previewGrenade.DrawTrajectory(spawnPoint.position, velocity);
//previewGrenade.ShowExplosionRadiusAtLanding(spawnPoint.position, velocity);
            }
        }

        // ќтпустили кнопку Ч бросаем реальную гранату
        if (Input.GetKeyUp(KeyCode.Space))
        {
            if (previewGrenade != null)
            {
                // очищаем визуализацию и удал€ем фантом
               // previewGrenade.ClearVisuals();
                Destroy(previewGrenade.gameObject);
                previewGrenade = null;
            }

            // создаЄм насто€щую гранату
            GameObject currentGrenade = Instantiate(grenadePrefab, spawnPoint.position, Quaternion.identity);
            //currentGrenade.GetComponent<Grenade>().ThrowTowardsCursor();
        }
    }
}
