using UnityEngine;

namespace DevCameraMod.Scripts
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
