using UnityEngine;

namespace MA_Toolbox.MA_HeightMapCaptureRig
{
    public class Rotator : MonoBehaviour
    {
        [SerializeField]
        private float m_speed = 10.0f;

        // Update is called once per frame
        void Update()
        {
            this.transform.Rotate(Vector3.up * Time.deltaTime * m_speed, Space.Self);
        }
    }
}