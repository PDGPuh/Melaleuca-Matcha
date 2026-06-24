using UnityEngine;

namespace RungTramTraSu
{
    public class BoatTrigger : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            // Kiểm tra xem đối tượng va chạm có phải là Người chơi (Player) không
            if (other.CompareTag("Player"))
            {
                if (Phase1Manager.Instance != null)
                {
                    Phase1Manager.Instance.BoardBoat();
                }
            }
        }
    }
}
