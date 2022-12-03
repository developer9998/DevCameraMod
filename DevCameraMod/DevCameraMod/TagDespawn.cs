using UnityEngine;

namespace DevCameraMod
{
    public class TagDespawn : MonoBehaviour
    {
        public VRRig rig;
        public void LateUpdate()
        {
            if (rig == null) Destroy(gameObject);
        }
    }
}
